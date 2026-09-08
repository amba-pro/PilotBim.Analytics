using PilotBim.Analytics.Discovery;
using PilotBim.Analytics.Models;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    public sealed class BimDiscoveryServiceTests
    {
        [Fact]
        public void ResolveCountCapabilityStatus_Success_IsAvailable_EvenWhenCountZero()
        {
            Assert.Equal(CapabilityStatus.Available, BimDiscoveryService.ResolveCountCapabilityStatus(timedOut: false, hadError: false));
        }

        [Fact]
        public void ResolveCountCapabilityStatus_Timeout_IsPartial_NotAvailable()
        {
            Assert.Equal(CapabilityStatus.Partial, BimDiscoveryService.ResolveCountCapabilityStatus(timedOut: true, hadError: false));
        }

        [Fact]
        public void ResolveCountCapabilityStatus_Error_IsError_NotAvailable()
        {
            Assert.Equal(CapabilityStatus.Error, BimDiscoveryService.ResolveCountCapabilityStatus(timedOut: false, hadError: true));
        }

        [Fact]
        public void ResolveCountCapabilityStatus_ErrorWinsOverTimeout()
        {
            Assert.Equal(CapabilityStatus.Error, BimDiscoveryService.ResolveCountCapabilityStatus(timedOut: true, hadError: true));
        }

        [Fact]
        public void SelectGlobalId_PrefersCanonicalOverReadable()
        {
            Assert.Equal("canon", BimDiscoveryService.SelectGlobalId("canon", "alt", "readable"));
            Assert.Equal("alt", BimDiscoveryService.SelectGlobalId(null, "alt", "readable"));
            Assert.Equal("readable", BimDiscoveryService.SelectGlobalId(null, null, "readable"));
            Assert.Null(BimDiscoveryService.SelectGlobalId(null, null, null));
            Assert.Equal("canon", BimDiscoveryService.SelectGlobalId("canon", null, "readable"));
        }
    }
}
