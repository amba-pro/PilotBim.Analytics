using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Ascon.Pilot.SDK;
using PilotBim.Analytics.Data;
using PilotBim.Analytics.Diagnostics;
using PilotBim.Analytics.Discovery;
using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.Services
{
    /// <summary>
    /// Type/object sampling orchestration extracted from InventoryService.
    /// Sample buffers are owned by the caller (InventoryService) and mutated in place —
    /// Stage 6.2 does not clear or copy them (sticky-buffer semantics preserved).
    /// </summary>
    internal sealed class ObjectSamplingCoordinator
    {
        private readonly IObjectsRepository _repository;
        private readonly ISearchService _search;
        private readonly List<IDataObject> _historySampleBuffer;
        private readonly List<IDataObject> _systemFieldSampleBuffer;
        private readonly List<DocumentSampleRow> _documentSampleBuffer;

        public ObjectSamplingCoordinator(
            IObjectsRepository repository,
            ISearchService search,
            List<IDataObject> historySampleBuffer,
            List<IDataObject> systemFieldSampleBuffer,
            List<DocumentSampleRow> documentSampleBuffer)
        {
            _repository = repository;
            _search = search;
            _historySampleBuffer = historySampleBuffer;
            _systemFieldSampleBuffer = systemFieldSampleBuffer;
            _documentSampleBuffer = documentSampleBuffer;
        }

        public void SampleAllTypes(
            ProjectInventoryReport report,
            CancellationToken token,
            Action<string> progress,
            HierarchyWalkResult walk)
        {
            // Each Run/sampling cycle must start clean — do not retain prior Run samples.
            _historySampleBuffer.Clear();
            _systemFieldSampleBuffer.Clear();
            _documentSampleBuffer.Clear();

            var attrService = new AttributeDiscoveryService();
            var scanner = new PilotObjectScanner(_repository, _search);
            var sampler = new PilotObjectSampler(scanner);
            var docService = new DocumentDiscoveryService();
            var typeIndex = 0;
            bool useWalk = _search == null && walk != null && string.IsNullOrEmpty(walk.Error);

            foreach (var typeRecord in report.Types)
            {
                if (token.IsCancellationRequested)
                {
                    report.Cancelled = true;
                    break;
                }

                typeIndex++;
                if (typeIndex % 10 == 0 || typeIndex == 1)
                    progress("Sampling type " + typeIndex + "/" + report.Types.Count + ": " + typeRecord.Title);

                IType type = null;
                try
                {
                    type = _repository.GetType(typeRecord.TypeId);
                }
                catch (Exception ex)
                {
                    typeRecord.Status = CapabilityStatus.Error;
                    typeRecord.Warnings = ex.Message;
                    continue;
                }

                typeRecord.Attributes = attrService.BuildAttributeShells(type);

                if (useWalk)
                {
                    ApplyWalkBucket(report, typeRecord, walk, attrService, docService);
                    continue;
                }

                if (_search == null)
                {
                    typeRecord.ObjectCount = -1;
                    typeRecord.ObjectCountIsEstimate = true;
                    typeRecord.Status = CapabilityStatus.Partial;
                    typeRecord.Warnings = "Search unavailable; hierarchy walk failed";
                    foreach (var a in typeRecord.Attributes)
                        attrService.FinalizeAttribute(a);
                    continue;
                }

                using (var session = new CallbackWaitSession())
                {
                    Exception sampleError = null;
                    sampler.SampleType(typeRecord.TypeId, report.SampleLimit, (objects, total) =>
                    {
                        try
                        {
                            if (!session.ShouldAccept())
                                return;
                            ApplySampledObjects(report, typeRecord, objects, total, attrService, docService, estimate: total < 0);
                        }
                        catch (Exception ex)
                        {
                            sampleError = ex;
                            typeRecord.Status = CapabilityStatus.Error;
                            typeRecord.Warnings = ex.Message;
                        }
                        finally
                        {
                            session.SignalCompleted();
                        }
                    }, ex =>
                    {
                        sampleError = ex;
                        session.SignalFailed(ex);
                    });

                    var wait = session.Wait(TimeSpan.FromSeconds(30));
                    if (wait.Status == CallbackWaitStatus.TimedOut)
                    {
                        typeRecord.Status = CapabilityStatus.Partial;
                        typeRecord.Warnings = (typeRecord.Warnings + " sample timeout").Trim();
                        report.Warnings.Add("Timeout sampling type " + typeRecord.Name);
                    }

                    if (sampleError != null)
                    {
                        AnalyticsLogger.Warning("sample-type", typeRecord.Name + " " + sampleError.Message);
                        report.Diagnostics.Add(AnalyticsLogger.CreateEntry("WARNING", "sample-type", typeRecord.Name + ": " + sampleError.Message));
                    }
                    else if (wait.Status == CallbackWaitStatus.Failed && wait.Error != null)
                    {
                        AnalyticsLogger.Warning("sample-type", typeRecord.Name + " " + wait.Error.Message);
                        report.Diagnostics.Add(AnalyticsLogger.CreateEntry("WARNING", "sample-type", typeRecord.Name + ": " + wait.Error.Message));
                    }
                }
            }

            report.ZoneResults.Add(Zone("Attributes", CapabilityStatus.Pass, "profiled types=" + report.Types.Count(t => t.SampledCount > 0)));

            if (_search != null && !token.IsCancellationRequested)
            {
                progress("Creator / created-date aggregation...");
                var scanMode = (ScanMode)report.SampleLimit;
                new CreatorAggregationService().Aggregate(report, scanner, scanMode, token, progress);
            }
        }

        private void ApplyWalkBucket(
            ProjectInventoryReport report,
            TypeInventoryRecord typeRecord,
            HierarchyWalkResult walk,
            AttributeDiscoveryService attrService,
            DocumentDiscoveryService docService)
        {
            TypeBucket bucket;
            if (!walk.ByType.TryGetValue(typeRecord.TypeId, out bucket) || bucket == null)
            {
                typeRecord.ObjectCount = 0;
                typeRecord.ObjectCountIsEstimate = walk.Truncated;
                typeRecord.SampledCount = 0;
                typeRecord.Status = CapabilityStatus.Available;
                typeRecord.Warnings = walk.Truncated ? "Not seen in hierarchy walk (may exist outside visited budget)" : null;
                foreach (var a in typeRecord.Attributes)
                    attrService.FinalizeAttribute(a);
                return;
            }

            ApplySampledObjects(
                report,
                typeRecord,
                bucket.Samples,
                bucket.Count,
                attrService,
                docService,
                estimate: walk.Truncated);
            if (walk.Truncated)
                typeRecord.Warnings = string.IsNullOrEmpty(typeRecord.Warnings)
                    ? "Hierarchy walk truncated; count is lower bound"
                    : typeRecord.Warnings;
        }

        private void ApplySampledObjects(
            ProjectInventoryReport report,
            TypeInventoryRecord typeRecord,
            IReadOnlyList<IDataObject> objects,
            long total,
            AttributeDiscoveryService attrService,
            DocumentDiscoveryService docService,
            bool estimate)
        {
            if (objects == null)
                objects = new List<IDataObject>();

            typeRecord.ObjectCount = InventoryService.ResolveObjectCount(total, objects.Count);
            typeRecord.ObjectCountIsEstimate = estimate || total < 0;
            typeRecord.SampledCount = objects.Count;

            foreach (var obj in objects)
            {
                if (obj == null || obj.State != DataState.Loaded)
                    continue;
                attrService.ProfileObject(obj, typeRecord.Attributes, typeRecord);
                RecordCreatorAndCreatedDate(report, obj);
                RecordResponsible(report, obj, typeRecord.Attributes);

                if (_systemFieldSampleBuffer.Count < 20)
                    _systemFieldSampleBuffer.Add(obj);
                if (_historySampleBuffer.Count < 5)
                    _historySampleBuffer.Add(obj);
            }

            if (_documentSampleBuffer.Count < 20)
            {
                foreach (var row in docService.SampleDocuments(objects, 5))
                {
                    if (_documentSampleBuffer.Count >= 20)
                        break;
                    _documentSampleBuffer.Add(row);
                }
            }

            foreach (var a in typeRecord.Attributes)
                attrService.FinalizeAttribute(a);

            if (typeRecord.Attributes.Count > 0 && typeRecord.SampledCount > 0)
                typeRecord.FillPercent = typeRecord.Attributes.Average(a => a.FillRate) * 100.0;

            typeRecord.Status = CapabilityStatus.Available;
        }

        private static void RecordCreatorAndCreatedDate(ProjectInventoryReport report, IDataObject obj)
        {
            if (report == null || obj == null)
                return;

            report.CreatorSampleTotal++;
            if (obj.Creator != null)
            {
                var id = obj.Creator.Id;
                if (report.CreatorSampleCounts.ContainsKey(id))
                    report.CreatorSampleCounts[id]++;
                else
                    report.CreatorSampleCounts[id] = 1;
            }

            var monthKey = AnalyticsFormats.MonthBucketKey(obj.Created);
            if (report.CreatedMonthSampleCounts.ContainsKey(monthKey))
                report.CreatedMonthSampleCounts[monthKey]++;
            else
                report.CreatedMonthSampleCounts[monthKey] = 1;
        }

        private static void RecordResponsible(
            ProjectInventoryReport report,
            IDataObject obj,
            IList<AttributeInventoryRecord> attributes)
        {
            if (report == null || obj == null || attributes == null || obj.Attributes == null)
                return;

            foreach (var attr in attributes)
            {
                if (attr == null || attr.ValueType != AttributeType.OrgUnit.ToString())
                    continue;
                if (!obj.Attributes.ContainsKey(attr.Name))
                    continue;

                object value;
                try
                {
                    value = obj.Attributes[attr.Name];
                }
                catch
                {
                    continue;
                }

                foreach (var orgId in ReferenceResolver.EnumerateIntIds(value))
                {
                    if (orgId == 0)
                        continue;
                    if (report.ResponsibleSampleCounts.ContainsKey(orgId))
                        report.ResponsibleSampleCounts[orgId]++;
                    else
                        report.ResponsibleSampleCounts[orgId] = 1;
                }
            }
        }

        private static ZoneResult Zone(string zone, string status, string message)
        {
            return new ZoneResult { Zone = zone, Status = status, Message = message };
        }
    }
}
