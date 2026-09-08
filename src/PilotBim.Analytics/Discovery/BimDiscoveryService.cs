using System;
using System.Collections.Generic;
using System.Linq;
using Ascon.Pilot.Bim.SDK;
using Ascon.Pilot.Bim.SDK.ModelStorage;
using Ascon.Pilot.Bim.SDK.Search;
using Ascon.Pilot.Bim.Search.SDK;
using Ascon.Pilot.Bim.Search.SDK.ModelProperties;
using Ascon.Pilot.SDK;
using PilotBim.Analytics.Data;
using PilotBim.Analytics.Diagnostics;
using PilotBim.Analytics.Models;
using BimTypeNames = Ascon.Pilot.Bim.SDK.TypeNames;

namespace PilotBim.Analytics.Discovery
{
    internal sealed class BimDiscoveryService
    {
        private readonly IObjectsRepository _repository;
        private readonly ISearchService _search;
        private readonly IModelStorageProvider _storageProvider;
        private readonly IModelSearchManager _searchManager;

        public BimDiscoveryService(
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

        public void Discover(
            ProjectInventoryReport report,
            System.Threading.CancellationToken token,
            HierarchyWalkResult walk,
            ScanMode mode,
            Action<string> progress)
        {
            void Progress(string msg)
            {
                if (progress != null)
                    progress(msg);
            }
            report.BimCapabilities.Clear();
            report.BimElementSamples.Clear();

            try
            {
                report.BimSdkVersion = typeof(IModelStorage).Assembly.GetName().Version != null
                    ? typeof(IModelStorage).Assembly.GetName().Version.ToString()
                    : "unknown";

                report.BimCapabilities.Add(Cap("BIM SDK assembly", CapabilityStatus.Available, "Ascon.Pilot.Bim.SDK", report.BimSdkVersion));
                report.BimCapabilities.Add(Cap("TypeNames.CoordinationModel", CapabilityStatus.Available, "Ascon.Pilot.Bim.SDK.TypeNames", BimTypeNames.CoordinationModel));
                report.BimCapabilities.Add(Cap("TypeNames.ModelPart", CapabilityStatus.Available, "Ascon.Pilot.Bim.SDK.TypeNames", BimTypeNames.ModelPart));
                report.BimCapabilities.Add(Cap("TypeNames.RemarksFolder", CapabilityStatus.Available, "Ascon.Pilot.Bim.SDK.TypeNames", BimTypeNames.RemarksFolder));
                report.BimCapabilities.Add(Cap("TypeNames.ViewPoint", CapabilityStatus.Available, "Ascon.Pilot.Bim.SDK.TypeNames", BimTypeNames.ViewPoint));
                report.BimCapabilities.Add(Cap("IModelStorageProvider", _storageProvider != null ? CapabilityStatus.Available : CapabilityStatus.Blocked, "IPilotServiceProvider.GetServices<IModelStorageProvider>()", null));
                report.BimCapabilities.Add(Cap("IModelSearchManager", _searchManager != null ? CapabilityStatus.Available : CapabilityStatus.Partial, "IPilotServiceProvider.GetServices<IModelSearchManager>()", _searchManager == null ? "Service not resolved in this session" : null));
                report.BimCapabilities.Add(Cap("GlobalId property", CapabilityStatus.Available, "Ascon.Pilot.Bim.Search.SDK.PropertyNames.GlobalId", PropertyNames.GlobalId));
                report.BimCapabilities.Add(Cap("GlobalIdReadable property", CapabilityStatus.Available, "Property.GlobalIdReadable / PropertyNames.GlobalIdReadable", PropertyNames.GlobalIdReadable));
                report.BimCapabilities.Add(Cap("ModelPartId property", CapabilityStatus.Available, "Property.ModelPartId / IModelElementId.ModelPartId", null));
                report.BimCapabilities.Add(Cap("bimObjectId attribute", CapabilityStatus.NeedsRuntime, "IDataObject.Attributes[\"bimObjectId\"]", "Config/attribute on remark cards; not a BIM SDK type member"));
                report.BimCapabilities.Add(Cap("Element properties", CapabilityStatus.Available, "IModelStorage.LoadElementProperties", "Probe only; avoid full scan"));
                report.BimCapabilities.Add(Cap("Model versions", CapabilityStatus.Available, "IModelStorage.GetVersions()", "DateTime versions"));
                report.BimCapabilities.Add(Cap("Remark → element relation", CapabilityStatus.Partial, "remark attribute bimObjectId + model search GlobalId", "Requires runtime mapping validation"));
                report.BimCapabilities.Add(Cap("Element → model", CapabilityStatus.Available, "IModelStorage.ModelId + storage scope", null));
                report.BimCapabilities.Add(Cap("Element → model part", CapabilityStatus.Available, "IModelElementId.ModelPartId", null));

                CountBimTypes(report, token, walk);
                if (token.IsCancellationRequested)
                    return;

                Progress("BIM index analytics (search .bm)...");
                new BimIndexAnalyticsService(_repository, _searchManager).Analyze(
                    report,
                    mode,
                    token,
                    walk,
                    new PilotObjectScanner(_repository, _search),
                    Progress);

                if (token.IsCancellationRequested)
                    return;

                ProbeElements(report, token, walk);
                report.ZoneResults.Add(new ZoneResult { Zone = "BIM", Status = CapabilityStatus.Pass, Message = "BIM discovery completed" });
            }
            catch (Exception ex)
            {
                AnalyticsLogger.Error("bim-discovery", ex);
                report.Errors.Add("BIM: " + ex.Message);
                report.ZoneResults.Add(new ZoneResult { Zone = "BIM", Status = CapabilityStatus.Error, Message = ex.Message });
                report.BimCapabilities.Add(Cap("BIM discovery", CapabilityStatus.Error, "BimDiscoveryService", ex.Message));
            }
        }

        private void CountBimTypes(ProjectInventoryReport report, System.Threading.CancellationToken token, HierarchyWalkResult walk)
        {
            if (_search == null)
            {
                if (walk != null && string.IsNullOrEmpty(walk.Error))
                {
                    report.BimModelsCount = walk.BimModelIds.Count;
                    report.BimModelPartsCount = walk.BimModelPartIds.Count;
                    report.BimCapabilities.Add(Cap("Models count", CapabilityStatus.Available, "Hierarchy walk TypeNames.CoordinationModel",
                        "count=" + report.BimModelsCount + (walk.Truncated ? " (lower bound)" : string.Empty)));
                    report.BimCapabilities.Add(Cap("Model parts count", CapabilityStatus.Available, "Hierarchy walk TypeNames.ModelPart",
                        "count=" + report.BimModelPartsCount + (walk.Truncated ? " (lower bound)" : string.Empty)));
                }
                else
                {
                    report.Warnings.Add("BIM object counts skipped: ISearchService unavailable and hierarchy walk empty");
                    report.BimCapabilities.Add(Cap("Models count", CapabilityStatus.Partial, "ISearchService + TypeNames.CoordinationModel", "Search unavailable"));
                    report.BimCapabilities.Add(Cap("Model parts count", CapabilityStatus.Partial, "ISearchService + TypeNames.ModelPart", "Search unavailable"));
                }
                return;
            }

            var modelType = _repository.GetType(BimTypeNames.CoordinationModel);
            var partType = _repository.GetType(BimTypeNames.ModelPart);
            var scanner = new PilotObjectScanner(_repository, _search);

            if (modelType != null)
            {
                using (var session = new CallbackWaitSession())
                {
                    scanner.SearchByType(modelType.Id, 50, (ids, t) =>
                    {
                        try
                        {
                            if (!session.ShouldAccept())
                                return;
                            report.BimModelsCount = t >= 0 ? t : (ids != null ? ids.Count : 0);
                        }
                        finally
                        {
                            session.SignalCompleted();
                        }
                    }, ex =>
                    {
                        AnalyticsLogger.Error("bim-models-count", ex);
                        session.SignalFailed(ex);
                    });
                    var wait = session.Wait(TimeSpan.FromSeconds(10));
                    var status = ResolveCountCapabilityStatus(
                        wait.Status == CallbackWaitStatus.TimedOut,
                        wait.Status == CallbackWaitStatus.Failed);
                    report.BimCapabilities.Add(Cap("Models count", status, "Search TypeId=" + modelType.Id,
                        "count=" + report.BimModelsCount
                        + (wait.Status == CallbackWaitStatus.TimedOut ? " (timeout)"
                            : wait.Status == CallbackWaitStatus.Failed ? " (error)" : string.Empty)));
                }
            }
            else
            {
                report.BimCapabilities.Add(Cap("Models count", CapabilityStatus.NotVerified, "TypeNames.CoordinationModel", "Type not present in current database"));
            }

            if (token.IsCancellationRequested)
                return;

            if (partType != null)
            {
                using (var session = new CallbackWaitSession())
                {
                    scanner.SearchByType(partType.Id, 50, (ids, t) =>
                    {
                        try
                        {
                            if (!session.ShouldAccept())
                                return;
                            report.BimModelPartsCount = t >= 0 ? t : (ids != null ? ids.Count : 0);
                        }
                        finally
                        {
                            session.SignalCompleted();
                        }
                    }, ex =>
                    {
                        AnalyticsLogger.Error("bim-parts-count", ex);
                        session.SignalFailed(ex);
                    });
                    var wait = session.Wait(TimeSpan.FromSeconds(10));
                    var status = ResolveCountCapabilityStatus(
                        wait.Status == CallbackWaitStatus.TimedOut,
                        wait.Status == CallbackWaitStatus.Failed);
                    report.BimCapabilities.Add(Cap("Model parts count", status, "Search TypeId=" + partType.Id,
                        "count=" + report.BimModelPartsCount
                        + (wait.Status == CallbackWaitStatus.TimedOut ? " (timeout)"
                            : wait.Status == CallbackWaitStatus.Failed ? " (error)" : string.Empty)));
                }
            }
            else
            {
                report.BimCapabilities.Add(Cap("Model parts count", CapabilityStatus.NotVerified, "TypeNames.ModelPart", "Type not present in current database"));
            }
        }

        /// <summary>
        /// Search count capability must not report Available when the wait timed out or errored.
        /// Successful empty result (count=0) remains Available.
        /// </summary>
        internal static string ResolveCountCapabilityStatus(bool timedOut, bool hadError)
        {
            if (hadError)
                return CapabilityStatus.Error;
            if (timedOut)
                return CapabilityStatus.Partial;
            return CapabilityStatus.Available;
        }

        private void ProbeElements(ProjectInventoryReport report, System.Threading.CancellationToken token, HierarchyWalkResult walk)
        {
            if (_storageProvider == null)
            {
                report.BimCapabilities.Add(Cap("Elements sample", CapabilityStatus.Blocked, "IModelStorageProvider", "Provider not resolved"));
                return;
            }

            Guid? modelId = null;

            if (_search == null)
            {
                if (walk != null && walk.BimModelIds.Count > 0)
                {
                    modelId = walk.BimModelIds[0];
                }
                else
                {
                    report.BimCapabilities.Add(Cap("Elements sample", CapabilityStatus.Partial, "Need model id from hierarchy walk", "Skipped"));
                    return;
                }
            }
            else
            {
                var modelType = _repository.GetType(BimTypeNames.CoordinationModel);
                if (modelType == null)
                {
                    report.BimCapabilities.Add(Cap("Elements sample", CapabilityStatus.NotVerified, "No coordination model type", null));
                    return;
                }

                using (var session = new CallbackWaitSession())
                {
                    var scanner = new PilotObjectScanner(_repository, _search);
                    scanner.SearchByType(modelType.Id, 1, (ids, total) =>
                    {
                        try
                        {
                            if (!session.ShouldAccept())
                                return;
                            if (ids != null && ids.Count > 0)
                                modelId = ids[0];
                        }
                        finally
                        {
                            session.SignalCompleted();
                        }
                    }, ex =>
                    {
                        AnalyticsLogger.Error("bim-probe-search", ex);
                        session.SignalFailed(ex);
                    });
                    var wait = session.Wait(TimeSpan.FromSeconds(10));
                    if (wait.Status == CallbackWaitStatus.TimedOut)
                        AnalyticsLogger.Warning("bim-probe-search", "timeout resolving model id");
                }
            }

            if (!modelId.HasValue)
            {
                report.BimCapabilities.Add(Cap("Elements sample", CapabilityStatus.Partial, "No models found", null));
                return;
            }

            if (token.IsCancellationRequested)
                return;

            try
            {
                var storage = _storageProvider.GetStorage(modelId.Value);
                if (storage == null)
                {
                    report.BimCapabilities.Add(Cap("Elements sample", CapabilityStatus.Blocked, "GetStorage returned null", modelId.Value.ToString()));
                    return;
                }

                // Compatibility: IModelStorage has no readiness wait API — poll IsLoaded briefly.
                // Do not force Load* loops; if still unloaded, skip probe (Partial).
                var waitLoad = 0;
                while (!storage.IsLoaded && waitLoad < 20 && !token.IsCancellationRequested)
                {
                    System.Threading.Thread.Sleep(200);
                    waitLoad++;
                }

                if (!storage.IsLoaded)
                {
                    report.Warnings.Add("BIM storage not loaded for model " + modelId.Value + "; element probe skipped to avoid hang");
                    report.BimCapabilities.Add(Cap("Elements sample", CapabilityStatus.Partial, "IModelStorage.IsLoaded=false", "Open model in viewer for full probe"));
                    report.BimCapabilities.Add(Cap("Elements", CapabilityStatus.Partial, "IModelStorage.LoadElements", "API available; runtime load required"));
                    return;
                }

                var versions = storage.GetVersions() != null ? storage.GetVersions().ToList() : new List<DateTime>();
                var version = versions.Count > 0 ? versions.Max() : DateTime.MinValue;
                var partIds = storage.GetModelPartsIds() != null ? storage.GetModelPartsIds().Take(1).ToList() : new List<Guid>();
                if (partIds.Count == 0)
                {
                    report.BimCapabilities.Add(Cap("Elements sample", CapabilityStatus.Partial, "No model parts in storage", null));
                    return;
                }

                var partId = partIds[0];
                // Prefer root elements to avoid loading entire part.
                IEnumerable<IModelElement> roots = null;
                try
                {
                    roots = storage.LoadRootElements(version);
                }
                catch (Exception ex)
                {
                    AnalyticsLogger.Warning("bim-root-elements", ex.Message);
                }

                var sampleElements = new List<IModelElement>();
                if (roots != null)
                {
                    foreach (var el in roots)
                    {
                        if (el == null)
                            continue;
                        sampleElements.Add(el);
                        if (sampleElements.Count >= 10)
                            break;
                    }
                }

                if (sampleElements.Count < 50)
                {
                    try
                    {
                        // Bounded enumeration — stop after 50.
                        var loaded = storage.LoadElements(partId, version);
                        if (loaded != null)
                        {
                            foreach (var el in loaded)
                            {
                                if (token.IsCancellationRequested)
                                    break;
                                if (el == null)
                                    continue;
                                if (sampleElements.Any(x => x.Id != null && el.Id != null && x.Id.ElementId == el.Id.ElementId))
                                    continue;
                                sampleElements.Add(el);
                                if (sampleElements.Count >= 50)
                                    break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        AnalyticsLogger.Warning("bim-load-elements", ex.Message);
                        report.Warnings.Add("LoadElements probe: " + ex.Message);
                    }
                }

                foreach (var el in sampleElements.Take(50))
                {
                    if (token.IsCancellationRequested)
                        break;
                    var row = new BimElementSample
                    {
                        ModelId = modelId.Value,
                        ModelPartId = el.Id != null ? el.Id.ModelPartId : partId,
                        ElementId = el.Id != null ? el.Id.ElementId : Guid.Empty,
                        Name = el.Name,
                        Type = el.Type
                    };

                    try
                    {
                        var props = storage.LoadElementProperties(el.Id, version);
                        var propList = props != null ? props.ToList() : new List<IPropertySet>();
                        var flat = new List<IProperty>();
                        foreach (var set in propList)
                        {
                            if (set == null || set.Properties == null)
                                continue;
                            flat.AddRange(set.Properties.Where(p => p != null));
                        }
                        row.PropertyCount = flat.Count;
                        string globalId = null;
                        string globalIdAlt = null;
                        string globalIdReadable = null;
                        foreach (var p in flat)
                        {
                            if (string.Equals(p.Name, PropertyNames.GlobalId, StringComparison.OrdinalIgnoreCase))
                                globalId = ReferenceResolver.SafeSampleString(p.Value, 64);
                            else if (string.Equals(p.Name, "GlobalId", StringComparison.OrdinalIgnoreCase))
                                globalIdAlt = ReferenceResolver.SafeSampleString(p.Value, 64);
                            else if (string.Equals(p.Name, PropertyNames.GlobalIdReadable, StringComparison.OrdinalIgnoreCase))
                                globalIdReadable = ReferenceResolver.SafeSampleString(p.Value, 64);

                            if (row.PropertyPreview.Count < 5)
                                row.PropertyPreview.Add((p.Name ?? "?") + "=" + ReferenceResolver.SafeSampleString(p.Value, 40));
                        }
                        row.GlobalId = SelectGlobalId(globalId, globalIdAlt, globalIdReadable);
                    }
                    catch (Exception ex)
                    {
                        AnalyticsLogger.Warning("bim-props", el.Id + " " + ex.Message);
                    }

                    report.BimElementSamples.Add(row);
                }

                report.BimCapabilities.Add(Cap("Elements sample", CapabilityStatus.Available, "LoadRootElements/LoadElements Take<=50", "sampled=" + report.BimElementSamples.Count));
                report.BimCapabilities.Add(Cap("Elements", CapabilityStatus.Available, "IModelStorage.LoadElements", "Full count NOT scanned automatically"));
            }
            catch (Exception ex)
            {
                AnalyticsLogger.Error("bim-probe", ex);
                report.Warnings.Add("BIM element probe failed: " + ex.Message);
                report.BimCapabilities.Add(Cap("Elements sample", CapabilityStatus.Error, "IModelStorage", ex.Message));
            }
        }

        /// <summary>
        /// Prefer canonical PropertyNames.GlobalId, then literal "GlobalId", then readable display form.
        /// </summary>
        internal static string SelectGlobalId(string globalId, string globalIdAlt, string globalIdReadable)
        {
            if (!string.IsNullOrWhiteSpace(globalId))
                return globalId;
            if (!string.IsNullOrWhiteSpace(globalIdAlt))
                return globalIdAlt;
            if (!string.IsNullOrWhiteSpace(globalIdReadable))
                return globalIdReadable;
            return null;
        }

        private static BimCapability Cap(string name, string availability, string source, string notes)
        {
            return new BimCapability
            {
                Name = name,
                Availability = availability,
                SdkSource = source,
                Notes = notes
            };
        }
    }
}
