using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sap.DriverServer.Domain.UnitTests.Support;
using Sap.DriverServer.Inspector.Models;
using Sap.DriverServer.Inspector.Services;
using Sap.DriverServer.Inspector.ViewModels;

namespace Sap.DriverServer.Domain.UnitTests
{
    [TestClass]
    [OSCondition(OperatingSystems.Windows)]
    [TestCategory("Inspector")]
    [TestCategory("UnitTest")]
    public class CreateSessionViewModelTests
    {
        [TestMethod(DisplayName = "Verify that form edits rewrite sap:options and keep the other capabilities.")]
        public void FormToJsonTest()
        {
            // Arrange: a dialog whose JSON already carries an unrelated capability.
            using var store = new TemporaryStore();
            var model = NewModel(store.Store);
            model.CapabilitiesJson = "{\"g4:trace\": true}";

            // Act: fill the system, the client, and a numeric connection index.
            model.SystemName = "DEV";
            model.Client = "100";
            model.ConnectionIndex = "1";
            var json = JsonNode.Parse(model.CapabilitiesJson).AsObject();

            // Assert: the options are written, the index is a number, and the other capability survives.
            Assert.AreEqual("DEV", (string)json["sap:options"]["systemName"]);
            Assert.AreEqual("100", (string)json["sap:options"]["client"]);
            Assert.AreEqual(1, (int)json["sap:options"]["connectionIndex"]);
            Assert.IsTrue((bool)json["g4:trace"]);
        }

        [TestMethod(DisplayName = "Verify that Format re-indents valid JSON with four spaces and refills the form.")]
        public void FormatJsonTest()
        {
            // Arrange: a dialog with compact JSON typed by hand.
            using var store = new TemporaryStore();
            var model = NewModel(store.Store);
            model.CapabilitiesJson = "{\"sap:options\":{\"systemName\":\"DEV\"}}";

            // Act: format the JSON.
            model.FormatJson();
            var lines = model.CapabilitiesJson.Split('\n').Select(i => i.TrimEnd('\r')).ToArray();

            // Assert: one member per line with a 4-space indent, no error, and the form still holds the option.
            Assert.AreEqual("{", lines[0]);
            Assert.AreEqual("    \"sap:options\": {", lines[1]);
            Assert.AreEqual("        \"systemName\": \"DEV\"", lines[2]);
            Assert.AreEqual(string.Empty, model.JsonError);
            Assert.AreEqual("DEV", model.SystemName);
        }

        [TestMethod(DisplayName = "Verify that Format keeps invalid JSON as typed and says to correct it first.")]
        public void FormatJsonInvalidTest()
        {
            // Arrange: a dialog with broken JSON, then with a JSON array instead of an object.
            using var store = new TemporaryStore();
            var model = NewModel(store.Store);
            const string broken = "{\"sap:options\": ";
            model.CapabilitiesJson = broken;

            // Act: format the broken text, then the array.
            model.FormatJson();
            var textAfterBroken = model.CapabilitiesJson;
            var errorAfterBroken = model.JsonError;
            model.CapabilitiesJson = "[1, 2]";
            model.FormatJson();

            // Assert: both texts stay as typed, and each error ends with the guidance.
            Assert.AreEqual(broken, textAfterBroken);
            Assert.EndsWith("Correct the JSON text before formatting.", errorAfterBroken);
            Assert.AreEqual("[1, 2]", model.CapabilitiesJson);
            Assert.AreEqual("The capabilities must be a JSON object. Correct the JSON text before formatting.", model.JsonError);
        }

        [TestMethod(DisplayName = "Verify that Format leaves empty text empty.")]
        public void FormatJsonEmptyTest()
        {
            // Arrange: a dialog whose JSON was cleared.
            using var store = new TemporaryStore();
            var model = NewModel(store.Store);
            model.CapabilitiesJson = string.Empty;

            // Act: format the empty text.
            model.FormatJson();

            // Assert: the text stays empty and no error is reported.
            Assert.AreEqual(string.Empty, model.CapabilitiesJson);
            Assert.AreEqual(string.Empty, model.JsonError);
        }

