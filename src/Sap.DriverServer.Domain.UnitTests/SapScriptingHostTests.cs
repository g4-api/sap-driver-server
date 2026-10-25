using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sap.DriverServer.Domain.UnitTests.Support;
using Sap.DriverServer.Models;
using Sap.DriverServer.Scripting;

namespace Sap.DriverServer.Domain.UnitTests
{
    [TestClass]
    [OSCondition(OperatingSystems.Windows)]
    [TestCategory("SapScriptingHost")]
    [TestCategory("UnitTest")]
    public class SapScriptingHostTests
    {
        [TestMethod(DisplayName = "Verify that every engine call of an operation runs on the dispatcher thread and objects are released afterwards.")]
        public void InvokeRunsOnDispatcherAndReleasesTest()
        {
            // Arrange: create a host over a fake engine.
            var engine = new FakeSapGuiEngine(FakeSapTree.NewApplication());
            using var host = TestHosts.NewHost(engine);

            // Act: run an operation that records its own thread.
            var operationThreadId = host.Invoke(_ => Environment.CurrentManagedThreadId);

            // Assert: attachment, operation, and release ran on one non-caller thread, in that order.
            Assert.HasCount(1, engine.ThreadIds);
            Assert.Contains(operationThreadId, engine.ThreadIds);
            Assert.AreNotEqual(Environment.CurrentManagedThreadId, operationThreadId);
            var expectedCalls = new[] { "GetApplication", "Release" };
            Assert.AreSequenceEqual(expectedCalls, engine.Calls);
        }

        [TestMethod(DisplayName = "Verify that a COM failure inside an operation becomes a classified SapDriverException and objects are still released.")]
        public void InvokeConvertsComFailureTest()
        {
            // Arrange: create a host over a fake engine.
            var engine = new FakeSapGuiEngine(FakeSapTree.NewApplication());
            using var host = TestHosts.NewHost(engine);

            // Act: run an operation that fails with E_ACCESSDENIED.
            var exception = Assert.ThrowsExactly<SapDriverException>(() => host.Invoke<int>(_ => throw new COMException("denied", unchecked((int)0x80070005))));

            // Assert: the failure is classified and the release still ran.
            Assert.AreEqual(SapErrorKinds.AccessDenied, exception.ErrorKind);
            Assert.AreEqual("Release", engine.Calls[^1]);
        }

        [TestMethod(DisplayName = "Verify that a disconnect resets the engine so the next operation attaches again.")]
        public void InvokeResetsEngineOnDisconnectTest()
        {
            // Arrange: create a host over a fake engine.
            var engine = new FakeSapGuiEngine(FakeSapTree.NewApplication());
            using var host = TestHosts.NewHost(engine);

            // Act: fail with RPC_E_DISCONNECTED, as when SAP Logon is closed.
            var exception = Assert.ThrowsExactly<SapDriverException>(() => host.Invoke<int>(_ => throw new COMException("gone", unchecked((int)0x80010108))));

            // Assert: the failure reports SAP GUI as not running and the engine was reset before the release.
            Assert.AreEqual(SapErrorKinds.SapGuiNotRunning, exception.ErrorKind);
            var expectedCalls = new[] { "GetApplication", "Reset", "Release" };
            Assert.AreSequenceEqual(expectedCalls, engine.Calls);
        }

        [TestMethod(DisplayName = "Verify that an attachment failure is surfaced unchanged as its classified SapDriverException.")]
        public void InvokeSurfacesAttachFailureTest()
        {
            // Arrange: configure the engine to fail attachment because scripting is disabled.
            var engine = new FakeSapGuiEngine(application: null)
            {
                AttachException = SapDriverException.NewSessionNotCreated(SapErrorKinds.ScriptingDisabled, "disabled")
            };
            using var host = TestHosts.NewHost(engine);

            // Act: run any operation.
            var exception = Assert.ThrowsExactly<SapDriverException>(() => host.Invoke(_ => 1));

            // Assert: the attachment failure keeps its classification.
            Assert.AreEqual(WebDriverErrorCodes.SessionNotCreated, exception.Error);
            Assert.AreEqual(SapErrorKinds.ScriptingDisabled, exception.ErrorKind);
        }

        [TestMethod(DisplayName = "Verify that non-COM failures propagate as server faults without classification.")]
        public void InvokePropagatesServerFaultTest()
        {
            // Arrange: create a host over a fake engine.
            var engine = new FakeSapGuiEngine(FakeSapTree.NewApplication());
            using var host = TestHosts.NewHost(engine);

            // Act and Assert: an ordinary bug keeps its exception type.
            Assert.ThrowsExactly<InvalidOperationException>(() => host.Invoke<int>(_ => throw new InvalidOperationException("bug")));
        }

        [TestMethod(DisplayName = "Verify that an operation exceeding the COM call timeout fails with the W3C timeout error.")]
        public void InvokeTimeoutTest()
        {
            // Arrange: create a host with a very short COM call timeout.
            var engine = new FakeSapGuiEngine(FakeSapTree.NewApplication());
            var settings = new SapDriverSettings { BusyWaitMilliseconds = 100, ComCallTimeoutMilliseconds = 100 };
            using var host = new SapScriptingHost(new StaComDispatcher(), engine, settings, NullLogger<SapScriptingHost>.Instance);

            // Act: run an operation that outlasts the timeout.
            var exception = Assert.ThrowsExactly<SapDriverException>(() => host.Invoke(_ =>
            {
                Thread.Sleep(500);
                return 1;
            }));

            // Assert: the timeout is reported with the W3C code and SAP kind.
            Assert.AreEqual(WebDriverErrorCodes.Timeout, exception.Error);
            Assert.AreEqual(SapErrorKinds.Timeout, exception.ErrorKind);
        }

        [TestMethod(DisplayName = "Verify that Dispose releases the engine on the dispatcher thread and later operations fail.")]
        public void DisposeReleasesEngineOnDispatcherTest()
        {
            // Arrange: create a host and capture the dispatcher thread through one operation.
            var engine = new FakeSapGuiEngine(FakeSapTree.NewApplication());
            var host = TestHosts.NewHost(engine);
            var dispatcherThreadId = host.Invoke(_ => Environment.CurrentManagedThreadId);

            // Act: dispose the host twice.
            host.Dispose();
            host.Dispose();

            // Assert: the engine was disposed once, on the same thread as its operations, and the host rejects new work.
            Assert.HasCount(1, engine.Calls.FindAll(i => i == "Dispose"));
            var expectedThreadIds = new[] { dispatcherThreadId };
            Assert.AreSequenceEqual(expectedThreadIds, engine.ThreadIds);
            Assert.ThrowsExactly<ObjectDisposedException>(() => host.Invoke(_ => 1));
        }
    }
}
