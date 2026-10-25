using System;

namespace Sap.DriverServer.Domain
{
    /// <summary>
    /// Serializes delegates onto one dedicated thread that owns every SAP GUI COM reference.
    /// </summary>
    /// <remarks>
    /// SAP GUI Scripting objects are apartment-threaded. Running every call on one STA thread keeps the RCWs in
    /// the apartment that created them and guarantees that two HTTP requests never drive SAP GUI concurrently.
    /// </remarks>
    public interface ISapComDispatcher : IDisposable
    {
        #region *** Methods      ***
        /// <summary>
        /// Runs an action on the dispatcher thread and waits for it to finish.
        /// </summary>
        /// <param name="operation">The action to run.</param>
        /// <param name="timeout">The maximum time to wait, including time spent queued behind earlier operations.</param>
        /// <exception cref="TimeoutException">The operation did not finish in time; it keeps running and its outcome is discarded.</exception>
        /// <exception cref="ObjectDisposedException">The dispatcher was shut down.</exception>
        void Invoke(Action operation, TimeSpan timeout);

        /// <summary>
        /// Runs a function on the dispatcher thread and returns its result.
        /// </summary>
        /// <typeparam name="T">The result type.</typeparam>
        /// <param name="operation">The function to run.</param>
        /// <param name="timeout">The maximum time to wait, including time spent queued behind earlier operations.</param>
        /// <returns>The function result.</returns>
        /// <exception cref="TimeoutException">The operation did not finish in time; it keeps running and its result is discarded.</exception>
        /// <exception cref="ObjectDisposedException">The dispatcher was shut down.</exception>
        T Invoke<T>(Func<T> operation, TimeSpan timeout);
        #endregion
    }
}
