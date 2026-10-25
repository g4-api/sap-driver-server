using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Input;

using Sap.DriverServer.Domain;
using Sap.DriverServer.Inspector.Models;
using Sap.DriverServer.Inspector.Mvvm;
using Sap.DriverServer.Inspector.Services;
using Sap.DriverServer.Models;

namespace Sap.DriverServer.Inspector.ViewModels
{
    /// <summary>
    /// Drives the Create session dialog: driver address, <c>sap:options</c> form and JSON, saved sets, and attaching.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The form and the capabilities JSON edit the same data. Editing a form field rewrites the <c>sap:options</c>
    /// member of the JSON and keeps every other capability; editing the JSON refills the form when the JSON is valid
    /// and reports the parse error otherwise. A guard flag stops the two updates from triggering each other.
    /// </para>
    /// <para>
    /// A successful Create or Attach sets <see cref="Result"/> and raises <see cref="Completed"/>; the dialog then
    /// closes and the main window takes ownership of the source. Created sessions are owned by the inspector and
    /// deleted on disconnect; attached sessions are not. Awaits resume on the UI thread because they update bound
    /// properties, the documented exception to the library-wide <c>ConfigureAwait(false)</c> rule.
    /// </para>
    /// </remarks>
    public sealed class CreateSessionViewModel : ObservableObject
    {
        #region *** Constants    ***
        // Names the capability that carries the SAP session selection.
        private const string OptionsKey = SapCapabilities.Options;

        // Writes the capabilities JSON indented, because users read and edit it in the dialog.
        private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };
        #endregion

        #region *** Fields       ***
        // Reads the local SAP sessions for prefilling; null when local inspection is unavailable.
        private readonly Func<Task<IList<SapConnectionInfoModel>>> _loadLocalSessionsAsync;

        // Creates a driver client for an address; replaced in tests to reach an in-process driver.
        private readonly Func<string, SapDriverClient> _newClient;

        // Reads and saves the named capability sets.
        private readonly CapabilitySetStore _store;

        // Backs CapabilitiesJson.
        private string _capabilitiesJson = "{}";

        // Backs Client.
        private string _client = string.Empty;

        // Backs ConnectionDescription.
        private string _connectionDescription = string.Empty;

        // Backs ConnectionIndex.
        private string _connectionIndex = string.Empty;

        // Backs DriverUrl.
        private string _driverUrl = SapDriverClient.DefaultDriverUrl;

        // Backs IsBusy.
        private bool _isBusy;

        // Stops the form and the JSON from updating each other recursively.
        private bool _isSyncing;

        // Backs JsonError.
        private string _jsonError = string.Empty;

        // Backs SelectedLocalSession.
        private LocalSessionOption _selectedLocalSession;

        // Backs SelectedOpenSession.
        private string _selectedOpenSession;

        // Backs SelectedSet.
        private CapabilitySet _selectedSet;

        // Backs SessionIndex.
        private string _sessionIndex = string.Empty;

        // Backs SessionPath.
        private string _sessionPath = string.Empty;

        // Backs SetName.
        private string _setName = string.Empty;

        // Backs StatusText.
        private string _statusText = string.Empty;

        // Backs SystemName.
        private string _systemName = string.Empty;

