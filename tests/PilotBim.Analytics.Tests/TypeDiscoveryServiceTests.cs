using PilotBim.Analytics.Discovery;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    public sealed class TypeDiscoveryServiceTests
    {
        [Fact]
        public void TypeSortKey_NullType_ReturnsEmpty_DoesNotThrow()
        {
            Assert.Equal(string.Empty, TypeDiscoveryService.TypeSortKey(null));
        }
    }
}
