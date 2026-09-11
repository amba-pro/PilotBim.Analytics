using System;
using System.Threading;
using PilotBim.Analytics.Services;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    public sealed class ScanSessionScopeTests
    {
        [Fact]
        public void Begin_ReturnsLiveNonCancelledToken()
        {
            using (var scope = new ScanSessionScope())
            {
                var session = scope.Begin();
                Assert.True(scope.IsActive);
                Assert.False(session.Token.IsCancellationRequested);
                Assert.True(session.Token.CanBeCanceled);
            }
        }

        [Fact]
        public void Cancel_CancelsActiveToken()
        {
            using (var scope = new ScanSessionScope())
            {
                var session = scope.Begin();
                scope.Cancel();
                Assert.True(session.Token.IsCancellationRequested);
            }
        }

        [Fact]
        public void SecondBegin_CancelsAndDisposesFirstToken()
        {
            using (var scope = new ScanSessionScope())
            {
                var first = scope.Begin();
                var second = scope.Begin();
                Assert.True(first.Token.IsCancellationRequested);
                Assert.False(second.Token.IsCancellationRequested);
                Assert.NotEqual(first.Version, second.Version);
            }
        }

        [Fact]
        public void Complete_DisposesActiveAndClearsIsActive()
        {
            var scope = new ScanSessionScope();
            var session = scope.Begin();
            Assert.True(scope.IsActive);
            scope.Complete(session);
            Assert.False(scope.IsActive);
            scope.Dispose();
        }

        [Fact]
        public void Cancel_AfterComplete_DoesNotThrow()
        {
            using (var scope = new ScanSessionScope())
            {
                var session = scope.Begin();
                scope.Complete(session);
                scope.Cancel();
            }
        }

        [Fact]
        public void Dispose_IsIdempotent()
        {
            var scope = new ScanSessionScope();
            scope.Begin();
            scope.Dispose();
            scope.Dispose();
            Assert.False(scope.IsActive);
        }

        [Fact]
        public void Dispose_WhileActive_CancelsFirst()
        {
            var scope = new ScanSessionScope();
            var session = scope.Begin();
            scope.Dispose();
            Assert.True(session.Token.IsCancellationRequested);
            Assert.False(scope.IsActive);
        }

        [Fact]
        public void Complete_OfSupersededSession_DoesNotAffectNewer()
        {
            using (var scope = new ScanSessionScope())
            {
                var oldSession = scope.Begin();
                var newSession = scope.Begin();
                scope.Complete(oldSession);
                Assert.True(scope.IsActive);
                Assert.False(newSession.Token.IsCancellationRequested);
                scope.Complete(newSession);
                Assert.False(scope.IsActive);
            }
        }

        [Fact]
        public void IsActive_Transitions_BeginTrue_CompleteFalse()
        {
            using (var scope = new ScanSessionScope())
            {
                Assert.False(scope.IsActive);
                var session = scope.Begin();
                Assert.True(scope.IsActive);
                scope.Complete(session);
                Assert.False(scope.IsActive);
            }
        }
    }
}
