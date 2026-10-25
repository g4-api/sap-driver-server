using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

using Sap.DriverServer.Domain;

namespace Sap.DriverServer.Scripting
{
    /// <summary>
    /// Runs queued delegates one at a time on a dedicated single-threaded apartment (STA) thread.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SAP GUI Scripting objects are apartment-threaded COM objects hosted by <c>saplogon.exe</c>. Creating and
    /// calling them from one STA thread keeps every RCW in its owning apartment and serializes all SAP access,
    /// so concurrent HTTP requests never interleave calls into the same SAP GUI session.
    /// </para>
    /// <para>
    /// The dispatcher does not pump window messages between work items. That is sufficient because the driver
    /// makes outgoing calls only and does not subscribe to SAP GUI events.
    /// </para>
    /// <para>
    /// Disposal stops accepting work, lets already queued work finish, and waits a bounded time for the thread
    /// to exit. A thread stuck in a hung COM call is abandoned; it is a background thread and ends with the process.
    /// </para>
    /// </remarks>
    public sealed class StaComDispatcher : ISapComDispatcher
    {
        #region *** Constants    ***
        // Bounds how long disposal waits for queued work to drain before abandoning the thread.
        private static readonly TimeSpan JoinTimeout = TimeSpan.FromSeconds(5);
        #endregion

        #region *** Fields       ***
        // Holds pending work items in submission order; completing it lets the thread drain and exit.
        private readonly BlockingCollection<Action> _queue = [];

        // Owns every COM reference created by queued work for the lifetime of the dispatcher.
        private readonly Thread _thread;

        // Records disposal so late callers receive ObjectDisposedException instead of a lost work item.
        private int _isDisposed;
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="StaComDispatcher"/> class with the default thread name.
        /// </summary>
        public StaComDispatcher()
            : this(threadName: "SapComDispatcher")
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="StaComDispatcher"/> class and starts its STA thread.
        /// </summary>
        /// <param name="threadName">The thread name shown in debuggers and dumps.</param>
        public StaComDispatcher(string threadName)
        {
            // Start a background STA thread so a hung SAP call never keeps the process alive.
            _thread = new Thread(RunQueue)
            {
                IsBackground = true,
                Name = threadName
            };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
        }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets the managed thread identifier of the dispatcher thread.
        /// </summary>
        public int ThreadId => _thread.ManagedThreadId;
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        public void Dispose()
        {
            // Allow only the first caller to stop the queue.
            if (Interlocked.Exchange(ref _isDisposed, 1) == 1)
            {
                return;
            }

            // Stop accepting work; items already queued still run so their callers receive an outcome.
            _queue.CompleteAdding();

            // Wait a bounded time for the drain; the queue is disposed only when no thread can still read it.
            var isJoined = _thread == Thread.CurrentThread || _thread.Join(JoinTimeout);
            if (isJoined)
            {
                _queue.Dispose();
            }
        }

        /// <inheritdoc />
        public void Invoke(Action operation, TimeSpan timeout)
        {
            // Require the action before adapting it to the result-returning overload.
            ArgumentNullException.ThrowIfNull(argument: operation);

            // Route through the complete overload so both shapes share queueing, timeout, and failure handling.
            Invoke(() =>
            {
                operation();
                return true;
            }, timeout);
        }

        /// <inheritdoc />
        public T Invoke<T>(Func<T> operation, TimeSpan timeout)
        {
            // Require the function and reject work after shutdown.
            ArgumentNullException.ThrowIfNull(argument: operation);
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) == 1, this);

            // Run nested calls inline; queueing them deadlocks the thread waiting on itself.
            if (Environment.CurrentManagedThreadId == _thread.ManagedThreadId)
            {
                return operation();
            }

            // Queue the work with a completion source that carries its result or exception back to the caller.
            var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            try
            {
                _queue.Add(() => InvokeWorkItem(operation, completion));
            }
            catch (InvalidOperationException exception)
            {
                // Report a dispose race as disposal rather than as a queue implementation detail.
                throw new ObjectDisposedException(nameof(StaComDispatcher), exception);
            }

            try
            {
                // Block the request thread until the STA thread finishes; the original exception is rethrown unwrapped.
                return completion.Task.WaitAsync(timeout).GetAwaiter().GetResult();
            }
            catch (TimeoutException exception)
            {
                // Name the budget in the message; the work item keeps running and its outcome is discarded.
                var message = $"The SAP GUI Scripting operation did not complete within {timeout.TotalMilliseconds} ms.";
                throw new TimeoutException(message, exception);
            }
        }

        // Executes one work item on the STA thread and completes its caller's task.
        // Every exception is captured into the task, so a failing item never terminates the dispatcher thread.
        private static void InvokeWorkItem<T>(Func<T> operation, TaskCompletionSource<T> completion)
        {
            try
            {
                // Publish the result to the waiting caller.
                completion.TrySetResult(operation());
            }
            catch (Exception exception)
            {
                // Publish the failure to the waiting caller, preserving the original exception type.
                completion.TrySetException(exception);
            }
        }

        // Thread body: runs work items in submission order until the queue is completed and drained.
        // Work items capture their own exceptions, so the loop ends only through CompleteAdding.
        private void RunQueue()
        {
            // Drain the queue sequentially; GetConsumingEnumerable blocks while the queue is empty.
            foreach (var workItem in _queue.GetConsumingEnumerable())
            {
                workItem();
            }
        }
        #endregion
    }
}
