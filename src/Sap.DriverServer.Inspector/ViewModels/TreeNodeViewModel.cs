using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

using Sap.DriverServer.Inspector.Models;
using Sap.DriverServer.Inspector.Mvvm;

namespace Sap.DriverServer.Inspector.ViewModels
{
    /// <summary>
    /// Represents one scripting object in the lazily loaded inspector tree.
    /// </summary>
    /// <remarks>
    /// Containers start with a single placeholder child so the tree shows an expander; the real children are read from
    /// SAP GUI the first time the node is expanded or revealed. Selecting a node notifies the owning view model,
    /// which loads the details. All members run on the UI thread.
    /// </remarks>
    public sealed class TreeNodeViewModel : ObservableObject
    {
        #region *** Fields       ***
        // Loads child summaries in the background; supplied by the main view model.
        private readonly Func<string, Task<IList<ElementSummary>>> _loadChildrenAsync;

        // Notifies the main view model when the user selects this node.
        private readonly Action<TreeNodeViewModel> _onSelected;

        // Records whether the real children replaced the placeholder.
        private bool _isLoaded;

        // Backs IsExpanded; expanding triggers the first child load.
        private bool _isExpanded;

        // Backs IsSelected; selection triggers the details load.
        private bool _isSelected;
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="TreeNodeViewModel"/> class.
        /// </summary>
        /// <param name="summary">The object summary; null creates the "Loading…" placeholder.</param>
        /// <param name="loadChildrenAsync">The child loader.</param>
        /// <param name="onSelected">The selection callback.</param>
        public TreeNodeViewModel(ElementSummary summary, Func<string, Task<IList<ElementSummary>>> loadChildrenAsync, Action<TreeNodeViewModel> onSelected)
        {
            // Keep the collaborators and show an expander for containers until their children load.
            Summary = summary;
            _loadChildrenAsync = loadChildrenAsync;
            _onSelected = onSelected;

            if (summary?.IsContainer == true)
            {
                Children.Add(new TreeNodeViewModel(summary: null, loadChildrenAsync, onSelected));
            }
        }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets the child nodes; a single placeholder until loaded.
        /// </summary>
        public ObservableCollection<TreeNodeViewModel> Children { get; } = [];

        /// <summary>
        /// Gets the tree label: type, name, and quoted text when present.
        /// </summary>
        public string Display
        {
            get
            {
                // Show the placeholder text for the expander stub.
                if (Summary is null)
                {
                    return "Loading…";
                }

                var text = string.IsNullOrEmpty(Summary.Text) ? string.Empty : $"  \"{Summary.Text}\"";
                return $"{Summary.Type}  {Summary.Name}{text}";
            }
        }

        /// <summary>
        /// Gets the object identifier, or null for the placeholder.
        /// </summary>
        public string Id => Summary?.Id;

        /// <summary>
        /// Gets or sets a value indicating whether the node is expanded; expanding loads the children once.
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                // Load the children the first time the user expands the node.
                if (SetProperty(ref _isExpanded, value) && value)
                {
                    _ = LoadChildrenAsync();
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the node is selected; selecting notifies the main view model.
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                // Report user and programmatic selections; the main view model ignores the ones it caused.
                if (SetProperty(ref _isSelected, value) && value && Summary is not null)
                {
                    _onSelected(this);
                }
            }
        }

        /// <summary>
        /// Gets the object summary, or null for the placeholder.
        /// </summary>
        public ElementSummary Summary { get; }
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Loads the real children once, replacing the placeholder.
        /// </summary>
        /// <returns>A task that completes when the children are present.</returns>
        public async Task LoadChildrenAsync()
        {
            // Load only once and never for leaves or the placeholder.
            if (_isLoaded || Summary is null || !Summary.IsContainer)
            {
                return;
            }

            _isLoaded = true;

            // Read the children in the background, then replace the placeholder on the UI thread.
            var children = await _loadChildrenAsync(Summary.Id).ConfigureAwait(true);
            Children.Clear();

            foreach (var child in children)
            {
                Children.Add(new TreeNodeViewModel(child, _loadChildrenAsync, _onSelected));
            }
        }

        /// <summary>
        /// Selects the node without notifying the main view model, for programmatic reveal after hover or navigation.
        /// </summary>
        public void SelectSilently()
        {
            // Update the backing field and notify the view only.
            _isSelected = true;
            OnPropertyChanged(nameof(IsSelected));
        }
        #endregion
    }
}
