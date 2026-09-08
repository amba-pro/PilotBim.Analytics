using PilotBim.Analytics.Data;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    public sealed class AsyncCallbackGuardTests
    {
        [Fact]
        public void ShouldAccept_TrueUntilAbandoned()
        {
            var flag = 0;
            Assert.True(AsyncCallbackGuard.ShouldAccept(flag));
            AsyncCallbackGuard.Abandon(ref flag);
            Assert.False(AsyncCallbackGuard.ShouldAccept(flag));
        }
    }
}
