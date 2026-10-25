using System;

using Microsoft.Extensions.Logging;

using Sap.DriverServer.Models;

namespace Sap.DriverServer.Domain
{
    /// <summary>
    /// Default <see cref="ISapScriptingHost"/> that pairs one <see cref="ISapComDispatcher"/> with one <see cref="ISapGuiEngine"/>.
    /// </summary>
    /// <remarks>
    /// The host owns both collaborators. Disposal first releases the engine on the dispatcher thread, where its
    /// COM references live, and then stops the dispatcher thread.
    /// </remarks>
    /// <param name="dispatcher">The STA dispatcher that runs every operation.</param>
    /// <param name="engine">The scripting engine accessed only from the dispatcher thread.</param>
    /// <param name="settings">The driver settings that provide the COM call timeout.</param>
    /// <param name="logger">The logger that records classified failures without SAP content.</param>
    public sealed class SapScriptingHost(
        ISapComDispatcher dispatcher,
        ISapGuiEngine engine,
        SapDriverSettings settings,
        ILogger<SapScriptingHost> logger) : ISapScriptingHost
    {
        #region *** Constants    ***
        // Bounds the shutdown-time engine release so a hung SAP GUI cannot block process exit.
        private static readonly TimeSpan DisposeTimeout = TimeSpan.FromSeconds(5);
        #endregion

        #region *** Fields       ***
        // Serializes every SAP call onto the STA thread that owns the engine's COM references.
        private readonly ISapComDispatcher _dispatcher = dispatcher;

        // Provides the application node; touched only inside dispatcher delegates.
        private readonly ISapGuiEngine _engine = engine;

        // Records classified failures; never receives field values or COM error text.
        private readonly ILogger<SapScriptingHost> _logger = logger;

        // Bounds each operation, including queue time, so a hung SAP GUI surfaces as a timeout error.
        private readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(settings.ComCallTimeoutMilliseconds);

        // Guards against double disposal when both DI and the application lifetime release the host.
        private bool _isDisposed;
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        public void Dispose()
        {
            // Skip repeated disposal so the dispatcher is not stopped twice.
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            try
            {
                // Release the engine's COM references on their owning thread before the thread stops.
                _dispatcher.Invoke(_engine.Dispose, DisposeTimeout);
            }
            catch (Exception exception)
            {
                // Continue shutting down; a failed release only leaks references held by a process that is exiting.
                _logger.LogWarning(exception, "SAP GUI Scripting engine release failed during shutdown.");
            }

            // Stop the dispatcher thread after the engine no longer needs it.
            _dispatcher.Dispose();
        }

        /// <inheritdoc />
        public void Invoke(Action<ISapGuiNode> operation)
        {
            // Route through the result-returning overload so both shapes share one failure and cleanup path.
            Invoke(application =>
            {
                operation(application);
                return true;
            });
        }

        /// <inheritdoc />
        public T Invoke<T>(Func<ISapGuiNode, T> operation)
        {
            // Require the operation before queueing work on the dispatcher thread.
            ArgumentNullException.ThrowIfNull(argument: operation);

            try
            {
                // Run the operation on the STA thread; failures are classified there, where the engine can be reset.
                return _dispatcher.Invoke(() => InvokeOnDispatcher(_engine, _logger, operation), _timeout);
            }
            catch (TimeoutException exception)
            {
                // Report the timeout with the W3C code; the operation itself continues on the dispatcher thread.
                _logger.LogWarning("SAP GUI Scripting operation exceeded {TimeoutMilliseconds} ms.", _timeout.TotalMilliseconds);
                throw SapErrorMapper.ConvertToDriverException(exception);
            }
        }

        // Runs one operation on the dispatcher thread and owns its failure classification and COM cleanup.
        // A disconnect resets the engine so the next request attaches to a restarted SAP GUI; per-operation
        // COM references are released in every outcome. Non-COM failures propagate unchanged as server faults.
        private static T InvokeOnDispatcher<T>(ISapGuiEngine engine, ILogger logger, Func<ISapGuiNode, T> operation)
        {
            try
            {
                // Attach lazily so the server starts even while SAP GUI is closed.
                var application = engine.GetApplication();

                return operation(application);
            }
            catch (Exception exception) when (exception is not SapDriverException)
            {
                // Leave non-COM failures to the generic server-fault path.
                var driverException = SapErrorMapper.ConvertToDriverException(exception);
                if (driverException is null)
                {
                    throw;
                }

                // Drop the cached application after a disconnect so the next call attaches again.
                if (driverException.ErrorKind == SapErrorKinds.SapGuiNotRunning)
                {
                    engine.Reset();
                }

                // Log the classification and HRESULT only; the COM message may quote SAP screen contents.
                var hresult = SapErrorMapper.FindComException(exception)?.HResult ?? exception.HResult;
                logger.LogWarning(
                    "SAP GUI Scripting call failed: {ErrorKind}, HRESULT 0x{HResult:X8}.",
                    driverException.ErrorKind,
                    hresult);

                throw driverException;
            }
            finally
            {
                // Release every COM reference the operation created; the application object stays cached.
                engine.ReleaseOperationObjects();
            }
        }
        #endregion
    }
}
