using System;
using System.Threading.Tasks;

using Sap.DriverServer.Inspector.Models;

namespace Sap.DriverServer.Inspector.Services
{
    /// <summary>
    /// Turns cursor samples into hover selections, with pause/resume and stale-sample coalescing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The UI samples the cursor on a timer and calls <see cref="UpdateAsync"/>. Unchanged positions are ignored.
    /// While a hit test runs, newer samples replace each other, and only the latest one is resolved when the running
    /// test finishes, so a slow SAP GUI never builds a backlog of outdated lookups.
    /// </para>
    /// <para>
    /// <see cref="HitChanged"/> is raised only when the target (identifier and inner object) changes and live mode is
    /// not paused. A miss, such as hovering over a non-SAP window, keeps the previous selection, as Accessibility
    /// Insights does. The caller awaits on the UI thread; continuations resume there, so state needs no locking.
    /// </para>
    /// </remarks>
    /// <param name="resolveAsync">Resolves one cursor sample to a hit, or null when nothing scriptable is under it.</param>
    public sealed class LiveInspector(Func<InspectorPoint, Task<HitResult>> resolveAsync)
    {
        #region *** Fields       ***
        // Resolves cursor samples; supplied by the view model as a background call to the inspector service.
        private readonly Func<InspectorPoint, Task<HitResult>> _resolveAsync = resolveAsync;

        // Records whether a hit test is running, so newer samples are queued instead of started.
        private bool _isResolving;

        // Holds the last sample accepted, so unchanged positions are ignored.
        private InspectorPoint _lastPoint;

        // Holds the newest sample that arrived while a hit test was running; older queued samples are discarded.
        private InspectorPoint _pendingPoint;

        /// <summary>
        /// Occurs when the hovered target changes while live mode is active.
        /// </summary>
        public event EventHandler<HitResult> HitChanged;

        /// <summary>
        /// Occurs when a hit test fails; live mode continues with the next sample.
        /// </summary>
        public event EventHandler<Exception> ResolveFailed;
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets the current hovered target, or null before the first hit.
        /// </summary>
        public HitResult Current { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether live updates are paused (Shift+F5).
        /// </summary>
        public bool IsPaused { get; set; }
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Forgets the last sample and target, so the next sample is resolved even at the same position.
        /// </summary>
        public void Reset()
        {
            // Clear the de-duplication state, typically after resuming live mode.
            _lastPoint = null;
            Current = null;
        }

        /// <summary>
        /// Processes one cursor sample.
        /// </summary>
        /// <param name="point">The sample, or null when the cursor is over the inspector itself.</param>
        /// <returns>A task that completes when this call has no more queued samples to resolve.</returns>
        public async Task UpdateAsync(InspectorPoint point)
        {
            // Ignore samples while paused, over the inspector, or at an unchanged position.
            if (IsPaused || point is null || point == _lastPoint)
            {
                return;
            }

            _lastPoint = point;

            // Queue the sample when a hit test is already running; the running loop picks up the newest one.
            if (_isResolving)
            {
                _pendingPoint = point;
                return;
            }

            _isResolving = true;
            try
            {
                // Resolve samples until no newer one arrived during the last hit test.
                var next = point;
                while (next is not null)
                {
                    _pendingPoint = null;
                    var hit = await ResolveAsync(next).ConfigureAwait(true);
                    RaiseIfChanged(hit);
                    next = _pendingPoint;
                }
            }
            finally
            {
                // Allow the next sample to start a hit test.
                _isResolving = false;
            }
        }

        // Raises HitChanged when live mode is active and the hit targets a different object than the current one.
        private void RaiseIfChanged(HitResult hit)
        {
            // Keep the previous selection on a miss, and ignore results that arrive after pausing.
            if (IsPaused || hit is null || hit.TestSameTarget(Current))
            {
                return;
            }

            Current = hit;
            HitChanged?.Invoke(this, hit);
        }

        // Runs one hit test and reports a failure as a miss, so one SAP GUI error does not stop live mode.
        private async Task<HitResult> ResolveAsync(InspectorPoint point)
        {
            try
            {
                // Resume on the caller's context (the UI thread) so state updates stay single-threaded.
                return await _resolveAsync(point).ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                ResolveFailed?.Invoke(this, exception);
                return null;
            }
        }
        #endregion
    }
}