        /// <summary>
        /// Occurs when a session was created or attached and <see cref="Result"/> is set.
        /// </summary>
        public event EventHandler Completed;
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="CreateSessionViewModel"/> class.
        /// </summary>
        /// <param name="newClient">Creates a driver client for an address.</param>
        /// <param name="store">The saved capability sets.</param>
        /// <param name="loadLocalSessionsAsync">Reads the local SAP sessions; null when local inspection is unavailable.</param>
        public CreateSessionViewModel(Func<string, SapDriverClient> newClient, CapabilitySetStore store, Func<Task<IList<SapConnectionInfoModel>>> loadLocalSessionsAsync)
        {
            // Assert the required collaborators; the local loader is optional.
            AssertArguments(newClient, store);

            // Keep the collaborators.
            _newClient = newClient;
            _store = store;
            _loadLocalSessionsAsync = loadLocalSessionsAsync;

            // Wire the commands; each reports failures in the dialog status line.
            AttachCommand = new RelayCommand(_ => _ = RunAsync(AttachAsync), _ => !IsBusy && !string.IsNullOrEmpty(SelectedOpenSession));
            CreateCommand = new RelayCommand(_ => _ = RunAsync(CreateAsync), _ => !IsBusy && JsonError.Length == 0);
            DeleteSetCommand = new RelayCommand(_ => DeleteSet(), _ => SelectedSet is not null);
            PrefillCommand = new RelayCommand(_ => Prefill(SelectedLocalSession), _ => SelectedLocalSession is not null);
            RefreshLocalSessionsCommand = new RelayCommand(_ => _ = RunAsync(RefreshLocalSessionsAsync), _ => IsLocalAvailable && !IsBusy);
            RefreshOpenSessionsCommand = new RelayCommand(_ => _ = RunAsync(RefreshOpenSessionsAsync), _ => !IsBusy);
            RenameSetCommand = new RelayCommand(_ => RenameSet(), _ => SelectedSet is not null && !string.IsNullOrWhiteSpace(SetName));
            SaveSetCommand = new RelayCommand(_ => SaveSet(), _ => !string.IsNullOrWhiteSpace(SetName) && JsonError.Length == 0);

            // Load the saved sets; a broken file reads as empty.
            foreach (var set in _store.ReadSets())
            {
                SavedSets.Add(set);
            }

            return;

            // Requires the client factory and the store before the constructor wires commands to them.
            // The helper reads its parameters only and reports a missing one through its argument name.
            static void AssertArguments(Func<string, SapDriverClient> newClient, CapabilitySetStore store)
            {
                // Require both collaborators; the dialog cannot reach a driver or save sets without them.
                ArgumentNullException.ThrowIfNull(argument: newClient);
                ArgumentNullException.ThrowIfNull(argument: store);
            }
        }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets the command that attaches to the selected open session.
        /// </summary>
        public ICommand AttachCommand { get; }

        /// <summary>
        /// Gets or sets the capabilities JSON object placed in <c>alwaysMatch</c>.
        /// </summary>
        public string CapabilitiesJson
        {
            get => _capabilitiesJson;
            set
            {
                // Refill the form from valid JSON; report invalid JSON without touching the form.
                if (SetProperty(ref _capabilitiesJson, value) && !_isSyncing)
                {
                    UpdateFormFromJson();
                }
            }
        }

        /// <summary>
        /// Gets or sets the <c>client</c> option, such as <c>100</c>.
        /// </summary>
        public string Client
        {
            get => _client;
            set => SetFormField(ref _client, value);
        }

        /// <summary>
        /// Gets or sets the <c>connectionDescription</c> option, the SAP Logon entry description.
        /// </summary>
        public string ConnectionDescription
        {
            get => _connectionDescription;
            set => SetFormField(ref _connectionDescription, value);
        }

        /// <summary>
        /// Gets or sets the <c>connectionIndex</c> option as text; empty means "any connection".
        /// </summary>
        public string ConnectionIndex
        {
            get => _connectionIndex;
            set => SetFormField(ref _connectionIndex, value);
        }

        /// <summary>
        /// Gets the command that creates a new session with the capabilities.
        /// </summary>
        public ICommand CreateCommand { get; }

        /// <summary>
        /// Gets the command that deletes the selected saved set.
        /// </summary>
        public ICommand DeleteSetCommand { get; }

        /// <summary>
        /// Gets or sets the driver address.
        /// </summary>
        public string DriverUrl
        {
            get => _driverUrl;
            set => SetProperty(ref _driverUrl, value);
        }

