using PilotBim.Analytics.Discovery;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    public sealed class CreatorAggregationServiceTests
    {
        [Fact]
        public void ReduceBudget_TimeoutReturningZero_DoesNotConsumeRequestedTake()
        {
            // CURRENT BUG: deducts requestedTake (200) → 300
            // EXPECTED: deduct returnedCount (0) → 500
            Assert.Equal(500, CreatorAggregationService.ReduceBudget(500, requestedTake: 200, returnedCount: 0));
        }

        [Fact]
        public void ReduceBudget_PartialReturn_DeductsReturnedOnly()
        {
            Assert.Equal(400, CreatorAggregationService.ReduceBudget(500, requestedTake: 200, returnedCount: 100));
        }

        [Fact]
        public void ReduceBudget_FullReturn_DeductsAllReturned()
        {
            Assert.Equal(300, CreatorAggregationService.ReduceBudget(500, requestedTake: 200, returnedCount: 200));
        }

        [Fact]
        public void ReduceBudget_ReturnedExceedsRemaining_ClampsToZero()
        {
            Assert.Equal(0, CreatorAggregationService.ReduceBudget(50, requestedTake: 200, returnedCount: 80));
        }

        [Fact]
        public void ReduceBudget_NegativeReturned_TreatedAsZero()
        {
            Assert.Equal(500, CreatorAggregationService.ReduceBudget(500, requestedTake: 200, returnedCount: -1));
        }
    }
}
