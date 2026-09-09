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
                new ObjectSamplingCoordinator(
                        _repository,
                        _search,
                        _historySampleBuffer,
                        _systemFieldSampleBuffer,
                        _documentSampleBuffer)
                    .SampleAllTypes(report, token, Progress, walk);

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
                    report.FinalStatus = "PARTIAL_RUNTIME_INVENTORY";

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

        /// <summary>
        /// TypeInventoryRecord.ObjectCount is long — keep full search Total without int narrowing.
        /// </summary>
        internal static long ResolveObjectCount(long total, int sampleCount)
        {
            if (total >= 0)
                return total;
            return sampleCount;
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
