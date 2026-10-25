using System;
using System.Linq;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sap.DriverServer.Domain.UnitTests.Support;
using Sap.DriverServer.Models;
using Sap.DriverServer.Scripting;
using Sap.DriverServer.Settings;
using Sap.DriverServer.Settings.Models;

namespace Sap.DriverServer.Domain.UnitTests
{
    [TestClass]
    [OSCondition(OperatingSystems.Windows)]
    [TestCategory("SapIntegration")]
    public class SapIntegrationTests
    {
        [TestMethod(DisplayName = "Verify against a live SAP GUI that discovery lists scriptable sessions (read-only, opt-in).")]
        public void LiveDiscoveryTest()
        {
            // Arrange: attach through the production COM engine, or skip when not opted in or unavailable.
            using var host = NewLiveHost();

            // Act: discover the open connections and sessions.
            var connections = host.Invoke(SapGuiNavigator.GetConnections);

            // Assert: every discovered session has a well-formed scripting identifier.
            foreach (var session in connections.SelectMany(i => i.Sessions))
            {
                Assert.StartsWith("/app/con[", session.Id);
            }
        }

        [TestMethod(DisplayName = "Verify against a live SAP GUI that the object model document and status bar can be read (read-only, opt-in).")]
        public void LiveDocumentAndStatusBarTest()
        {
            // Arrange: attach and pick the first scriptable session, or skip when none is open.
            using var host = NewLiveHost();
            var connections = host.Invoke(SapGuiNavigator.GetConnections);
            var first = connections.SelectMany(i => i.Sessions).FirstOrDefault();
            if (first is null)
            {
                Assert.Inconclusive("No scriptable SAP GUI session is open.");
            }

            // Act: read the session document and the status bar without changing anything.
            var (root, statusBarType) = host.Invoke(application =>
            {
                var session = application.FindById(first.Id);
                var document = DocumentObjectModelFactory.NewDocument(session);
                var statusBar = session.FindById("wnd[0]/sbar");

                return (document.Root.Name.LocalName, statusBar?.Type);
            });

            // Assert: the document is rooted at the session and the main window has a status bar.
            Assert.AreEqual(SapGuiTypes.Session, root);
            Assert.AreEqual("GuiStatusbar", statusBarType);
        }

        // Creates a host over the production COM engine; marks the test inconclusive when not opted in or SAP is unavailable.
        private static SapScriptingHost NewLiveHost()
        {
            // Skip unless the operator explicitly enabled live SAP tests.
            if (!AppSettings.Environment.IsSapIntegrationEnabled)
            {
                Assert.Inconclusive($"Set {EnvironmentSettings.SapIntegrationKey}=true to run live SAP GUI tests.");
            }

            var host = new SapScriptingHost(new StaComDispatcher(), new ComSapGuiEngine(), TestHosts.NewSettings(), NullLogger<SapScriptingHost>.Instance);

            try
            {
                // Probe the engine; prerequisite failures make the test inconclusive instead of failed.
                host.Invoke(application => application.GetChildren().Count);
                return host;
            }
            catch (SapDriverException exception) when (exception.Error == WebDriverErrorCodes.SessionNotCreated)
            {
                host.Dispose();
                Assert.Inconclusive($"SAP GUI Scripting is unavailable: {exception.ErrorKind}.");
                throw;
            }
        }
    }
}