        /// <summary>
        /// Gets a value indicating whether a driver call is running.
        /// </summary>
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                // Re-evaluate the commands that depend on the busy state.
                if (SetProperty(ref _isBusy, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether SAP GUI sessions on this machine can prefill the options.
        /// </summary>
        public bool IsLocalAvailable => _loadLocalSessionsAsync is not null;

        /// <summary>
        /// Gets the capabilities JSON parse error, or an empty string when the JSON is valid.
        /// </summary>
        public string JsonError
        {
            get => _jsonError;
            private set => SetProperty(ref _jsonError, value);
        }

        /// <summary>
        /// Gets the SAP GUI sessions found on this machine.
        /// </summary>
        public ObservableCollection<LocalSessionOption> LocalSessions { get; } = [];

        /// <summary>
        /// Gets the WebDriver sessions open on the driver, for Attach existing.
        /// </summary>
        public ObservableCollection<string> OpenSessions { get; } = [];

        /// <summary>
        /// Gets the command that fills the options from the selected local session.
        /// </summary>
        public ICommand PrefillCommand { get; }

        /// <summary>
        /// Gets the command that reads the local SAP GUI sessions.
        /// </summary>
        public ICommand RefreshLocalSessionsCommand { get; }

        /// <summary>
        /// Gets the command that reads the driver's open sessions.
        /// </summary>
        public ICommand RefreshOpenSessionsCommand { get; }

        /// <summary>
        /// Gets the command that renames the selected saved set to <see cref="SetName"/>.
        /// </summary>
        public ICommand RenameSetCommand { get; }

        /// <summary>
        /// Gets the created or attached source, or null until the dialog completes.
        /// </summary>
        public DriverInspectorSource Result { get; private set; }

        /// <summary>
        /// Gets the saved capability sets.
        /// </summary>
        public ObservableCollection<CapabilitySet> SavedSets { get; } = [];

        /// <summary>
        /// Gets the command that saves the current settings under <see cref="SetName"/>.
        /// </summary>
        public ICommand SaveSetCommand { get; }

        /// <summary>
        /// Gets or sets the local session chosen for prefilling.
        /// </summary>
        public LocalSessionOption SelectedLocalSession
        {
            get => _selectedLocalSession;
            set => SetProperty(ref _selectedLocalSession, value);
        }

        /// <summary>
        /// Gets or sets the open driver session chosen for attaching.
        /// </summary>
        public string SelectedOpenSession
        {
            get => _selectedOpenSession;
            set => SetProperty(ref _selectedOpenSession, value);
        }

        /// <summary>
        /// Gets or sets the selected saved set; selecting one loads its address and capabilities.
        /// </summary>
        public CapabilitySet SelectedSet
        {
            get => _selectedSet;
            set
            {
                // Load the set into the dialog; the JSON setter refills the form.
                if (SetProperty(ref _selectedSet, value) && value is not null)
                {
                    SetName = value.Name;
                    DriverUrl = value.DriverUrl;
                    CapabilitiesJson = value.Capabilities;
                }
            }
        }

        /// <summary>
        /// Gets or sets the <c>sessionIndex</c> option as text; empty means "any session".
        /// </summary>
        public string SessionIndex
        {
            get => _sessionIndex;
            set => SetFormField(ref _sessionIndex, value);
        }

        /// <summary>
        /// Gets or sets the <c>sessionPath</c> option, such as <c>/app/con[0]/ses[0]</c>.
        /// </summary>
        public string SessionPath
        {
            get => _sessionPath;
            set => SetFormField(ref _sessionPath, value);
        }

        /// <summary>
        /// Gets or sets the name used to save or rename a set.
        /// </summary>
        public string SetName
        {
            get => _setName;
            set => SetProperty(ref _setName, value);
        }

        /// <summary>
        /// Gets the dialog status line.
        /// </summary>
        public string StatusText
        {
            get => _statusText;
            private set => SetProperty(ref _statusText, value);
        }

        /// <summary>
        /// Gets or sets the <c>systemName</c> option, such as <c>DEV</c>.
        /// </summary>
        public string SystemName
        {
            get => _systemName;
            set => SetFormField(ref _systemName, value);
        }
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Attaches to the selected open session without taking ownership of it.
        /// </summary>
        /// <returns>A task that completes when the source is ready or the failure is reported.</returns>
        public async Task AttachAsync()
        {
            // Verify the session by loading its page source before handing the source over.
            var client = _newClient(DriverUrl);
            var source = new DriverInspectorSource(client, SelectedOpenSession, isOwner: false);
            await CompleteAsync(source).ConfigureAwait(true);
        }

        /// <summary>
        /// Creates a session with the current capabilities; the inspector owns and later deletes it.
        /// </summary>
        /// <returns>A task that completes when the source is ready or the failure is reported.</returns>
        public async Task CreateAsync()
        {
            // Parse the capabilities first, so a JSON error never reaches the driver.
            var capabilities = JsonNode.Parse(CapabilitiesJson) as JsonObject
                ?? throw new FormatException("The capabilities must be a JSON object.");

            var client = _newClient(DriverUrl);
            string sessionId;
            try
            {
                StatusText = "Creating the session…";
                sessionId = await client.NewSessionAsync(capabilities).ConfigureAwait(true);
            }
            catch
            {
                // The client is not handed over on failure, so release it here.
                client.Dispose();
                throw;
            }

            await CompleteAsync(new DriverInspectorSource(client, sessionId, isOwner: true)).ConfigureAwait(true);
        }

        /// <summary>
        /// Fills the options from a local SAP session: its path, system, and client.
        /// </summary>
        /// <param name="option">The local session, or null to do nothing.</param>
        public void Prefill(LocalSessionOption option)
        {
            // Select by path and add system and client, so the options stay readable and verifiable.
            if (option is null)
            {
                return;
            }

            _isSyncing = true;
            try
            {
                SessionPath = option.Id;
                SystemName = option.SystemName;
                Client = option.Client;
                ConnectionIndex = string.Empty;
                SessionIndex = string.Empty;
                ConnectionDescription = string.Empty;
            }
            finally
            {
                _isSyncing = false;
            }

            UpdateJsonFromForm();
        }

        /// <summary>
        /// Reads the local SAP sessions for the prefill picker.
        /// </summary>
        /// <returns>A task that completes when the picker is filled.</returns>
        public async Task RefreshLocalSessionsAsync()
        {
            // Flatten connections into sessions; discovery carries identifiers and system metadata only.
            var connections = await _loadLocalSessionsAsync().ConfigureAwait(true);

            LocalSessions.Clear();
            foreach (var session in connections.SelectMany(i => i.Sessions))
            {
                LocalSessions.Add(new LocalSessionOption
                {
                    Client = session.Client ?? string.Empty,
                    ConnectionDescription = session.ConnectionDescription ?? string.Empty,
                    Id = session.Id,
                    SystemName = session.SystemName ?? string.Empty
                });
            }

            SelectedLocalSession = LocalSessions.FirstOrDefault();
            StatusText = $"{LocalSessions.Count} local SAP GUI session(s).";
        }

        /// <summary>
        /// Reads the driver's open sessions and SAP GUI reachability for Attach existing.
        /// </summary>
        /// <returns>A task that completes when the list is filled.</returns>
        public async Task RefreshOpenSessionsAsync()
        {
            // Read the status; the driver lists its session identifiers there.
            using var client = _newClient(DriverUrl);
            var status = await client.GetStatusAsync().ConfigureAwait(true);

            OpenSessions.Clear();
            foreach (var session in status.Sessions ?? [])
            {
                OpenSessions.Add(session);
            }

            SelectedOpenSession = OpenSessions.FirstOrDefault();

            // Report SAP GUI reachability on the driver machine together with the session count.
            var sap = status.Sap;
            var sapText = sap is null || sap.Available ? "SAP GUI reachable" : $"SAP GUI not reachable ({sap.ErrorKind}): {sap.Message?.TrimEnd('.')}";
            StatusText = $"{OpenSessions.Count} open session(s); {sapText}.";
        }

        // Builds the sap:options object from the non-empty form fields; index fields that are not numbers stay text.
        private JsonObject NewOptions()
        {
            var options = new JsonObject();

            AddText("sessionPath", SessionPath);
            AddIndex("connectionIndex", ConnectionIndex);
            AddIndex("sessionIndex", SessionIndex);
            AddText("connectionDescription", ConnectionDescription);
            AddText("systemName", SystemName);
            AddText("client", Client);

            return options;

            // Adds a text option when it has a value.
            void AddText(string name, string value)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    options[name] = value.Trim();
                }
            }

            // Adds an index option as a number, or as text the driver rejects with a clear message.
            void AddIndex(string name, string value)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return;
                }

