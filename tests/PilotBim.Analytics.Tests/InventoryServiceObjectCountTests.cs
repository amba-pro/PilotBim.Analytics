using PilotBim.Analytics.Services;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    public sealed class InventoryServiceObjectCountTests
    {
        [Fact]
        public void ResolveObjectCount_PreservesLongTotal_BeyondIntMax()
        {
            long total = (long)int.MaxValue + 1000L;
            Assert.Equal(total, InventoryService.ResolveObjectCount(total, sampleCount: 10));
        }

        [Fact]
        public void ResolveObjectCount_NegativeTotal_UsesSampleCount()
        {
            Assert.Equal(7, InventoryService.ResolveObjectCount(-1, sampleCount: 7));
        }

        [Fact]
        public void ResolveObjectCount_ZeroTotal_IsZero()
        {
            Assert.Equal(0, InventoryService.ResolveObjectCount(0, sampleCount: 5));
        }
    }
}