        [TestMethod(DisplayName = "Verify that JSON edits refill the form, invalid JSON is reported, and an empty form removes sap:options.")]
        public void JsonToFormTest()
        {
            // Arrange: a dialog over a temporary store.
            using var store = new TemporaryStore();
            var model = NewModel(store.Store);

            // Act: type valid JSON, then invalid JSON.
            model.CapabilitiesJson = "{\"sap:options\": {\"sessionPath\": \"/app/con[0]/ses[1]\", \"sessionIndex\": 1}}";
            var pathAfterValid = model.SessionPath;
            var indexAfterValid = model.SessionIndex;
            model.CapabilitiesJson = "{\"sap:options\": ";
            var errorAfterInvalid = model.JsonError;
            var pathAfterInvalid = model.SessionPath;

            // Act: fix the JSON, then clear the form.
            model.CapabilitiesJson = "{\"sap:options\": {\"sessionPath\": \"/app/con[0]/ses[1]\"}}";
            model.SessionPath = string.Empty;

            // Assert: valid JSON filled the form, invalid JSON left it alone, and clearing removed the options.
            Assert.AreEqual("/app/con[0]/ses[1]", pathAfterValid);
            Assert.AreEqual("1", indexAfterValid);
            Assert.IsGreaterThan(0, errorAfterInvalid.Length);
            Assert.AreEqual("/app/con[0]/ses[1]", pathAfterInvalid);
            Assert.AreEqual(string.Empty, model.JsonError);
            Assert.IsNull(JsonNode.Parse(model.CapabilitiesJson)["sap:options"]);
        }

        [TestMethod(DisplayName = "Verify that prefilling from a local session selects it by path, system, and client.")]
        public void PrefillTest()
        {
            // Arrange: a dialog with an index already typed.
            using var store = new TemporaryStore();
            var model = NewModel(store.Store);
            model.SessionIndex = "3";

            // Act: prefill from a local session.
            model.Prefill(new LocalSessionOption { Client = "200", Id = "/app/con[1]/ses[0]", SystemName = "QAS" });
            var options = JsonNode.Parse(model.CapabilitiesJson)["sap:options"].AsObject();

            // Assert: the path, system, and client replace the previous criteria.
            Assert.AreEqual("/app/con[1]/ses[0]", (string)options["sessionPath"]);
            Assert.AreEqual("QAS", (string)options["systemName"]);
            Assert.AreEqual("200", (string)options["client"]);
            Assert.IsFalse(options.ContainsKey("sessionIndex"));
        }

        [TestMethod(DisplayName = "Verify that capability sets are saved, renamed, and deleted on disk.")]
        public void SavedSetsTest()
        {
            // Arrange: a dialog with options and a set name.
            using var store = new TemporaryStore();
            var model = NewModel(store.Store);
            model.DriverUrl = "http://sap-box:5556/wd/hub";
            model.SystemName = "DEV";
            model.SetName = "dev box";

            // Act: save, rename, and read the file back.
            model.SaveSetCommand.Execute(parameter: null);
            model.SetName = "DEV main";
            model.RenameSetCommand.Execute(parameter: null);
            var afterRename = store.Store.ReadSets();

            // Act: delete the set and read the file back.
            model.DeleteSetCommand.Execute(parameter: null);
            var afterDelete = store.Store.ReadSets();

            // Assert: the renamed set kept its address and options, and deleting emptied the file.
            Assert.HasCount(1, afterRename);
            Assert.AreEqual("DEV main", afterRename[0].Name);
            Assert.AreEqual("http://sap-box:5556/wd/hub", afterRename[0].DriverUrl);
            Assert.Contains("\"systemName\": \"DEV\"", afterRename[0].Capabilities);
            Assert.IsEmpty(afterDelete);
        }

