using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Ascon.Pilot.Bim.SDK.ModelStorage;
using Ascon.Pilot.Bim.SDK.Search;
using Ascon.Pilot.SDK;
using PilotBim.Analytics.Data;
using PilotBim.Analytics.Diagnostics;
using PilotBim.Analytics.Discovery;
using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.Services
{
    public sealed class InventoryService
    {
        private readonly IObjectsRepository _repository;
        private readonly ISearchService _search;
        private readonly IModelStorageProvider _storageProvider;
        private readonly IModelSearchManager _searchManager;

        public InventoryService(
            IObjectsRepository repository,
            ISearchService search,
            IModelStorageProvider storageProvider,
            IModelSearchManager searchManager)
        {
            _repository = repository;
            _search = search;
            _storageProvider = storageProvider;
            _searchManager = searchManager;
        }

        public ProjectInventoryReport Run(
            ScanMode mode,
            CancellationToken token,
            Action<string> progress)
        {
            var report = new ProjectInventoryReport
            {
                GeneratedAt = DateTime.Now,
                ScanMode = mode.ToString().ToUpperInvariant(),
                SampleLimit = (int)mode
            };

            void Progress(string msg)
            {
                if (progress != null)
                    progress(msg);
                AnalyticsLogger.Info("progress", msg);
                report.Diagnostics.Add(AnalyticsLogger.CreateEntry("INFO", "progress", msg));
            }

            try
            {
                Progress("SDK discovery...");
                var sdkDiscovery = new PilotSdkDiscoveryService();
                sdkDiscovery.PopulateStaticSdkInventory(report);
                report.ZoneResults.Add(Zone("SDK", CapabilityStatus.Pass, "Static reflection inventory"));

                if (token.IsCancellationRequested)
                    return Cancel(report);

                Progress("Scanning types...");
                var typeService = new TypeDiscoveryService(_repository);
                report.Types = typeService.DiscoverTypes();
                report.TypesDiscovered = report.Types.Count;
                report.ZoneResults.Add(Zone("Types", CapabilityStatus.Pass, "types=" + report.TypesDiscovered));

                Progress("Resolving states...");
                try
                {
                    var stateService = new StateDiscoveryService(_repository);
                    report.States = stateService.DiscoverStates();
                    report.StatesDiscovered = report.States.Count;
                    new StateMappingService().ApplyOverrides(report.States);
                    report.ZoneResults.Add(Zone("States", CapabilityStatus.Pass, "states=" + report.StatesDiscovered));
                }
                catch (Exception ex)
                {
                    FailZone(report, "States", ex);
                }

                if (token.IsCancellationRequested)
                    return Cancel(report);

                Progress("Resolving organisations...");
                Dictionary<int, OrganisationInventoryRecord> orgMap = new Dictionary<int, OrganisationInventoryRecord>();
                try
                {
                    var orgService = new OrganisationDiscoveryService(_repository);
                    report.Organisations = orgService.DiscoverOrganisations();
                    report.OrganisationsResolved = report.Organisations.Count;
                    orgMap = report.Organisations.ToDictionary(o => o.OrganisationId, o => o);
                    report.ZoneResults.Add(Zone("Organisations", CapabilityStatus.Pass, "orgs=" + report.OrganisationsResolved));
                }
                catch (Exception ex)
                {
                    FailZone(report, "Organisations", ex);
                }

                Progress("Resolving persons...");
                try
                {
                    var personService = new PersonDiscoveryService(_repository);
                    report.Persons = personService.DiscoverPersons(orgMap);
                    report.PersonsResolved = report.Persons.Count;
                    report.ZoneResults.Add(Zone("Persons", CapabilityStatus.Pass, "persons=" + report.PersonsResolved));
                }
                catch (Exception ex)
                {
                    FailZone(report, "Persons", ex);
                }

                if (token.IsCancellationRequested)
                    return Cancel(report);

                HierarchyWalkResult walk = null;
                var scanner = new PilotObjectScanner(_repository, _search);

                if (_search == null)
                {
                    Progress("Hierarchy walk (ISearchService unavailable)...");
                    walk = new HierarchyWalkSampler(_repository, scanner).Walk(mode, token);
                    report.Warnings.Add("ISearchService unavailable — using hierarchy walk for object inventory");
                    report.ZoneResults.Add(Zone("Search", CapabilityStatus.Partial,
                        "ISearchService null; hierarchy walk visited=" + walk.VisitedCount
                        + " objects=" + walk.TotalObjects
                        + (walk.Truncated ? " truncated=true" : string.Empty)));
                    if (!string.IsNullOrEmpty(walk.Error))
                        report.Errors.Add(walk.Error);
                    if (walk.Truncated)
                        report.Warnings.Add("Hierarchy walk truncated at visit budget; object counts are estimates");
                }
                else
                {
                    report.ZoneResults.Add(Zone("Search", CapabilityStatus.Pass, "ISearchService available"));
                }

                if (token.IsCancellationRequested)
                    return Cancel(report);

                Progress("Sampling objects / attributes...");
                SampleAllTypes(report, token, Progress, walk);

                if (token.IsCancellationRequested)
                    return Cancel(report);

                try
                {
                    new StateDiscoveryService(_repository).ApplyObservedUsage(report.States, report.Types);
                }
                catch (Exception ex)
                {
                    AnalyticsLogger.Warning("state-usage", ex.Message);
                }

                Progress("Document / version capabilities...");
                try
                {
                    report.DocumentCapabilities = new DocumentDiscoveryService().BuildCapabilities();
                    report.ZoneResults.Add(Zone("Documents", CapabilityStatus.Pass, "capabilities inventoried"));
                }
                catch (Exception ex)
                {
                    FailZone(report, "Documents", ex);
                }

                Progress("History capabilities...");
                try
                {
                    var history = new HistoryDiscoveryService(_repository);
                    report.HistoryCapabilities = history.BuildCapabilities();
                    if (_historySampleBuffer.Count > 0)
                        history.SampleHistory(_historySampleBuffer, report.HistorySamples, 10);
                    report.ZoneResults.Add(Zone("History", CapabilityStatus.Pass, "samples=" + report.HistorySamples.Count));
                }
                catch (Exception ex)
                {
                    FailZone(report, "History", ex);
                }

                if (token.IsCancellationRequested)
                    return Cancel(report);

                Progress("Inspecting BIM SDK...");
                var bim = new BimDiscoveryService(_repository, _search, _storageProvider, _searchManager);
                bim.Discover(report, token, walk, mode, Progress);

                if (token.IsCancellationRequested)
                    return Cancel(report);

                Progress("Remark → BIM link analytics...");
                new RemarkAnalyticsService(_repository, _search, _searchManager)
                    .Analyze(report, mode, token, Progress);

                report.DocumentSamples = _documentSampleBuffer;
                new SystemFieldDiscoveryService().EnrichFromSample(report, _systemFieldSampleBuffer);

                report.AllAttributes = report.Types.SelectMany(t => t.Attributes).ToList();
                report.AttributesDiscovered = report.AllAttributes.Count;
                report.ObjectsFound = report.Types.Sum(t => t.ObjectCount > 0 ? t.ObjectCount : 0);

                Progress("Building analytics capability matrix...");
                new CapabilityMatrixService().Build(report);

                report.PerformanceNotes.Add("Sample limit=" + report.SampleLimit + " mode=" + report.ScanMode);
                if (report.BimPartAnalytics != null && report.BimPartAnalytics.Count > 0)
                {
                    var indexed = report.BimPartAnalytics.Sum(p => p.ElementCount);
                    report.PerformanceNotes.Add("BIM element counts from search index (.bm): " + indexed
                        + " across " + report.BimPartAnalytics.Count + " parts.");
                }
                else
                    report.PerformanceNotes.Add("BIM index analytics produced no part rows.");
                report.PerformanceNotes.Add("IModelStorage element sample runs only when model is loaded in viewer.");
                if (_search != null)
                    report.PerformanceNotes.Add("Object counts use ISearchResult.Total when search is available.");
                else
                    report.PerformanceNotes.Add("Object counts from hierarchy walk (Root→Children→SubscribeObjects)"
                        + (walk != null ? "; visited=" + walk.VisitedCount + " truncated=" + walk.Truncated : string.Empty) + ".");
                report.PerformanceNotes.Add("Attribute profiling uses sampled objects only.");

                if (string.IsNullOrEmpty(report.FinalStatus))
                {
                    report.FinalStatus = report.ObjectsFound > 0
                        ? "PARTIAL_RUNTIME_INVENTORY"
                        : "PARTIAL_RUNTIME_INVENTORY";
                }

                Progress("Inventory complete.");
            }
            catch (Exception ex)
            {
                AnalyticsLogger.Error("inventory", ex);
                report.Errors.Add(ex.Message);
                report.FinalStatus = "BLOCKED_BY_SDK";
            }

            return report;
        }

        private readonly List<IDataObject> _historySampleBuffer = new List<IDataObject>();
        private readonly List<IDataObject> _systemFieldSampleBuffer = new List<IDataObject>();
        private readonly List<DocumentSampleRow> _documentSampleBuffer = new List<DocumentSampleRow>();

        private void SampleAllTypes(
            ProjectInventoryReport report,
            CancellationToken token,
            Action<string> progress,
            HierarchyWalkResult walk)
        {
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

                var gate = new ManualResetEventSlim(false);
                Exception sampleError = null;
                sampler.SampleType(typeRecord.TypeId, report.SampleLimit, (objects, total) =>
                {
                    try
                    {
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
                        gate.Set();
                    }
                }, ex =>
                {
                    sampleError = ex;
                    gate.Set();
                });

                if (!gate.Wait(TimeSpan.FromSeconds(30)))
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

            typeRecord.ObjectCount = total >= 0 ? (int)Math.Min(total, int.MaxValue) : objects.Count;
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

            var monthKey = obj.Created.ToString("yyyy-MM");
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

        private static ProjectInventoryReport Cancel(ProjectInventoryReport report)
        {
            report.Cancelled = true;
            report.Warnings.Add("Scan cancelled by user");
            report.FinalStatus = "PARTIAL_RUNTIME_INVENTORY";
            report.AnalyticsReadiness = CapabilityStatus.Partial;
            return report;
        }

        private static ZoneResult Zone(string zone, string status, string message)
        {
            return new ZoneResult { Zone = zone, Status = status, Message = message };
        }

        private static void FailZone(ProjectInventoryReport report, string zone, Exception ex)
        {
            AnalyticsLogger.Error(zone, ex);
            report.Errors.Add(zone + ": " + ex.Message);
            report.ZoneResults.Add(Zone(zone, CapabilityStatus.Error, ex.Message));
        }

        public void LoadChildren(Guid parentId, Action<IReadOnlyList<IDataObject>> onDone)
        {
            var cached = new PilotObjectScanner(_repository, _search).TryGetCached(parentId);
            if (cached != null && cached.Children != null)
            {
                var childIds = cached.Children.ToList();
                if (childIds.Count == 0)
                {
                    onDone(new List<IDataObject>());
                    return;
                }

                var loaded = new List<IDataObject>();
                var remaining = new HashSet<Guid>(childIds);
                new PilotObjectScanner(_repository, _search).SubscribeObjects(childIds, obj =>
                {
                    if (obj == null || !remaining.Remove(obj.Id))
                        return;
                    loaded.Add(obj);
                    if (remaining.Count == 0)
                        onDone(loaded);
                }, () => onDone(loaded), ex =>
                {
                    AnalyticsLogger.Error("structure-children", ex);
                    onDone(loaded);
                });
                return;
            }

            new PilotObjectScanner(_repository, _search).SubscribeObjects(new[] { parentId }, parent =>
            {
                if (parent == null || parent.Children == null || parent.Children.Count == 0)
                {
                    onDone(new List<IDataObject>());
                    return;
                }

                var childIds = parent.Children.ToList();
                var loaded = new List<IDataObject>();
                var remaining = new HashSet<Guid>(childIds);
                new PilotObjectScanner(_repository, _search).SubscribeObjects(childIds, obj =>
                {
                    if (obj == null || !remaining.Remove(obj.Id))
                        return;
                    loaded.Add(obj);
                    if (remaining.Count == 0)
                        onDone(loaded);
                }, () => onDone(loaded), ex =>
                {
                    AnalyticsLogger.Error("structure-children", ex);
                    onDone(loaded);
                });
            }, () => { }, ex =>
            {
                AnalyticsLogger.Error("structure-parent", ex);
                onDone(new List<IDataObject>());
            });
        }

        public IDataObject GetRootObject()
        {
            try
            {
#pragma warning disable 612
                return _repository.GetRootObject();
#pragma warning restore 612
            }
            catch (Exception ex)
            {
                AnalyticsLogger.Error("root-object", ex);
                return null;
            }
        }
    }
}
