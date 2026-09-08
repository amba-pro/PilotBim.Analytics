using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Ascon.Pilot.Bim.SDK.Search;
using Ascon.Pilot.Bim.Search.SDK;
using Ascon.Pilot.Bim.Search.SDK.Builders;
using Ascon.Pilot.Bim.Search.SDK.ModelProperties;
using Ascon.Pilot.Bim.Search.SDK.Serialization;
using Ascon.Pilot.SDK;
using PilotBim.Analytics.Data;
using PilotBim.Analytics.Diagnostics;
using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.Discovery
{
    internal sealed class RemarkAnalyticsService
    {
        private static readonly SearchProperty GlobalIdProperty = Property.WithName(
            PropertyNames.GlobalId,
            PropertyNames.CommonPropertiesCategory,
            PropertyDataType.String);

        private readonly IObjectsRepository _repository;
        private readonly ISearchService _search;
        private readonly IModelSearchManager _searchManager;

        public RemarkAnalyticsService(
            IObjectsRepository repository,
            ISearchService search,
            IModelSearchManager searchManager)
        {
            _repository = repository;
            _search = search;
            _searchManager = searchManager;
        }

        public void Analyze(ProjectInventoryReport report, ScanMode mode, CancellationToken token, Action<string> progress)
        {
            report.RemarkLinks.Clear();
            report.RemarkAnalytics = new RemarkAnalyticsSummary();

            var remarkTypes = report.Types.Where(IsRemarkType).Where(t => t.ObjectCount > 0).ToList();
            report.RemarkAnalytics.TotalRemarkObjects = remarkTypes.Sum(t => t.ObjectCount);

            if (remarkTypes.Count == 0)
            {
                report.RemarkAnalytics.Scope = "no remark types with objects";
                return;
            }

            if (_search == null)
            {
                report.Warnings.Add("Remark link validation skipped: ISearchService unavailable");
                report.RemarkAnalytics.Scope = "search unavailable";
                return;
            }

            progress("Remark analytics: sampling issue/remark cards...");
            var scanner = new PilotObjectScanner(_repository, _search);
            var sampler = new PilotObjectSampler(scanner);
            int samplePerType = mode == ScanMode.Fast ? 10 : mode == ScanMode.Full ? 100 : 30;
            var partIds = report.BimPartAnalytics != null
                ? report.BimPartAnalytics.Select(p => p.PartId).Distinct().ToList()
                : new List<Guid>();
            IModelSearchService indexSearch = TryOpenIndexSearch(partIds);

            foreach (var type in remarkTypes)
            {
                if (token.IsCancellationRequested)
                    break;

                var gate = new ManualResetEventSlim(false);
                var abandoned = 0;
                sampler.SampleType(type.TypeId, samplePerType, (objects, total) =>
                {
                    try
                    {
                        if (!AsyncCallbackGuard.ShouldAccept(System.Threading.Interlocked.CompareExchange(ref abandoned, 0, 0)))
                            return;
                        foreach (var obj in objects ?? new List<IDataObject>())
                        {
                            if (token.IsCancellationRequested)
                                break;
                            if (!AsyncCallbackGuard.ShouldAccept(System.Threading.Interlocked.CompareExchange(ref abandoned, 0, 0)))
                                return;
                            if (obj == null || obj.State != DataState.Loaded)
                                continue;

                            var row = BuildRow(obj, type, indexSearch);
                            report.RemarkLinks.Add(row);
                            report.RemarkAnalytics.SampledRemarks++;
                            if (!string.IsNullOrWhiteSpace(row.BimObjectId))
                            {
                                report.RemarkAnalytics.WithBimObjectId++;
                                if (row.ResolvedInIndex)
                                    report.RemarkAnalytics.ResolvedInIndex++;
                            }
                        }
                    }
                    finally
                    {
                        gate.Set();
                    }
                }, _ => gate.Set());

                if (!gate.Wait(TimeSpan.FromSeconds(20)))
                    AsyncCallbackGuard.Abandon(ref abandoned);
            }

            var indexed = report.BimPartAnalytics != null
                ? report.BimPartAnalytics.Sum(p => p.ElementCount)
                : 0L;
            report.RemarkAnalytics.IndexedElements = indexed;
            if (indexed > 0 && report.RemarkAnalytics.TotalRemarkObjects > 0)
                report.RemarkAnalytics.RemarksPer1000Elements =
                    (double)report.RemarkAnalytics.TotalRemarkObjects / indexed * 1000.0;

            report.RemarkAnalytics.Scope = "sampled=" + report.RemarkAnalytics.SampledRemarks
                + " types=" + remarkTypes.Count
                + (indexSearch != null ? "; index resolve ON" : "; index resolve OFF");

            report.ZoneResults.Add(new ZoneResult
            {
                Zone = "Remarks",
                Status = report.RemarkAnalytics.ResolvedInIndex > 0 ? CapabilityStatus.Pass : CapabilityStatus.Partial,
                Message = "sampled=" + report.RemarkAnalytics.SampledRemarks
                    + " resolved=" + report.RemarkAnalytics.ResolvedInIndex
            });
        }

        private RemarkLinkRow BuildRow(IDataObject obj, TypeInventoryRecord type, IModelSearchService indexSearch)
        {
            var row = new RemarkLinkRow
            {
                ObjectId = obj.Id,
                DisplayName = obj.DisplayName,
                TypeName = type.Title ?? type.Name,
                MappingStatus = CapabilityStatus.NeedsMapping
            };

            string bimId = null;
            try
            {
                if (obj.Attributes != null)
                {
                    foreach (var key in obj.Attributes.Keys)
                    {
                        if (!string.Equals(key, "bimObjectId", StringComparison.OrdinalIgnoreCase))
                            continue;
                        bimId = ReferenceResolver.SafeSampleString(obj.Attributes[key], 128);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                row.Notes = "read attribute: " + ex.Message;
                return row;
            }

            row.BimObjectId = bimId;
            if (string.IsNullOrWhiteSpace(bimId))
            {
                row.Notes = "bimObjectId empty";
                return row;
            }

            if (indexSearch == null)
            {
                row.Notes = "index search unavailable";
                return row;
            }

            try
            {
                var expressions = ModelSearchExpressionsBuilder.CreateBuilder()
                    .AddExpression(GlobalIdProperty.EqualTo(bimId));
                var set = ModelSearchSetBuilder.CreateBuilder(SearchSetLogicalOperator.And)
                    .AddExpressions(expressions)
                    .Build();
                var hits = indexSearch.Search(set, 5);
                var list = hits as IList<Ascon.Pilot.Bim.SDK.IModelElementId> ?? hits?.ToList();
                row.ResolvedInIndex = list != null && list.Count > 0;
                row.MappingStatus = row.ResolvedInIndex ? CapabilityStatus.Ready : CapabilityStatus.NeedsMapping;
                row.Notes = row.ResolvedInIndex
                    ? "GlobalId found in index (" + list.Count + " hit(s))"
                    : "GlobalId not found in index";
            }
            catch (Exception ex)
            {
                AnalyticsLogger.Warning("remark-resolve", obj.Id + " " + ex.Message);
                row.Notes = ex.Message;
            }

            return row;
        }

        private IModelSearchService TryOpenIndexSearch(List<Guid> partIds)
        {
            if (_searchManager == null || partIds == null || partIds.Count == 0)
                return null;

            try
            {
                var svc = _searchManager.GetModelPartsSearchServiceAsync(partIds).GetAwaiter().GetResult();
                if (svc == null)
                    return null;
                foreach (var partId in partIds)
                {
                    try
                    {
                        var task = svc.AddModelPartAsync(partId);
                        if (task != null)
                            task.GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        AnalyticsLogger.Warning("remark-index-part", partId + " " + ex.Message);
                    }
                }
                return svc;
            }
            catch (Exception ex)
            {
                AnalyticsLogger.Warning("remark-index", ex.Message);
                return null;
            }
        }

        private static bool IsRemarkType(TypeInventoryRecord type)
        {
            if (type == null)
                return false;
            var name = (type.Name ?? string.Empty).ToLowerInvariant();
            var title = (type.Title ?? string.Empty).ToLowerInvariant();
            return name.IndexOf("issue", StringComparison.Ordinal) >= 0
                || name.IndexOf("remark", StringComparison.Ordinal) >= 0
                || title.IndexOf("замечан", StringComparison.Ordinal) >= 0;
        }
    }
}
