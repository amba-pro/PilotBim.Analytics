using PilotBim.Analytics.Discovery;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    public sealed class DocumentDiscoveryServiceTests
    {
        [Fact]
        public void NextSampleAction_NullObject_SkipsInsteadOfStopping()
        {
            // BUG (pre-fix): null → Stop (break), so later objects are never seen
            // EXPECTED: null → Skip (continue)
            Assert.Equal(
                DocumentDiscoveryService.SampleLoopAction.Skip,
                DocumentDiscoveryService.NextSampleAction(objIsNull: true, rowsCount: 0, max: 20));
        }

        [Fact]
        public void NextSampleAction_CapacityReached_Stops()
        {
            Assert.Equal(
                DocumentDiscoveryService.SampleLoopAction.Stop,
                DocumentDiscoveryService.NextSampleAction(objIsNull: false, rowsCount: 20, max: 20));
        }

        [Fact]
        public void NextSampleAction_NullDoesNotMatter_WhenCapacityReached()
        {
            Assert.Equal(
                DocumentDiscoveryService.SampleLoopAction.Stop,
                DocumentDiscoveryService.NextSampleAction(objIsNull: true, rowsCount: 20, max: 20));
        }

        [Fact]
        public void NextSampleAction_ValidObject_Processes()
        {
            Assert.Equal(
                DocumentDiscoveryService.SampleLoopAction.Process,
                DocumentDiscoveryService.NextSampleAction(objIsNull: false, rowsCount: 0, max: 20));
        }

        [Fact]
        public void NextSampleAction_Sequence_NullBetweenValids_AllowsSecondProcess()
        {
            var actions = new[]
            {
                DocumentDiscoveryService.NextSampleAction(false, 0, 20),
                DocumentDiscoveryService.NextSampleAction(true, 1, 20),
                DocumentDiscoveryService.NextSampleAction(false, 1, 20)
            };

            Assert.Equal(new[]
            {
                DocumentDiscoveryService.SampleLoopAction.Process,
                DocumentDiscoveryService.SampleLoopAction.Skip,
                DocumentDiscoveryService.SampleLoopAction.Process
            }, actions);
        }
    }
}
