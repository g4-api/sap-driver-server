using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sap.DriverServer.Domain.UnitTests.Support;
using Sap.DriverServer.Inspector.Services;
using Sap.DriverServer.Inspector.ViewModels;
using Sap.DriverServer.Inspector.Views;
using Sap.DriverServer.Models;

namespace Sap.DriverServer.Domain.UnitTests
{
    [TestClass]
    [OSCondition(OperatingSystems.Windows)]
    [TestCategory("Inspector")]
    [TestCategory("UnitTest")]
    public class MainViewModelTests
    {
        [STATestMethod(DisplayName = "Verify that the inspector starts without SAP GUI in a neutral waiting state, never attaches or throws while SAP Logon is absent, and attaches once it appears.")]
        public async Task WaitingForSapGuiTestAsync()
        {
            // Arrange: no SAP Logon, and an engine that would fail if the inspector tried to attach anyway.
            var isSapGuiRunning = false;
            var application = FakeSapTree.NewApplication();
            var engine = new FakeSapGuiEngine(application: null)
            {
                AttachException = SapDriverException.NewSessionNotCreated(SapErrorKinds.SapGuiNotRunning, "SAP GUI is not running.")
            };
            using var host = TestHosts.NewHost(engine);
            using var model = new MainViewModel(new LocalInspectorSource(new InspectorService(host), () => isSapGuiRunning), new Highlighter(), new CapabilitySetStore());

            // Act: start and tick through two retry intervals without SAP Logon.
            await model.InitializeSafeAsync();
            for (var tick = 0; tick < 2 * MainViewModel.LocalRetryTicks; tick++)
            {
                await model.OnTickAsync(point: null);
            }

            var waitingStatus = model.StatusText;
            var waitingHint = model.TreeHint;
            var attachAttempts = engine.Calls.FindAll(i => i == "GetApplication").Count;

            // Act: let SAP Logon appear and tick until the retry interval elapses.
            isSapGuiRunning = true;
            engine.AttachException = null;
            engine.Application = application;
            for (var tick = 0; tick < MainViewModel.LocalRetryTicks; tick++)
            {
                await model.OnTickAsync(point: null);
            }

            // Assert: the wait is neutral and never touched the engine; the retry attaches without user action.
            Assert.AreEqual("Waiting for SAP GUI on this machine…", waitingStatus);
            Assert.Contains("Create session", waitingHint);
            Assert.AreEqual(0, attachAttempts);
            Assert.HasCount(1, model.RootNodes);
            Assert.AreEqual("/app", model.RootNodes[0].Id);
            Assert.AreEqual(string.Empty, model.TreeHint);
        }

        [STATestMethod(DisplayName = "Verify that a SAP Logon that exits between the check and the attach still shows the neutral waiting state.")]
        public async Task SapGuiExitedDuringAttachTestAsync()
        {
            // Arrange: the check sees SAP Logon, but the attach finds it gone.
            var engine = new FakeSapGuiEngine(application: null)
            {
                AttachException = SapDriverException.NewSessionNotCreated(SapErrorKinds.SapGuiNotRunning, "SAP GUI is not running.")
            };
            using var host = TestHosts.NewHost(engine);
            using var model = new MainViewModel(new LocalInspectorSource(new InspectorService(host), () => true), new Highlighter(), new CapabilitySetStore());

            // Act: start.
            await model.InitializeSafeAsync();

            // Assert: the failure is reported as waiting, not as an error.
            Assert.AreEqual("Waiting for SAP GUI on this machine…", model.StatusText);
            Assert.Contains("Create session", model.TreeHint);
        }

        [STATestMethod(DisplayName = "Verify that a local failure other than a missing SAP Logon is reported as an error.")]
        public async Task ScriptingDisabledReportedTestAsync()
        {
            // Arrange: an engine that fails because scripting is disabled.
            var engine = new FakeSapGuiEngine(application: null)
            {
                AttachException = SapDriverException.NewSessionNotCreated(SapErrorKinds.ScriptingDisabled, "SAP GUI Scripting is disabled.")
            };
            using var host = TestHosts.NewHost(engine);
            using var model = new MainViewModel(new LocalInspectorSource(new InspectorService(host), () => true), new Highlighter(), new CapabilitySetStore());

            // Act: start.
            await model.InitializeSafeAsync();

            // Assert: the reason is shown with its SAP error kind.
            Assert.Contains(SapErrorKinds.ScriptingDisabled, model.StatusText);
        }
    }
}
