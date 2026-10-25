using System;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sap.DriverServer.Models;
using Sap.DriverServer.Scripting;

namespace Sap.DriverServer.Domain.UnitTests
{
    [TestClass]
    [OSCondition(OperatingSystems.Windows)]
    [TestCategory("Scripting")]
    [TestCategory("UnitTest")]
    public class ComSapGuiEngineTests
    {
        // Holds E_ACCESSDENIED, which Windows returns when SAP Logon runs as another user or integrity level.
        private const int AccessDenied = unchecked((int)0x80070005);

        // Holds E_FAIL, the generic refusal SAP GUI returns when scripting is disabled.
        private const int Failed = unchecked((int)0x80004005);

        [TestMethod(DisplayName = "Verify that a missing SAPGUI table entry fails with sapGuiNotRunning.")]
        public void MissingEntryTest()
        {
            // Arrange: a table without the SAPGUI entry.
            var engine = new ComSapGuiEngine(new FakeTableReader(() => null));

            // Act: attach.
            var exception = Assert.ThrowsExactly<SapDriverException>(() => engine.GetApplication());

            // Assert: the failure names SAP Logon and is classified as not running.
            Assert.AreEqual(SapErrorKinds.SapGuiNotRunning, exception.ErrorKind);
            Assert.AreEqual(WebDriverErrorCodes.SessionNotCreated, exception.Error);
            Assert.Contains("SAP Logon", exception.Message);
        }

        [TestMethod(DisplayName = "Verify that table access denial fails with accessDenied and other table failures with sapGuiNotRunning.")]
        public void TableFailureClassificationTest()
        {
            // Arrange: one table that denies access and one that fails generically.
            var denied = new ComSapGuiEngine(new FakeTableReader(() => throw new COMException("denied", AccessDenied)));
            var failed = new ComSapGuiEngine(new FakeTableReader(() => throw new COMException("failed", Failed)));

            // Act: attach through both.
            var deniedException = Assert.ThrowsExactly<SapDriverException>(() => denied.GetApplication());
            var failedException = Assert.ThrowsExactly<SapDriverException>(() => failed.GetApplication());

            // Assert: denial keeps its own kind; the generic failure means the entry is unreachable.
            Assert.AreEqual(SapErrorKinds.AccessDenied, deniedException.ErrorKind);
            Assert.AreEqual(SapErrorKinds.SapGuiNotRunning, failedException.ErrorKind);
        }

        [TestMethod(DisplayName = "Verify that a refused or empty scripting engine fails with scriptingDisabled, and denial with accessDenied.")]
        public void ScriptingEngineRefusalTest()
        {
            // Arrange: entries that return no engine, refuse it, and deny access to it.
            var empty = new ComSapGuiEngine(new FakeTableReader(() => new FakeRotEntry(() => null)));
            var refused = new ComSapGuiEngine(new FakeTableReader(() => new FakeRotEntry(() => throw new COMException("off", Failed))));
            var denied = new ComSapGuiEngine(new FakeTableReader(() => new FakeRotEntry(() => throw new COMException("denied", AccessDenied))));

            // Act: attach through each entry.
            var emptyException = Assert.ThrowsExactly<SapDriverException>(() => empty.GetApplication());
            var refusedException = Assert.ThrowsExactly<SapDriverException>(() => refused.GetApplication());
            var deniedException = Assert.ThrowsExactly<SapDriverException>(() => denied.GetApplication());

            // Assert: both refusals point at the SAP GUI scripting option; denial keeps its own kind.
            Assert.AreEqual(SapErrorKinds.ScriptingDisabled, emptyException.ErrorKind);
            Assert.AreEqual(SapErrorKinds.ScriptingDisabled, refusedException.ErrorKind);
            Assert.AreEqual(SapErrorKinds.AccessDenied, deniedException.ErrorKind);
        }

        [TestMethod(DisplayName = "Verify that the engine attaches once, reuses the application, and attaches again after Reset.")]
        public void AttachmentLifecycleTest()
        {
            // Arrange: a table whose SAPGUI entry returns an application object.
            var reader = new FakeTableReader(() => new FakeRotEntry(() => new FakeApplication()));
            var engine = new ComSapGuiEngine(reader);

            // Act: attach twice, reset, and attach again.
            var first = engine.GetApplication();
            var second = engine.GetApplication();
            engine.Reset();
            engine.GetApplication();

            // Assert: the application is reused until the reset, and its members are reachable.
            Assert.AreSame(first, second);
            Assert.AreEqual("/app", first.Id);
            Assert.AreEqual("GuiApplication", first.Type);
            Assert.AreEqual(2, reader.Reads);
            Assert.AreEqual("SAPGUI", reader.LastItemName);
        }

        [TestMethod(DisplayName = "Verify that the real table reader returns null for an entry no process registered.")]
        public void RealTableMissingEntryTest()
        {
            // Arrange: the production reader over this machine's running object table.
            var reader = new RunningObjectTableReader();

            // Act: look up a name no process registers.
            var entry = reader.GetObject($"G4-Missing-{Guid.NewGuid():N}");

            // Assert: absence is reported as null, not as an exception.
            Assert.IsNull(entry);
        }

        // Represents GuiApplication with the two members the engine test reads through late binding.
        public sealed class FakeApplication
        {
            public string Id { get; } = "/app";

            public string Type { get; } = "GuiApplication";
        }

        // Represents the SAPGUI table entry, whose only member returns the scripting engine.
        public sealed class FakeRotEntry(Func<object> getScriptingEngine)
        {
            public object GetScriptingEngine()
            {
                // Return or fail exactly as the test scenario requires.
                return getScriptingEngine();
            }
        }

        // Serves table lookups from a delegate and records how often and for which name the engine asked.
        private sealed class FakeTableReader(Func<object> getObject) : IRunningObjectTableReader
        {
            public string LastItemName { get; private set; }

            public int Reads { get; private set; }

            public object GetObject(string itemName)
            {
                // Record the lookup, then answer from the scenario delegate.
                Reads++;
                LastItemName = itemName;

                return getObject();
            }
        }
    }
}
