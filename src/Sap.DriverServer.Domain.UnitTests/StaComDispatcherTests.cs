using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sap.DriverServer.Scripting;

namespace Sap.DriverServer.Domain.UnitTests
{
    [TestClass]
    [OSCondition(OperatingSystems.Windows)]
    [TestCategory("StaComDispatcher")]
    [TestCategory("UnitTest")]
    public class StaComDispatcherTests
    {
        // Gives each test generous time, because CI machines can be slow to schedule the STA thread.
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Gets or sets the MSTest context, whose cancellation token aborts waits when the run is cancelled.
        /// </summary>
        public TestContext TestContext { get; set; }

        [TestMethod(DisplayName = "Verify that Invoke runs the operation on one dedicated STA thread that is not the caller's thread.")]
        public void InvokeRunsOnStaThreadTest()
        {
            // Arrange: start a dispatcher and capture the calling thread.
            using var dispatcher = new StaComDispatcher();
            var callerThreadId = Environment.CurrentManagedThreadId;

            // Act: read the apartment state and thread identifier from inside two operations.
            var first = dispatcher.Invoke(() => (Thread.CurrentThread.GetApartmentState(), Environment.CurrentManagedThreadId), Timeout);
            var second = dispatcher.Invoke(() => Environment.CurrentManagedThreadId, Timeout);

            // Assert: both operations ran on the same STA thread, which differs from the caller.
            Assert.AreEqual(ApartmentState.STA, first.Item1);
            Assert.AreEqual(dispatcher.ThreadId, first.Item2);
            Assert.AreEqual(first.Item2, second);
            Assert.AreNotEqual(callerThreadId, first.Item2);
        }

        [TestMethod(DisplayName = "Verify that operations submitted concurrently run one at a time and never overlap.")]
        public void InvokeSerializesConcurrentCallsTest()
        {
            // Arrange: count concurrently running operations from many caller threads.
            using var dispatcher = new StaComDispatcher();
            var running = 0;
            var maximum = 0;

            // Act: submit 50 operations in parallel, each sleeping briefly while marked as running.
            Parallel.For(0, 50, new ParallelOptions { CancellationToken = TestContext.CancellationToken }, _ => dispatcher.Invoke(() =>
            {
                var current = Interlocked.Increment(ref running);
                InterlockedMax(ref maximum, current);
                Thread.Sleep(2);
                Interlocked.Decrement(ref running);
            }, Timeout));

            // Assert: no two operations overlapped.
            Assert.AreEqual(1, maximum);
        }

        [TestMethod(DisplayName = "Verify that operations from one caller run in submission order.")]
        public void InvokePreservesOrderTest()
        {
            // Arrange: record execution order from queued operations.
            using var dispatcher = new StaComDispatcher();
            var order = new ConcurrentQueue<int>();

            // Act: submit numbered operations sequentially.
            for (var index = 0; index < 20; index++)
            {
                var value = index;
                dispatcher.Invoke(() => order.Enqueue(value), Timeout);
            }

            // Assert: the recorded order matches the submission order.
            Assert.AreSequenceEqual(Enumerable.Range(0, 20), order);
        }

        [TestMethod(DisplayName = "Verify that an exception thrown on the STA thread reaches the caller unwrapped and the thread survives.")]
        public void InvokePropagatesExceptionTest()
        {
            // Arrange: start a dispatcher.
            using var dispatcher = new StaComDispatcher();

            // Act: run a failing operation, then a succeeding one.
            var exception = Assert.ThrowsExactly<InvalidOperationException>(() => dispatcher.Invoke<int>(() => throw new InvalidOperationException("boom"), Timeout));
            var result = dispatcher.Invoke(() => 42, Timeout);

            // Assert: the original exception type and message arrive, and the dispatcher keeps working.
            Assert.AreEqual("boom", exception.Message);
            Assert.AreEqual(42, result);
        }

        [TestMethod(DisplayName = "Verify that Invoke throws TimeoutException when the operation exceeds its budget, and later work still runs.")]
        public void InvokeTimesOutTest()
        {
            // Arrange: start a dispatcher and a gate that holds the first operation.
            using var dispatcher = new StaComDispatcher();
            using var gate = new ManualResetEventSlim(initialState: false);

            // Act: submit an operation that blocks past a short timeout, then release it.
            var exception = Assert.ThrowsExactly<TimeoutException>(() => dispatcher.Invoke(() => gate.Wait(Timeout, TestContext.CancellationToken), TimeSpan.FromMilliseconds(100)));
            gate.Set();
            var result = dispatcher.Invoke(() => "after", Timeout);

            // Assert: the timeout names its budget, and the next operation runs once the blocked one finishes.
            Assert.Contains("100 ms", exception.Message);
            Assert.AreEqual("after", result);
        }

        [TestMethod(DisplayName = "Verify that a nested Invoke from the STA thread runs inline instead of deadlocking.")]
        public void InvokeNestedRunsInlineTest()
        {
            // Arrange: start a dispatcher.
            using var dispatcher = new StaComDispatcher();

            // Act: invoke the dispatcher from inside one of its own operations.
            var result = dispatcher.Invoke(() => dispatcher.Invoke(() => Environment.CurrentManagedThreadId, Timeout), Timeout);

            // Assert: the nested operation ran on the dispatcher thread.
            Assert.AreEqual(dispatcher.ThreadId, result);
        }

        [TestMethod(DisplayName = "Verify that Dispose drains queued work and that later Invoke calls throw ObjectDisposedException.")]
        public void DisposeDrainsAndRejectsTest()
        {
            // Arrange: queue a slow operation from another thread so it is pending when Dispose runs.
            var dispatcher = new StaComDispatcher();
            var isCompleted = false;
            var pending = Task.Run(() => dispatcher.Invoke(() =>
            {
                Thread.Sleep(200);
                isCompleted = true;
            }, Timeout), TestContext.CancellationToken);
            Thread.Sleep(50);

            // Act: dispose while the operation runs, then try to submit more work.
            dispatcher.Dispose();
            pending.Wait(Timeout, TestContext.CancellationToken);

            // Assert: the queued work finished and new work is rejected.
            Assert.IsTrue(isCompleted);
            Assert.ThrowsExactly<ObjectDisposedException>(() => dispatcher.Invoke(() => 1, Timeout));

            // Act: dispose again.
            dispatcher.Dispose();
        }

        // Raises a shared maximum to a candidate value atomically.
        private static void InterlockedMax(ref int target, int candidate)
        {
            int current;
            do
            {
                current = Volatile.Read(ref target);
                if (candidate <= current)
                {
                    return;
                }
            }
            while (Interlocked.CompareExchange(ref target, candidate, current) != current);
        }
    }
}
