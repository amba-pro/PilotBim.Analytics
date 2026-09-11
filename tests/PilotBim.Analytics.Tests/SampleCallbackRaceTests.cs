using System;
using System.Threading;
using System.Threading.Tasks;
using PilotBim.Analytics.Data;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    /// <summary>
    /// Characterizes the TD-25 invariant used by ObjectSamplingCoordinator.ApplySampledObjects:
    /// after timeout/abandon, further mutations must stop (re-check ShouldAccept in the loop).
    /// </summary>
    public sealed class SampleCallbackRaceTests
    {
        [Fact]
        public void AcceptedCallback_MutatesResult()
        {
            using (var session = new CallbackWaitSession())
            {
                var mutations = 0;
                MutateWhileAccepted(session, ref mutations, target: 5);
                Assert.Equal(5, mutations);
                session.SignalCompleted();
                Assert.Equal(CallbackWaitStatus.Completed, session.Wait(TimeSpan.FromSeconds(1)).Status);
            }
        }

        [Fact]
        public void TimeoutThenLateCallback_DoesNotMutate()
        {
            using (var session = new CallbackWaitSession())
            {
                var wait = session.Wait(TimeSpan.FromMilliseconds(1));
                Assert.Equal(CallbackWaitStatus.TimedOut, wait.Status);
                Assert.False(session.ShouldAccept());

                var mutations = 0;
                MutateWhileAccepted(session, ref mutations, target: 10);
                Assert.Equal(0, mutations);
            }
        }

        [Fact]
        public void DisposeThenCallback_DoesNotMutate()
        {
            var session = new CallbackWaitSession();
            session.Dispose();
            Assert.False(session.ShouldAccept());

            var mutations = 0;
            MutateWhileAccepted(session, ref mutations, target: 10);
            Assert.Equal(0, mutations);
        }

        [Fact]
        public async Task AbandonMidMutation_StopsFurtherWrites()
        {
            using (var session = new CallbackWaitSession())
            {
                var mutations = 0;
                Assert.True(session.ShouldAccept());

                var abandoner = Task.Run(() =>
                {
                    Thread.Sleep(30);
                    session.Abandon();
                });

                MutateWhileAccepted(session, ref mutations, target: 500, pauseMs: 2);
                await abandoner;

                Assert.True(mutations < 500, "expected abandon to cut short the mutation loop");
                Assert.False(session.ShouldAccept());
            }
        }

        [Fact]
        public void DoubleEntry_AfterAbandon_SecondCallDoesNotMutate()
        {
            using (var session = new CallbackWaitSession())
            {
                var mutations = 0;
                MutateWhileAccepted(session, ref mutations, target: 3);
                Assert.Equal(3, mutations);

                session.Abandon();
                MutateWhileAccepted(session, ref mutations, target: 3);
                Assert.Equal(3, mutations);
            }
        }

        /// <summary>
        /// Mirrors ApplySampledObjects: entry check + per-item re-check before each write.
        /// </summary>
        private static void MutateWhileAccepted(CallbackWaitSession session, ref int mutations, int target, int pauseMs = 0)
        {
            if (!session.ShouldAccept())
                return;

            for (var i = 0; i < target; i++)
            {
                if (!session.ShouldAccept())
                    return;
                mutations++;
                if (pauseMs > 0)
                    Thread.Sleep(pauseMs);
            }
        }
    }
}
