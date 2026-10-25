using System;
using System.Collections.Generic;
using System.Threading;

using Sap.DriverServer.Domain;

namespace Sap.DriverServer.Domain.UnitTests.Support
{
    /// <summary>
    /// In-memory <see cref="ISapGuiEngine"/> that serves a fake application tree and records lifecycle calls and threads.
    /// </summary>
    public sealed class FakeSapGuiEngine : ISapGuiEngine
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="FakeSapGuiEngine"/> class.
        /// </summary>
        /// <param name="application">The application node returned by <see cref="GetApplication"/>.</param>
        public FakeSapGuiEngine(ISapGuiNode application)
        {
            // Keep the tree; tests mutate it to simulate SAP GUI state changes.
            Application = application;
        }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the application node, or null to simulate an attachment failure through <see cref="AttachException"/>.
        /// </summary>
        public ISapGuiNode Application { get; set; }

        /// <summary>
        /// Gets or sets the exception thrown by <see cref="GetApplication"/>, or null for a successful attachment.
        /// </summary>
        public Exception AttachException { get; set; }

        /// <summary>
        /// Gets the lifecycle calls in order: <c>GetApplication</c>, <c>Release</c>, <c>Reset</c>, <c>Dispose</c>.
        /// </summary>
        public List<string> Calls { get; } = [];

        /// <summary>
        /// Gets the managed thread identifiers on which engine members ran.
        /// </summary>
        public HashSet<int> ThreadIds { get; } = [];
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        public void Dispose()
        {
            // Record disposal and its thread.
            Record("Dispose");
        }

        /// <inheritdoc />
        public ISapGuiNode GetApplication()
        {
            // Record the call, then fail or attach as configured.
            Record("GetApplication");

            if (AttachException is not null)
            {
                throw AttachException;
            }

            return Application;
        }

        /// <inheritdoc />
        public void ReleaseOperationObjects()
        {
            // Record the per-operation release.
            Record("Release");
        }

        /// <inheritdoc />
        public void Reset()
        {
            // Record the reset after a disconnect.
            Record("Reset");
        }

        // Records a lifecycle call and the thread it ran on; lists are guarded because tests read them concurrently.
        private void Record(string call)
        {
            lock (Calls)
            {
                Calls.Add(call);
                ThreadIds.Add(Environment.CurrentManagedThreadId);
            }
        }
        #endregion
    }
}
