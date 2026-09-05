using System;
using System.Collections.Generic;
using System.Linq;
using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.Services
{
    internal sealed class ProjectAnalyticsService
    {
        public ProjectAnalyticsSnapshot Build(ProjectInventoryReport report)
        {
            if (report == null)
                throw new ArgumentNullException(nameof(report));

            var snapshot = new ProjectAnalyticsSnapshot
            {
                GeneratedAt = report.GeneratedAt,
                ScanMode = report.ScanMode,
                DataSource = DescribeDataSource(report)
            };

            var totalObjects = Math.Max(report.ObjectsFound, 0);
            var typesWithObjects = report.Types.Count(t => t.ObjectCount > 0);
            var avgFill = report.Types.Where(t => t.SampledCount > 0).Select(t => t.FillPercent).DefaultIfEmpty(0).Average();
            var indexedElements = report.BimPartAnalytics != null
                ? report.BimPartAnalytics.Sum(p => p.ElementCount)
                : 0L;
            var indexTruncated = report.BimPartAnalytics != null && report.BimPartAnalytics.Any(p => p.IsTruncated);

            snapshot.Summary.Add(Kpi("Всего объектов", FormatCount(totalObjects, report), "Сумма по типам (search или hierarchy walk)"));
            snapshot.Summary.Add(Kpi("Типов карточек", report.TypesDiscovered.ToString(), "types with objects: " + typesWithObjects));
            snapshot.Summary.Add(Kpi("BIM моделей", report.BimModelsCount.ToString(), null));
            snapshot.Summary.Add(Kpi("Частей моделей", report.BimModelPartsCount.ToString(), null));
            snapshot.Summary.Add(Kpi("BIM элементов (индекс)", FormatIndexedCount(indexedElements, indexTruncated), "IModelSearchService, без открытия 3D"));
            snapshot.Summary.Add(Kpi("Пользователей", report.PersonsResolved.ToString(), "каталог IPerson"));
            snapshot.Summary.Add(Kpi("Организаций", report.OrganisationsResolved.ToString(), "каталог IOrganisationUnit"));
            snapshot.Summary.Add(Kpi("Средний fill %", avgFill.ToString("0.0") + "%", "по сэмплам типов"));
            snapshot.Summary.Add(Kpi("Готовность аналитики", report.AnalyticsReadiness, report.FinalStatus));

            if (report.RemarkAnalytics != null && report.RemarkAnalytics.RemarksPer1000Elements > 0)
                snapshot.Summary.Add(Kpi("Замечаний / 1000 элементов",
                    report.RemarkAnalytics.RemarksPer1000Elements.ToString("0.0"),
                    "remarks total / BIM index elements × 1000"));

            snapshot.ObjectsByType = BuildTypeDistribution(report, totalObjects);
            snapshot.ObjectsByCreator = BuildCreatorDistribution(report);
            snapshot.ObjectsByCreatedMonth = BuildCreatedMonthDistribution(report);
            snapshot.ObjectsByUserState = BuildUserStateDistribution(report);
            snapshot.DocumentVersions = BuildDocumentVersions(report);
            snapshot.BimSummary = BuildBimSummary(report);
            snapshot.BimModelAnalytics = report.BimModelAnalytics != null
                ? report.BimModelAnalytics.ToList()
                : new List<BimModelAnalyticsRow>();
            snapshot.BimPartAnalytics = report.BimPartAnalytics != null
                ? report.BimPartAnalytics.ToList()
                : new List<BimPartAnalyticsRow>();
            snapshot.BimElementTypeCounts = report.BimElementTypeCounts != null
                ? report.BimElementTypeCounts.ToList()
                : new List<BimElementTypeCountRow>();
            snapshot.DataQuality = BuildDataQuality(report);
            snapshot.ModelRemarks = BuildModelRemarks(report);
            snapshot.RemarkLinks = report.RemarkLinks != null ? report.RemarkLinks.ToList() : new List<RemarkLinkRow>();
            snapshot.RemarkAnalytics = report.RemarkAnalytics;
            snapshot.ObjectsByResponsible = BuildResponsibleDistribution(report);
            snapshot.ObjectsByUserStateSemantic = BuildStateSemanticDistribution(report);
            snapshot.Limitations = BuildLimitations(report);
            snapshot.Notes = string.Join(Environment.NewLine, snapshot.Limitations.Take(3));

            return snapshot;
        }

        private static string DescribeDataSource(ProjectInventoryReport report)
        {
            var searchZone = report.ZoneResults.FirstOrDefault(z => z.Zone == "Search");
            if (searchZone != null && searchZone.Status == CapabilityStatus.Pass)
                return "ISearchService";
            if (searchZone != null && searchZone.Message != null && searchZone.Message.IndexOf("hierarchy", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Hierarchy walk";
            return "Inventory scan";
        }

        private static List<TypeCountRow> BuildTypeDistribution(ProjectInventoryReport report, long totalObjects)
        {
            var rows = report.Types
                .Where(t => t.ObjectCount > 0)
                .OrderByDescending(t => t.ObjectCount)
                .Select(t => new TypeCountRow
                {
                    TypeId = t.TypeId,
                    TypeName = t.Title ?? t.Name,
                    Count = t.ObjectCount,
                    IsEstimate = t.ObjectCountIsEstimate,
                    SharePercent = totalObjects > 0 ? (double)t.ObjectCount / totalObjects * 100.0 : 0,
                    FillPercent = t.FillPercent
                })
                .ToList();
            return rows;
        }

        private static List<CreatorCountRow> BuildCreatorDistribution(ProjectInventoryReport report)
        {
            var personMap = report.Persons.ToDictionary(p => p.PersonId, p => p.DisplayName);
            var counts = report.CreatorCountsFromFullScan && report.CreatorFullCounts.Count > 0
                ? report.CreatorFullCounts
                : report.CreatorSampleCounts;
            var total = report.CreatorCountsFromFullScan && report.CreatorFullScanObjects > 0
                ? Math.Max(report.CreatorFullScanObjects, 1)
                : Math.Max(report.CreatorSampleTotal, 1);
            var scope = report.CreatorCountsFromFullScan
                ? "search aggregation (" + report.CreatorFullScanObjects + " objects)"
                : "sampled objects (" + report.CreatorSampleTotal + ")";

            return counts
                .OrderByDescending(p => p.Value)
                .Select(pair =>
                {
                    string name;
                    if (!personMap.TryGetValue(pair.Key, out name))
                        name = "PersonId=" + pair.Key;
                    return new CreatorCountRow
                    {
                        CreatorId = pair.Key,
                        DisplayName = name,
                        SampledCount = pair.Value,
                        SharePercent = (double)pair.Value / total * 100.0,
                        Scope = scope
                    };
                })
                .ToList();
        }

        private static List<PeriodCountRow> BuildCreatedMonthDistribution(ProjectInventoryReport report)
        {
            var counts = report.CreatorCountsFromFullScan && report.CreatedMonthFullCounts.Count > 0
                ? report.CreatedMonthFullCounts
                : report.CreatedMonthSampleCounts;
            var total = counts.Values.Sum();
            if (total <= 0)
                total = 1;

            return counts
                .OrderBy(p => p.Key)
                .Select(pair => new PeriodCountRow
                {
                    Period = pair.Key,
                    Count = pair.Value,
                    SharePercent = (double)pair.Value / total * 100.0
                })
                .ToList();
        }

        private static List<StateCountRow> BuildUserStateDistribution(ProjectInventoryReport report)
        {
            var stateTitles = report.States.ToDictionary(s => s.StateId, s => s.Title ?? s.Name);
            var counts = new Dictionary<Guid, int>();

            foreach (var type in report.Types)
            {
                if (type.ObservedStateCounts == null)
                    continue;
                foreach (var pair in type.ObservedStateCounts)
                {
                    if (counts.ContainsKey(pair.Key))
                        counts[pair.Key] += pair.Value;
                    else
                        counts[pair.Key] = pair.Value;
                }
            }

            var total = Math.Max(counts.Values.Sum(), 1);
            return counts
                .OrderByDescending(p => p.Value)
                .Select(pair =>
                {
                    string title;
                    if (!stateTitles.TryGetValue(pair.Key, out title))
                        title = pair.Key.ToString();
                    return new StateCountRow
                    {
                        StateId = pair.Key,
                        StateTitle = title,
                        Count = pair.Value,
                        SharePercent = (double)pair.Value / total * 100.0,
                        Scope = "UserState attributes on sampled objects"
                    };
                })
                .ToList();
        }

        private static List<DocumentVersionRow> BuildDocumentVersions(ProjectInventoryReport report)
        {
            return report.DocumentSamples
                .OrderByDescending(d => d.PreviousSnapshotCount)
                .Select(d => new DocumentVersionRow
                {
                    ObjectId = d.ObjectId,
                    DisplayName = d.DisplayName,
                    TypeName = d.TypeName,
                    FileCount = d.FileCount,
                    PreviousSnapshotCount = d.PreviousSnapshotCount,
                    TotalIterations = d.PreviousSnapshotCount + 1
                })
                .ToList();
        }

        private static List<AnalyticsKpiRow> BuildBimSummary(ProjectInventoryReport report)
        {
            var elementSampleCount = report.BimElementSamples != null ? report.BimElementSamples.Count : 0;
            var indexedElements = report.BimPartAnalytics != null
                ? report.BimPartAnalytics.Sum(p => p.ElementCount)
                : 0L;
            var indexedGlobalIds = report.BimPartAnalytics != null
                ? report.BimPartAnalytics.Sum(p => p.GlobalIdCount)
                : 0L;
            var indexTruncated = report.BimPartAnalytics != null && report.BimPartAnalytics.Any(p => p.IsTruncated);
            var remarkCount = report.Types
                .Where(IsRemarkType)
                .Sum(t => t.ObjectCount > 0 ? t.ObjectCount : 0);

            var list = new List<AnalyticsKpiRow>
            {
                Kpi("Моделей в проекте", report.BimModelsCount.ToString(), "CoordinationModel"),
                Kpi("Частей моделей", report.BimModelPartsCount.ToString(), "ModelPart"),
                Kpi("Элементов (индекс .bm)", FormatIndexedCount(indexedElements, indexTruncated), "IModelSearchService, viewer не нужен"),
                Kpi("GlobalId в индексе", indexedGlobalIds.ToString(),
                    indexedElements > 0 ? (indexedGlobalIds * 100.0 / indexedElements).ToString("0.0") + "% от элементов" : null),
                Kpi("Элементов в сэмпле storage", elementSampleCount.ToString(), "только если модель открыта в viewer"),
                Kpi("Замечаний к модели", remarkCount.ToString(), "типы issue/remark"),
            };

            if (report.RemarkAnalytics != null)
            {
                list.Add(Kpi("Замечаний / 1000 элементов",
                    report.RemarkAnalytics.RemarksPer1000Elements.ToString("0.0"),
                    "indexedElements=" + report.RemarkAnalytics.IndexedElements));
                list.Add(Kpi("bimObjectId → index",
                    report.RemarkAnalytics.ResolvedInIndex + " / " + report.RemarkAnalytics.WithBimObjectId,
                    report.RemarkAnalytics.Scope));
            }

            list.Add(Kpi("Версия BIM SDK", string.IsNullOrWhiteSpace(report.BimSdkVersion) ? "—" : report.BimSdkVersion, null));

            foreach (var cap in report.BimCapabilities)
            {
                if (cap == null || string.IsNullOrEmpty(cap.Name))
                    continue;
                if (cap.Availability == CapabilityStatus.Available)
                    continue;
                if (IsTechnicalBimCapability(cap.Name))
                    continue;

                list.Add(Kpi(TranslateBimLabel(cap.Name), TranslateStatus(cap.Availability), cap.Notes));
            }

            return list;
        }

        private static List<ResponsibleCountRow> BuildResponsibleDistribution(ProjectInventoryReport report)
        {
            var orgMap = report.Organisations.ToDictionary(o => o.OrganisationId, o => o.Name);
            var total = Math.Max(report.ResponsibleSampleCounts.Values.Sum(), 1);

            return report.ResponsibleSampleCounts
                .OrderByDescending(p => p.Value)
                .Select(pair =>
                {
                    string name;
                    if (!orgMap.TryGetValue(pair.Key, out name))
                        name = "OrgUnitId=" + pair.Key;
                    return new ResponsibleCountRow
                    {
                        OrgUnitId = pair.Key,
                        DisplayName = name,
                        SampledCount = pair.Value,
                        SharePercent = (double)pair.Value / total * 100.0,
                        Scope = "OrgUnit attributes on sampled objects"
                    };
                })
                .ToList();
        }

        private static List<StateSemanticCountRow> BuildStateSemanticDistribution(ProjectInventoryReport report)
        {
            var stateMap = report.States.ToDictionary(s => s.StateId, s => s);
            var counts = new Dictionary<Guid, int>();

            foreach (var type in report.Types)
            {
                if (type.ObservedStateCounts == null)
                    continue;
                foreach (var pair in type.ObservedStateCounts)
                {
                    if (counts.ContainsKey(pair.Key))
                        counts[pair.Key] += pair.Value;
                    else
                        counts[pair.Key] = pair.Value;
                }
            }

            var semanticGroups = new Dictionary<string, int>();
            var titles = new Dictionary<string, string>();
            foreach (var pair in counts)
            {
                StateInventoryRecord state;
                var semantic = stateMap.TryGetValue(pair.Key, out state) ? state.SemanticStatus : CapabilityStatus.Unknown;
                var title = state != null ? (state.Title ?? state.Name) : pair.Key.ToString();
                if (semanticGroups.ContainsKey(semantic))
                    semanticGroups[semantic] += pair.Value;
                else
                    semanticGroups[semantic] = pair.Value;
                if (!titles.ContainsKey(semantic))
                    titles[semantic] = title;
            }

            var total = Math.Max(semanticGroups.Values.Sum(), 1);
            return semanticGroups
                .OrderByDescending(p => p.Value)
                .Select(pair => new StateSemanticCountRow
                {
                    StateId = Guid.Empty,
                    StateTitle = titles.ContainsKey(pair.Key) ? titles[pair.Key] : pair.Key,
                    Semantic = pair.Key,
                    Count = pair.Value,
                    SharePercent = (double)pair.Value / total * 100.0,
                    Scope = "inferred OPEN/CLOSED/REJECTED from state titles"
                })
                .ToList();
        }

        private static bool IsTechnicalBimCapability(string name)
        {
            if (string.IsNullOrEmpty(name))
                return true;
            if (name == "Models count" || name == "Model parts count")
                return true;
            if (name.StartsWith("TypeNames.", StringComparison.Ordinal))
                return true;
            if (name.IndexOf("SDK", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (name.IndexOf("property", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (name.IndexOf("IModel", StringComparison.Ordinal) >= 0)
                return true;
            if (name.IndexOf("GlobalId", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            return false;
        }

        private static string TranslateBimLabel(string name)
        {
            switch (name)
            {
                case "Elements sample": return "Сэмпл элементов";
                case "Elements": return "Элементы модели";
                case "bimObjectId attribute": return "Атрибут bimObjectId";
                case "Remark → element relation": return "Связь замечание → элемент";
                case "BIM discovery": return "Ошибка BIM discovery";
                default: return name;
            }
        }

        private static string TranslateStatus(string status)
        {
            switch (status)
            {
                case CapabilityStatus.Available: return "Доступно";
                case CapabilityStatus.Partial: return "Частично";
                case CapabilityStatus.Blocked: return "Заблокировано";
                case CapabilityStatus.Error: return "Ошибка";
                case CapabilityStatus.NotVerified: return "Не проверено";
                case CapabilityStatus.NeedsRuntime: return "Нужна проверка";
                default: return status;
            }
        }

        private static List<AttributeQualityRow> BuildDataQuality(ProjectInventoryReport report)
        {
            return report.AllAttributes
                .Where(a => a.SampledCount > 0)
                .OrderBy(a => a.FillRate)
                .ThenByDescending(a => a.IsObligatory)
                .Take(50)
                .Select(a => new AttributeQualityRow
                {
                    TypeName = a.TypeName,
                    AttributeTitle = a.Title ?? a.Name,
                    IsObligatory = a.IsObligatory,
                    FillRate = a.FillRate,
                    SampledCount = a.SampledCount,
                    Status = a.PopulationStatus
                })
                .ToList();
        }

        private static List<RemarkTypeRow> BuildModelRemarks(ProjectInventoryReport report)
        {
            var remarkTypes = report.Types
                .Where(t => IsRemarkType(t))
                .ToList();

            var rows = new List<RemarkTypeRow>();
            foreach (var type in remarkTypes)
            {
                var bimAttr = type.Attributes.FirstOrDefault(a =>
                    string.Equals(a.Name, "bimObjectId", StringComparison.OrdinalIgnoreCase));

                rows.Add(new RemarkTypeRow
                {
                    TypeId = type.TypeId,
                    TypeName = type.Title ?? type.Name,
                    ObjectCount = type.ObjectCount,
                    BimObjectIdFillRate = bimAttr != null ? bimAttr.FillRate : 0,
                    MappingStatus = ResolveRemarkMappingStatus(report, type.TypeId),
                    Notes = BuildRemarkNotes(report, type, bimAttr)
                });
            }

            return rows.OrderByDescending(r => r.ObjectCount).ToList();
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

        private static string ResolveRemarkMappingStatus(ProjectInventoryReport report, int typeId)
        {
            if (report.RemarkLinks == null || report.RemarkLinks.Count == 0)
                return CapabilityStatus.NeedsMapping;

            var links = report.RemarkLinks.Where(r => r.TypeName != null).ToList();
            if (links.Count == 0)
                return CapabilityStatus.NeedsMapping;

            var resolved = links.Count(r => r.ResolvedInIndex);
            var withId = links.Count(r => !string.IsNullOrWhiteSpace(r.BimObjectId));
            if (withId > 0 && resolved == withId)
                return CapabilityStatus.Ready;
            if (resolved > 0)
                return CapabilityStatus.Partial;
            return CapabilityStatus.NeedsMapping;
        }

        private static string BuildRemarkNotes(
            ProjectInventoryReport report,
            TypeInventoryRecord type,
            AttributeInventoryRecord bimAttr)
        {
            if (bimAttr == null)
                return "bimObjectId attribute not found on type";

            var fill = "bimObjectId fill on sample=" + (bimAttr.FillRate * 100).ToString("0") + "%";
            if (report.RemarkAnalytics != null && report.RemarkAnalytics.SampledRemarks > 0)
                return fill + "; index resolved=" + report.RemarkAnalytics.ResolvedInIndex
                    + "/" + report.RemarkAnalytics.WithBimObjectId;
            return fill + "; run scan for index validation";
        }

        private static List<string> BuildLimitations(ProjectInventoryReport report)
        {
            var creatorNote = report.CreatorCountsFromFullScan
                ? "Создатель/дата — search-агрегация по " + report.CreatorFullScanObjects + " объектам (бюджет режима)."
                : "Создатель и дата создания — по сэмплированным объектам.";

            var list = new List<string>
            {
                "Read-only: без изменений данных Pilot.",
                creatorNote,
                "UserState OPEN/CLOSED — эвристика по названию; можно переопределить в %LOCALAPPDATA%\\PilotBim.Analytics\\state-mapping.json",
                "Ответственный — OrgUnit id из сэмпла; Person через должность требует доп. маппинга.",
                "ModifiedDate на IDataObject недоступен — метрики по изменению заблокированы.",
                "Счётчики BIM-элементов — через поисковый индекс (.bm); при превышении лимита режима значение помечено ~."
            };

            if (report.Types.Any(t => t.ObjectCountIsEstimate))
                list.Insert(1, "Часть счётчиков объектов — оценка (hierarchy walk truncated или search partial).");

            return list;
        }

        private static AnalyticsKpiRow Kpi(string label, string value, string detail)
        {
            return new AnalyticsKpiRow { Label = label, Value = value, Detail = detail };
        }

        private static string FormatCount(long count, ProjectInventoryReport report)
        {
            if (count < 0)
                return "n/a";
            var anyEstimate = report.Types.Any(t => t.ObjectCountIsEstimate);
            return anyEstimate ? count + " (~)" : count.ToString();
        }

        private static string FormatIndexedCount(long count, bool truncated)
        {
            if (count <= 0)
                return "0";
            return truncated ? count + " ~" : count.ToString();
        }
    }
}