                options[name] = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)
                    ? JsonValue.Create(index)
                    : JsonValue.Create(value.Trim());
            }
        }

        // Validates a driver source by loading its root, then hands it to the main window.
        private async Task CompleteAsync(DriverInspectorSource source)
        {
            try
            {
                // Load the page source once, so a wrong session fails here instead of in the main window.
                StatusText = "Loading the session…";
                await source.GetRootAsync().ConfigureAwait(true);
            }
            catch
            {
                // Release the source; an owned session is deleted so it does not block the driver's single slot.
                await source.DisconnectAsync().ConfigureAwait(true);
                throw;
            }

            Result = source;
            Completed?.Invoke(this, EventArgs.Empty);
        }

        // Removes the selected saved set and saves the list.
        private void DeleteSet()
        {
            // Remove the set from the list and the selection, then persist the shorter list.
            SavedSets.Remove(SelectedSet);
            SelectedSet = null;
            SaveSets("Deleted the set.");
        }

        // Renames the selected saved set, keeping names unique.
        private void RenameSet()
        {
            // Refuse a name another set already uses.
            var name = SetName.Trim();
            if (SavedSets.Any(i => i != SelectedSet && string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                StatusText = $"A set named '{name}' already exists.";
                return;
            }

            // Rename, keep the order and the selection, then persist.
            SelectedSet.Name = name;
            var selected = SelectedSet;
            SortSets();
            SelectedSet = selected;
            SaveSets($"Renamed the set to '{name}'.");
        }

        // Runs a dialog operation with the busy flag and reports any failure in the status line.
        private async Task RunAsync(Func<Task> operation)
        {
            // Mark the dialog busy, so the commands that reach the driver are disabled meanwhile.
            IsBusy = true;
            try
            {
                await operation().ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                // Show driver failures with their W3C code and SAP kind, other failures with their message.
                StatusText = exception is SapDriverException driverException
                    ? $"{driverException.Error} ({driverException.ErrorKind ?? "no kind"}): {driverException.Message}"
                    : exception.GetBaseException().Message;
            }
            finally
            {
                // Re-enable the commands whatever the outcome.
                IsBusy = false;
            }
        }

        // Saves the current address and capabilities under the set name, replacing a set with the same name.
        private void SaveSet()
        {
            // Replace an existing set of the same name, otherwise add a new one.
            var name = SetName.Trim();
            var set = SavedSets.FirstOrDefault(i => string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase));
            if (set is null)
            {
                set = new CapabilitySet { Name = name };
                SavedSets.Add(set);
            }

            // Copy the current settings, keep the list sorted, and select the saved set without reloading it.
            set.DriverUrl = DriverUrl;
            set.Capabilities = CapabilitiesJson;
            SortSets();
            _selectedSet = set;
            OnPropertyChanged(nameof(SelectedSet));
            SaveSets($"Saved the set '{name}'.");
        }

        // Writes the saved sets to disk and reports the outcome.
        private void SaveSets(string successText)
        {
            try
            {
                // Write the whole list; the store replaces the file atomically.
                _store.SaveSets(SavedSets);
                StatusText = successText;
            }
            catch (Exception exception) when (exception is System.IO.IOException or UnauthorizedAccessException)
            {
                // Keep the in-memory list and name the file that was not written.
                StatusText = $"The sets were not saved to {_store.FilePath}: {exception.Message}";
            }
        }

        // Updates one form field and rewrites the JSON options, unless the JSON is the one being applied.
        // The property name defaults through CallerMemberName, which requires an optional parameter.
        private void SetFormField(ref string field, string value, [CallerMemberName] string propertyName = null)
        {
            // Store the value, then mirror it into the JSON unless the JSON is the source of this change.
            if (SetProperty(ref field, value ?? string.Empty, propertyName) && !_isSyncing)
            {
                UpdateJsonFromForm();
            }
        }

        // Orders the saved sets by name, as they are shown.
        private void SortSets()
        {
            // Rebuild the observable list in name order, which is the order the store reads back.
            var sorted = SavedSets.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToList();
            SavedSets.Clear();
            foreach (var set in sorted)
            {
                SavedSets.Add(set);
            }
        }

        // Refills the form from the sap:options member of valid JSON; reports invalid JSON instead.
        private void UpdateFormFromJson()
        {
            JsonObject root;
            try
            {
                // Require a JSON object; capabilities are always an object.
                root = JsonNode.Parse(string.IsNullOrWhiteSpace(CapabilitiesJson) ? "{}" : CapabilitiesJson) as JsonObject;
                if (root is null)
                {
                    JsonError = "The capabilities must be a JSON object.";
                    return;
                }
            }
            catch (JsonException exception)
            {
                JsonError = exception.Message;
                return;
            }

            // Apply each option, without rewriting the JSON the user is typing.
            JsonError = string.Empty;
            var options = root[OptionsKey] as JsonObject;
            _isSyncing = true;
            try
            {
                SessionPath = ReadOption(options, "sessionPath");
                ConnectionIndex = ReadOption(options, "connectionIndex");
                SessionIndex = ReadOption(options, "sessionIndex");
                ConnectionDescription = ReadOption(options, "connectionDescription");
                SystemName = ReadOption(options, "systemName");
                Client = ReadOption(options, "client");
            }
            finally
            {
                _isSyncing = false;
            }

            return;

            // Reads one option as text; numbers keep their JSON form.
            static string ReadOption(JsonObject options, string name)
            {
                var node = options?[name];
                return node is JsonValue value && value.TryGetValue<string>(out var text) ? text : node?.ToJsonString() ?? string.Empty;
            }
        }

        // Rewrites the sap:options member of the JSON from the form, keeping every other capability.
        private void UpdateJsonFromForm()
        {
            // Start from the current JSON when it is valid, so other capabilities survive form edits.
            JsonObject root;
            try
            {
                root = JsonNode.Parse(string.IsNullOrWhiteSpace(CapabilitiesJson) ? "{}" : CapabilitiesJson) as JsonObject ?? [];
            }
            catch (JsonException)
            {
                root = [];
            }

            // Replace the options; an empty form removes the member so the driver applies no criteria.
            var options = NewOptions();
            if (options.Count == 0)
            {
                root.Remove(OptionsKey);
            }
            else
            {
                root[OptionsKey] = options;
            }

            _isSyncing = true;
            try
            {
                CapabilitiesJson = root.ToJsonString(IndentedJson);
                JsonError = string.Empty;
            }
            finally
            {
                _isSyncing = false;
            }
        }
        #endregion
    }
}
