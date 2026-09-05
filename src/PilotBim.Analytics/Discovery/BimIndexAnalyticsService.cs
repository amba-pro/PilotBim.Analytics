using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Ascon.Pilot.Bim.SDK;
using Ascon.Pilot.Bim.SDK.Search;
using Ascon.Pilot.Bim.Search.SDK;
using Ascon.Pilot.Bim.Search.SDK.Builders;
using Ascon.Pilot.Bim.Search.SDK.ModelProperties;
using Ascon.Pilot.Bim.Search.SDK.SearchExpressions;
using Ascon.Pilot.Bim.Search.SDK.Serialization;
using Ascon.Pilot.SDK;
using PilotBim.Analytics.Data;
using PilotBim.Analytics.Diagnostics;
using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.Discovery
{
    /// <summary>
    /// Element analytics via BIM search index (.bm) — no viewer / IsLoaded required.
    /// </summary>
    internal sealed class BimIndexAnalyticsService
    {
        private static readonly SearchProperty GlobalIdProperty = Property.WithName(
            PropertyNames.GlobalId,
            PropertyNames.CommonPropertiesCategory,
            PropertyDataType.String);

        private static readonly SearchProperty TypeProperty = Property.WithName(
            PropertyNames.Type,
            PropertyNames.CommonPropertiesCategory,
            PropertyDataType.String);

        /// <summary>Short list for Standard mode (full list only in Full).</summary>
        private static readonly string[] CoreIfcTypes =
        {
            "IfcWall", "IfcWallStandardCase", "IfcSlab", "IfcDoor", "IfcWindow", "IfcColumn", "IfcBeam",
            "IfcCovering", "IfcBuildingElementProxy", "IfcSpace", "IfcPipeSegment", "IfcDuctSegment"
        };

        private static readonly string[] FullIfcTypes =
        {
            "IfcWall", "IfcWallStandardCase", "IfcSlab", "IfcDoor", "IfcWindow", "IfcColumn", "IfcBeam",
            "IfcCovering", "IfcBuildingElementProxy", "IfcFlowSegment", "IfcFlowTerminal", "IfcPipeSegment",
            "IfcDuctSegment", "IfcSpace", "IfcBuildingStorey", "IfcBuilding", "IfcSite", "IfcStair", "IfcRailing",
            "IfcPlate", "IfcMember", "IfcFooting", "IfcPile", "IfcRoof", "IfcCurtainWall", "IfcOpeningElement",
            "IfcAnnotation", "IfcGrid", "IfcFurnishingElement", "IfcTransportElement", "IfcMechanicalFastener"
        };

        private readonly IObjectsRepository _repository;
        private readonly IModelSearchManager _searchManager;

        public BimIndexAnalyticsService(IObjectsRepository repository, IModelSearchManager searchManager)
        {
            _repository = repository;
            _searchManager = searchManager;
        }

        public void Analyze(
            ProjectInventoryReport report,
            ScanMode mode,
            CancellationToken token,
            HierarchyWalkResult walk,
            PilotObjectScanner scanner,
            Action<string> progress)
        {
            report.BimModelAnalytics.Clear();
            report.BimPartAnalytics.Clear();
            report.BimElementTypeCounts.Clear();

            if (_searchManager == null)
            {
                report.Warnings.Add("IModelSearchManager unavailable — BIM element index analytics skipped");
                report.ZoneResults.Add(new ZoneResult
                {
                    Zone = "BIM Index",
                    Status = CapabilityStatus.Partial,
                    Message = "IModelSearchManager null"
                });
                return;
            }

            var catalog = new BimPartCatalogBuilder(_repository, scanner).Build(walk, token, progress);
            if (catalog.Count == 0)
            {
                report.Warnings.Add("No BIM model parts found for index analytics");
                report.ZoneResults.Add(new ZoneResult
                {
                    Zone = "BIM Index",
                    Status = CapabilityStatus.Partial,
                    Message = "parts=0"
                });
                return;
            }

            int maxHits = GetMaxHits(mode);
            bool doTypeBreakdown = mode != ScanMode.Fast;
            string[] ifcTypes = mode == ScanMode.Full ? FullIfcTypes : CoreIfcTypes;
            int typeBreakdownBudget = mode == ScanMode.Full ? catalog.Count : Math.Min(catalog.Count, 25);
            int partIndex = 0;
            int typeBreakdownDone = 0;

            progress("BIM index: " + catalog.Count + " частей (таблицы заполнятся после завершения)...");

            foreach (var modelGroup in catalog.GroupBy(p => p.ModelId == Guid.Empty ? p.PartId : p.ModelId))
            {
                if (token.IsCancellationRequested)
                    break;

                var partsInModel = modelGroup.ToList();
                var modelId = partsInModel[0].ModelId != Guid.Empty ? partsInModel[0].ModelId : partsInModel[0].PartId;
                var modelName = partsInModel[0].ModelName ?? ("Model " + modelId);

                progress("BIM index: " + modelName + " (" + partsInModel.Count + " parts)");

                IModelSearchService searchService = null;
                try
                {
                    searchService = _searchManager
                        .GetModelPartsSearchServiceAsync(partsInModel.Select(p => p.PartId))
                        .GetAwaiter()
                        .GetResult();
                }
                catch (Exception ex)
                {
                    AnalyticsLogger.Warning("bim-index-service", modelId + " " + ex.Message);
                    foreach (var part in partsInModel)
                    {
                        report.BimPartAnalytics.Add(new BimPartAnalyticsRow
                        {
                            PartId = part.PartId,
                            PartName = part.PartName,
                            ModelId = modelId,
                            ModelName = modelName,
                            Status = CapabilityStatus.Error,
                            DataSource = "IModelSearchManager",
                            Notes = ex.Message
                        });
                    }
                    continue;
                }

                if (searchService == null)
                {
                    foreach (var part in partsInModel)
                    {
                        report.BimPartAnalytics.Add(new BimPartAnalyticsRow
                        {
                            PartId = part.PartId,
                            PartName = part.PartName,
                            ModelId = modelId,
                            ModelName = modelName,
                            Status = CapabilityStatus.Blocked,
                            DataSource = "IModelSearchManager",
                            Notes = "GetModelPartsSearchServiceAsync returned null"
                        });
                    }
                    continue;
                }

                TryRegisterParts(searchService, partsInModel.Select(p => p.PartId));

                long modelElements = 0;
                long modelGlobalIds = 0;
                bool modelEstimate = false;

                foreach (var part in partsInModel)
                {
                    if (token.IsCancellationRequested)
                        break;

                    partIndex++;
                    progress("BIM index " + partIndex + "/" + catalog.Count + ": " + (part.PartName ?? "?"));

                    bool withTypes = doTypeBreakdown && typeBreakdownDone < typeBreakdownBudget;
                    var row = AnalyzePart(searchService, part, modelId, modelName, maxHits, withTypes, ifcTypes);
                    if (withTypes)
                        typeBreakdownDone++;

                    report.BimPartAnalytics.Add(row);

                    foreach (var typeRow in row.TypeBreakdown)
                        report.BimElementTypeCounts.Add(typeRow);

                    modelElements += row.ElementCount;
                    modelGlobalIds += row.GlobalIdCount;
                    if (row.IsTruncated)
                        modelEstimate = true;
                }

                report.BimModelAnalytics.Add(new BimModelAnalyticsRow
                {
                    ModelId = modelId,
                    ModelName = modelName,
                    PartCount = partsInModel.Count,
                    ElementCount = modelElements,
                    GlobalIdCount = modelGlobalIds,
                    GlobalIdFillPercent = modelElements > 0 ? (double)modelGlobalIds / modelElements * 100.0 : 0,
                    IsEstimate = modelEstimate,
                    Status = CapabilityStatus.Available,
                    Notes = "Index search; no viewer required"
                });
            }

            long totalIndexedElements = report.BimPartAnalytics.Sum(p => p.ElementCount);
            report.ZoneResults.Add(new ZoneResult
            {
                Zone = "BIM Index",
                Status = CapabilityStatus.Pass,
                Message = "parts=" + report.BimPartAnalytics.Count + " indexedElements=" + totalIndexedElements
            });

            report.PerformanceNotes.Add("BIM index analytics: maxHits=" + maxHits + " source=IModelSearchService");
        }

        private BimPartAnalyticsRow AnalyzePart(
            IModelSearchService searchService,
            BimPartRef part,
            Guid modelId,
            string modelName,
            int maxHits,
            bool withTypeBreakdown,
            string[] ifcTypes)
        {
            var row = new BimPartAnalyticsRow
            {
                PartId = part.PartId,
                PartName = part.PartName ?? part.PartId.ToString(),
                ModelId = modelId,
                ModelName = modelName,
                DataSource = "IModelSearchService"
            };

            try
            {
                bool truncated;
                // Prefer GlobalId for element proxy count — usually denser in index than Type.Defined.
                row.ElementCount = Count(searchService, part.PartId, TypeProperty.Defined(), maxHits, out truncated);
                row.IsTruncated = truncated;
                row.GlobalIdCount = Count(searchService, part.PartId, GlobalIdProperty.Defined(), maxHits, out truncated);
                row.IsTruncated = row.IsTruncated || truncated;
                if (row.ElementCount == 0 && row.GlobalIdCount > 0)
                    row.ElementCount = row.GlobalIdCount;
                row.WithoutGlobalIdCount = Math.Max(0, row.ElementCount - row.GlobalIdCount);
                row.GlobalIdFillPercent = row.ElementCount > 0
                    ? (double)row.GlobalIdCount / row.ElementCount * 100.0
                    : 0;

                if (withTypeBreakdown && row.ElementCount > 0)
                    row.TypeBreakdown = BuildTypeBreakdown(searchService, part, modelId, modelName, maxHits, row.ElementCount, ifcTypes);
                else
                    row.TypeBreakdown = new List<BimElementTypeCountRow>();

                row.Status = row.ElementCount > 0 ? CapabilityStatus.Available : CapabilityStatus.Partial;
                row.Notes = row.IsTruncated ? "Count capped at " + maxHits : null;
            }
            catch (Exception ex)
            {
                AnalyticsLogger.Warning("bim-index-part", part.PartId + " " + ex.Message);
                row.Status = CapabilityStatus.Error;
                row.Notes = ex.Message;
            }

            return row;
        }

        private List<BimElementTypeCountRow> BuildTypeBreakdown(
            IModelSearchService searchService,
            BimPartRef part,
            Guid modelId,
            string modelName,
            int maxHits,
            long totalElements,
            string[] ifcTypes)
        {
            var list = new List<BimElementTypeCountRow>();
            if (totalElements <= 0 || ifcTypes == null || ifcTypes.Length == 0)
                return list;

            long typedSum = 0;
            foreach (var ifcType in ifcTypes)
            {
                bool truncated;
                long count = Count(searchService, part.PartId, TypeProperty.EqualTo(ifcType), maxHits, out truncated);
                if (count <= 0)
                    continue;

                typedSum += count;
                list.Add(new BimElementTypeCountRow
                {
                    ModelId = modelId,
                    ModelName = modelName,
                    PartId = part.PartId,
                    PartName = part.PartName,
                    IfcType = ifcType,
                    Count = count,
                    IsEstimate = truncated,
                    SharePercent = (double)count / totalElements * 100.0
                });
            }

            list.Sort((a, b) => b.Count.CompareTo(a.Count));

            long other = totalElements - typedSum;
            if (other > 0)
            {
                list.Add(new BimElementTypeCountRow
                {
                    ModelId = modelId,
                    ModelName = modelName,
                    PartId = part.PartId,
                    PartName = part.PartName,
                    IfcType = "(other / untyped in index)",
                    Count = other,
                    SharePercent = (double)other / totalElements * 100.0
                });
            }

            return list;
        }

        private static int Count(
            IModelSearchService searchService,
            Guid partId,
            IModelSearchExpression elementExpression,
            int maxHits,
            out bool truncated)
        {
            truncated = false;
            var expressions = ModelSearchExpressionsBuilder.CreateBuilder()
                .AddExpression(elementExpression)
                .AddExpression(Property.ModelPartId.EqualTo(partId));

            var set = ModelSearchSetBuilder.CreateBuilder(SearchSetLogicalOperator.And)
                .AddExpressions(expressions)
                .Build();

            // Keep maxHits low: Search materializes hit list — large caps make Standard/Full hang.
            var hits = searchService.Search(set, maxHits);
            if (hits == null)
                return 0;

            int count;
            var list = hits as ICollection<IModelElementId>;
            if (list != null)
                count = list.Count;
            else
            {
                count = 0;
                foreach (var _ in hits)
                    count++;
            }

            truncated = count >= maxHits;
            return count;
        }

        private static void TryRegisterParts(IModelSearchService searchService, IEnumerable<Guid> partIds)
        {
            foreach (var partId in partIds)
            {
                try
                {
                    var task = searchService.AddModelPartAsync(partId);
                    if (task != null)
                        task.GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    AnalyticsLogger.Warning("bim-index-register", partId + " " + ex.Message);
                }
            }
        }

        private static int GetMaxHits(ScanMode mode)
        {
            // Caps are intentionally modest — API returns hit list, not Total.
            switch (mode)
            {
                case ScanMode.Fast:
                    return 2000;
                case ScanMode.Full:
                    return 50000;
                default:
                    return 10000;
            }
        }
    }
}
