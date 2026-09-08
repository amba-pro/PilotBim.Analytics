using System;
using System.Threading;
using System.Threading.Tasks;
using PilotBim.Analytics.Data;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    public sealed class CallbackWaitSessionTests
    {
        [Fact]
        public void Wait_SignalBeforeWait_Completed()
        {
            using (var session = new CallbackWaitSession())
            {
                session.SignalCompleted();
                var result = session.Wait(TimeSpan.FromSeconds(1));
                Assert.Equal(CallbackWaitStatus.Completed, result.Status);
                Assert.True(result.Succeeded);
                Assert.Null(result.Error);
                Assert.True(session.ShouldAccept());
            }
        }

        [Fact]
        public void Wait_Timeout_Abandons_AndRejectsLateAccept()
        {
            using (var session = new CallbackWaitSession())
            {
                var result = session.Wait(TimeSpan.FromMilliseconds(1));
                Assert.Equal(CallbackWaitStatus.TimedOut, result.Status);
                Assert.True(session.IsAbandoned);
                Assert.False(session.ShouldAccept());

                // Late signal must not throw after abandon/timeout
                session.SignalCompleted();
                Assert.False(session.ShouldAccept());
            }
        }

        [Fact]
        public void Wait_SignalFailed_ReturnsFailedWithError()
        {
            using (var session = new CallbackWaitSession())
            {
                var ex = new InvalidOperationException("boom");
                session.SignalFailed(ex);
                var result = session.Wait(TimeSpan.FromSeconds(1));
                Assert.Equal(CallbackWaitStatus.Failed, result.Status);
                Assert.Same(ex, result.Error);
            }
        }

        [Fact]
        public void DoubleSignal_IsIdempotent()
        {
            using (var session = new CallbackWaitSession())
            {
                session.SignalCompleted();
                session.SignalCompleted();
                var result = session.Wait(TimeSpan.FromSeconds(1));
                Assert.Equal(CallbackWaitStatus.Completed, result.Status);
            }
        }

        [Fact]
        public void ConcurrentSignals_DoNotThrow()
        {
            using (var session = new CallbackWaitSession())
            {
                Parallel.For(0, 32, _ => session.SignalCompleted());
                var result = session.Wait(TimeSpan.FromSeconds(1));
                Assert.Equal(CallbackWaitStatus.Completed, result.Status);
            }
        }

        [Fact]
        public void Dispose_RejectsAccept_AndLateSignalSafe()
        {
            var session = new CallbackWaitSession();
            session.SignalCompleted();
            Assert.Equal(CallbackWaitStatus.Completed, session.Wait(TimeSpan.FromSeconds(1)).Status);
            session.Dispose();
            Assert.False(session.ShouldAccept());
            session.SignalCompleted();
            session.SignalFailed(new Exception("late"));
        }

        [Fact]
        public void Wait_CancelledToken_ReturnsCancelled()
        {
            using (var session = new CallbackWaitSession())
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();
                var result = session.Wait(TimeSpan.FromSeconds(1), cts.Token);
                Assert.Equal(CallbackWaitStatus.Cancelled, result.Status);
                Assert.False(session.ShouldAccept());
            }
        }

        [Fact]
        public void CallbackAfterTimeout_ShouldNotAccept_EvenIfSignaled()
        {
            using (var session = new CallbackWaitSession())
            {
                Assert.Equal(CallbackWaitStatus.TimedOut, session.Wait(TimeSpan.FromMilliseconds(1)).Status);

                Assert.False(session.ShouldAccept());
                session.SignalCompleted();
            }
        }

        [Fact]
        public void AsyncCallbackGuard_TrueUntilAbandoned()
        {
            var flag = 0;
            Assert.True(AsyncCallbackGuard.ShouldAccept(flag));
            AsyncCallbackGuard.Abandon(ref flag);
            Assert.False(AsyncCallbackGuard.ShouldAccept(flag));
        }
    }
}