        [TestMethod(DisplayName = "Verify that selecting a saved set loads its address and options into the dialog.")]
        public void SelectSavedSetTest()
        {
            // Arrange: a store that already holds one set.
            using var store = new TemporaryStore();
            var set = new CapabilitySet { Capabilities = "{\"sap:options\": {\"client\": \"300\"}}", DriverUrl = "http://other:5556/wd/hub", Name = "other" };
            store.Store.SaveSets([set]);
            var model = NewModel(store.Store);

            // Act: select the loaded set.
            model.SelectedSet = model.SavedSets.Single();

            // Assert: the address, form, and name follow the set.
            Assert.AreEqual("http://other:5556/wd/hub", model.DriverUrl);
            Assert.AreEqual("300", model.Client);
            Assert.AreEqual("other", model.SetName);
        }

        [TestMethod(DisplayName = "Verify that the store reads a missing or corrupt file as empty and sorts sets by name.")]
        public void StoreReadTest()
        {
            // Arrange: a temporary store path.
            using var store = new TemporaryStore();

            // Act: read before any save, save two sets, then corrupt the file.
            var missing = store.Store.ReadSets();
            store.Store.SaveSets([new CapabilitySet { Name = "b" }, new CapabilitySet { Name = "A" }]);
            var saved = store.Store.ReadSets();
            File.WriteAllText(store.Store.FilePath, "{ not json");
            var corrupt = store.Store.ReadSets();

            // Assert: absence and corruption read as empty; saved sets come back sorted.
            Assert.IsEmpty(missing);
            var expectedNames = new[] { "A", "b" };
            Assert.AreSequenceEqual(expectedNames, saved.Select(i => i.Name).ToArray());
            Assert.IsEmpty(corrupt);
        }

        [TestMethod(DisplayName = "Verify that Create returns an owned source and Attach returns a source that does not own the session.")]
        public async Task CreateAndAttachTestAsync()
        {
            // Arrange: an in-process driver over the default fake tree, reached by every client the dialog creates.
            await using var factory = TestDrivers.NewFactory(FakeSapTree.NewApplication());
            using var store = new TemporaryStore();
            var model = new CreateSessionViewModel(newClient: _ => TestDrivers.NewClient(factory), store.Store, loadLocalSessionsAsync: null);
            var completions = 0;
            model.Completed += (_, _) => completions++;
            model.SystemName = "DEV";

            // Act: create a session, then list the open sessions and attach to it with a second dialog.
            await model.CreateAsync();
            var created = model.Result;
            var attachModel = new CreateSessionViewModel(newClient: _ => TestDrivers.NewClient(factory), store.Store, loadLocalSessionsAsync: null);
            await attachModel.RefreshOpenSessionsAsync();
            await attachModel.AttachAsync();

            // Assert: the created source owns its session; the attached source points at the same session without owning it.
            Assert.AreEqual(1, completions);
            Assert.IsTrue(created.IsOwner);
            Assert.AreEqual(created.SessionId, attachModel.SelectedOpenSession);
            Assert.IsFalse(attachModel.Result.IsOwner);
            Assert.AreEqual(created.SessionId, attachModel.Result.SessionId);
            Assert.IsFalse(model.IsLocalAvailable);

            // Cleanup: release both sources; the owned one deletes the session.
            attachModel.Result.Dispose();
            await created.DisconnectAsync();
        }

        // Creates a dialog model whose clients point at an address nothing serves; only offline members are exercised.
        private static CreateSessionViewModel NewModel(CapabilitySetStore store)
        {
            return new CreateSessionViewModel(newClient: url => new SapDriverClient(url), store, loadLocalSessionsAsync: null);
        }

        // Owns a store over a unique temporary file and deletes its folder on disposal.
        private sealed class TemporaryStore : IDisposable
        {
            // Holds the unique folder that contains the store file.
            private readonly string _folder = Path.Combine(Path.GetTempPath(), $"g4-sap-inspector-{Guid.NewGuid():N}");

            public TemporaryStore()
            {
                // Point the store at a file inside the unique folder; the first save creates the folder.
                Store = new CapabilitySetStore(Path.Combine(_folder, "capability-sets.json"));
            }

            public CapabilitySetStore Store { get; }

            public void Dispose()
            {
                // Remove the folder and everything the test wrote.
                if (Directory.Exists(_folder))
                {
                    Directory.Delete(_folder, recursive: true);
                }
            }
        }
    }
}
