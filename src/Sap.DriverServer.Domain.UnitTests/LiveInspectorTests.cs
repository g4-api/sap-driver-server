using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sap.DriverServer.Inspector.Models;
using Sap.DriverServer.Inspector.Services;

namespace Sap.DriverServer.Domain.UnitTests
{
    [TestClass]
    [OSCondition(OperatingSystems.Windows)]
    [TestCategory("LiveInspector")]
    [TestCategory("UnitTest")]
    public class LiveInspectorTests
    {
        [TestMethod(DisplayName = "Verify that samples arriving during a slow hit test are coalesced to the newest one.")]
        public async Task UpdateCoalescesSamplesTestAsync()
        {
            // Arrange: a resolver whose first hit test stays pending until released.
            var resolved = new List<InspectorPoint>();
            var gate = new TaskCompletionSource<HitResult>();
            var live = new LiveInspector(point =>
            {
                resolved.Add(point);
                return resolved.Count == 1 ? gate.Task : Task.FromResult(NewHit(point.X));
            });
            var first = NewPoint(1);

            // Act: start the first hit test, send three more samples while it runs, then release it.
            var running = live.UpdateAsync(first);
            await live.UpdateAsync(NewPoint(2));
            await live.UpdateAsync(NewPoint(3));
            await live.UpdateAsync(NewPoint(4));
            gate.SetResult(NewHit(1));
            await running;

            // Assert: only the first and the newest samples were resolved, and the newest hit is current.
            Assert.AreSequenceEqual(new[] { first, NewPoint(4) }, resolved);
            Assert.AreEqual("id4", live.Current.Id);
        }

        [TestMethod(DisplayName = "Verify that unchanged positions, paused mode, and the inspector's own windows are ignored.")]
        public async Task UpdateIgnoresSamplesTestAsync()
        {
            // Arrange: count hit tests.
            var calls = 0;
            var live = new LiveInspector(point =>
            {
                calls++;
                return Task.FromResult(NewHit(point.X));
            });

            // Act: repeat a position, send a null sample, pause, then resume with a reset at the same position.
            await live.UpdateAsync(NewPoint(1));
            await live.UpdateAsync(NewPoint(1));
            await live.UpdateAsync(point: null);
            live.IsPaused = true;
            await live.UpdateAsync(NewPoint(2));
            live.IsPaused = false;
            live.Reset();
            await live.UpdateAsync(NewPoint(1));

            // Assert: only the first sample and the post-reset sample were resolved.
            Assert.AreEqual(2, calls);
        }

        [TestMethod(DisplayName = "Verify that HitChanged fires only for new targets, misses keep the selection, and failures do not stop live mode.")]
        public async Task HitChangedTestAsync()
        {
            // Arrange: positions 1 and 2 hit the same control, 3 is a miss, 4 fails, and 5 hits another control.
            var changes = new List<string>();
            var failures = 0;
            var live = new LiveInspector(point => point.X switch
            {
                3 => Task.FromResult<HitResult>(null),
                4 => throw new InvalidOperationException("SAP busy"),
                5 => Task.FromResult(NewHit(5)),
                _ => Task.FromResult(NewHit(1))
            });
            live.HitChanged += (_, hit) => changes.Add(hit.Id);
            live.ResolveFailed += (_, _) => failures++;

            // Act: move through every position.
            for (var x = 1; x <= 5; x++)
            {
                await live.UpdateAsync(NewPoint(x));
            }

            // Assert: one change per distinct target, and the failure was reported without stopping updates.
            var expectedChanges = new[] { "id1", "id5" };
            Assert.AreSequenceEqual(expectedChanges, changes);
            Assert.AreEqual(1, failures);
        }

        // Builds a hit whose identifier encodes the sample position.
        private static HitResult NewHit(int x)
        {
            return new HitResult { Id = $"id{x}" };
        }

        // Builds a sample over a fixed window at the given horizontal position.
        private static InspectorPoint NewPoint(int x)
        {
            return new InspectorPoint(x, 0, WindowHandle: 1);
        }
    }
}
