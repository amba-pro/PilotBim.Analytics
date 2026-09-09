using System.Collections.Generic;
using System.Threading;
using PilotBim.Analytics.Models;
using PilotBim.Analytics.Services;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    /// <summary>
    /// Stage 6.3 regression: sample buffer lifecycle across repeated sampling / report assignment.
    /// </summary>
    public sealed class SampleBufferLifecycleTests
    {
        [Fact]
        public void SampleAllTypes_SecondCycle_DoesNotRetainPriorBufferEntries()
        {
            var history = new List<Ascon.Pilot.SDK.IDataObject> { null };
            var system = new List<Ascon.Pilot.SDK.IDataObject> { null };
            var documents = new List<DocumentSampleRow>
            {
                new DocumentSampleRow { DisplayName = "stale-from-run-1" }
            };

            var coordinator = new ObjectSamplingCoordinator(null, null, history, system, documents);
            var report = new ProjectInventoryReport
            {
                Types = new List<TypeInventoryRecord>()
            };

            // Empty Types: no SDK calls; production still enters SampleAllTypes at each Run.
            // Correct lifecycle: buffers must be reset at the start of a sampling cycle.
            coordinator.SampleAllTypes(report, CancellationToken.None, _ => { }, null);

            Assert.Empty(history);
            Assert.Empty(system);
            Assert.Empty(documents);
        }

        [Fact]
        public void PublishDocumentSamples_MustNotAliasMutableWorkingBuffer()
        {
            var workingBuffer = new List<DocumentSampleRow>
            {
                new DocumentSampleRow { DisplayName = "from-run-1" }
            };

            var published = InventoryService.PublishDocumentSamples(workingBuffer);
            workingBuffer.Add(new DocumentSampleRow { DisplayName = "mutated-after-assign" });

            Assert.Single(published);
            Assert.Equal("from-run-1", published[0].DisplayName);
            Assert.False(ReferenceEquals(workingBuffer, published));
        }
    }
}
