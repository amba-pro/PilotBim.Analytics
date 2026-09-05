using System;
using System.Collections.Generic;
using System.Linq;
using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.Services
{
    internal sealed class CapabilityMatrixService
    {
        public void Build(ProjectInventoryReport report)
        {
            var list = new List<AnalyticsCapability>();
            var dq = new List<AnalyticsCapability>();

            bool hasTypes = report.TypesDiscovered > 0;
            bool hasSearch = report.ZoneResults.Any(z => z.Zone == "Search" && z.Status == CapabilityStatus.Pass)
                || report.VerifiedApis.Any(a => a.Member.Contains("ISearchService") && a.Status == CapabilityStatus.Available);
            bool hasHierarchyInventory = report.ObjectsFound > 0
                || report.ZoneResults.Any(z => z.Zone == "Search" && z.Status == CapabilityStatus.Partial
                    && z.Message != null && z.Message.IndexOf("hierarchy", StringComparison.OrdinalIgnoreCase) >= 0);
            bool hasObjectCounts = hasSearch || hasHierarchyInventory;
            bool hasStates = report.StatesDiscovered > 0;
            bool hasPersons = report.PersonsResolved > 0;
            bool hasOrgs = report.OrganisationsResolved > 0;
            bool hasHistory = report.HistoryCapabilities.Any(h => h.Capability.StartsWith("Object change") && h.Availability == CapabilityStatus.Available);
            bool hasFileVersions = report.DocumentCapabilities.Any(d => d.Capability.Contains("Previous file") && d.Availability == CapabilityStatus.Available);
            bool hasBimModels = report.BimCapabilities.Any(b => b.Name == "Models count" && b.Availability == CapabilityStatus.Available);
            bool hasBimIndex = report.BimPartAnalytics != null && report.BimPartAnalytics.Any(p => p.ElementCount > 0);
            bool hasBimElements = hasBimIndex || report.BimCapabilities.Any(b => b.Name == "Elements" && (b.Availability == CapabilityStatus.Available || b.Availability == CapabilityStatus.Partial));
            bool hasCreated = report.SystemFields.Any(f => f.Field == "CreatedDate" && f.Availability == CapabilityStatus.Available);
            bool hasModified = report.SystemFields.Any(f => f.Field == "ModifiedDate" && f.Availability == CapabilityStatus.Available);
            bool orgUnitAttrs = report.AllAttributes.Any(a => a.ValueType == "OrgUnit");
            bool userStateAttrs = report.AllAttributes.Any(a => a.ValueType == "UserState");
            bool bimObjectAttr = report.AllAttributes.Any(a => string.Equals(a.Name, "bimObjectId", StringComparison.OrdinalIgnoreCase));

            list.Add(Metric("Количество объектов", "ObjectId + Search Total / hierarchy walk",
                hasObjectCounts ? CapabilityStatus.Available : CapabilityStatus.Partial,
                hasObjectCounts ? CapabilityStatus.Ready : CapabilityStatus.NeedsRuntime,
                hasSearch ? "ISearchResult.Total per type" : "Hierarchy walk Root→Children when Search unavailable"));
            list.Add(Metric("Объекты по типу", "TypeId", hasTypes ? CapabilityStatus.Available : CapabilityStatus.Blocked, hasTypes ? CapabilityStatus.Ready : CapabilityStatus.BlockedBySdk, null));
            list.Add(Metric("Объекты по статусу (UserState)", "UserState attribute", userStateAttrs ? CapabilityStatus.Available : CapabilityStatus.Partial, userStateAttrs ? CapabilityStatus.Ready : CapabilityStatus.NeedsMapping, "Attribute name depends on type config"));
            list.Add(Metric("Объекты по ответственному", "OrgUnit/Person attribute", orgUnitAttrs ? CapabilityStatus.Partial : CapabilityStatus.NotVerified, CapabilityStatus.NeedsMapping, "OrgUnit stores position ids; map to person via OrganisationUnitExtensions.Person"));
            list.Add(Metric("Объекты по создателю", "CreatorId", CapabilityStatus.Available, CapabilityStatus.Ready, "IDataObject.Creator"));
            list.Add(Metric("Объекты по дате создания", "CreatedDate", hasCreated ? CapabilityStatus.Available : CapabilityStatus.NotExposed, hasCreated ? CapabilityStatus.Ready : CapabilityStatus.Blocked, null));
            list.Add(Metric("Средний срок закрытия", "CreatedDate + ClosedDate/state transition history", CapabilityStatus.NotAvailable, CapabilityStatus.Blocked, "No dedicated ClosedDate; state transition history not exposed as first-class API"));
            list.Add(Metric("Время реакции / SLA", "State transition timestamps + author", CapabilityStatus.Partial, CapabilityStatus.NeedsRuntime, "May reconstruct from IHistoryItem if snapshots capture UserState"));
            list.Add(Metric("Количество возвратов / повторное открытие", "State transition history", CapabilityStatus.NotExposedBySdk, CapabilityStatus.Blocked, "Dedicated transition audit not exposed"));
            list.Add(Metric("Активность пользователей", "IHistoryItem.CreatorId + Created", hasHistory ? CapabilityStatus.Partial : CapabilityStatus.NotVerified, hasHistory ? CapabilityStatus.NeedsRuntime : CapabilityStatus.Blocked, "History available; activity metrics need aggregation design"));
            list.Add(Metric("Итерации документа", "PreviousFileSnapshots count", hasFileVersions ? CapabilityStatus.Available : CapabilityStatus.Partial, hasFileVersions ? CapabilityStatus.Ready : CapabilityStatus.NeedsRuntime, null));
            list.Add(Metric("Замечания по типам", "Remark type attribute", CapabilityStatus.Partial, CapabilityStatus.NeedsMapping, "Discover remark types dynamically from GetTypes(); do not hardcode"));
            list.Add(Metric("Замечания по модели", "bimObjectId → Model", bimObjectAttr ? CapabilityStatus.Partial : CapabilityStatus.NeedsRuntime, CapabilityStatus.NeedsRuntime, "Attribute presence confirmed in sample attributes=" + bimObjectAttr));
            list.Add(Metric("Замечаний / 1000 элементов", "Remark→Model + ElementCount", hasBimIndex ? CapabilityStatus.Partial : CapabilityStatus.NotVerified, hasBimIndex ? CapabilityStatus.NeedsRuntime : CapabilityStatus.Blocked, hasBimIndex ? "Element count from search index" : "Index analytics required"));
            list.Add(Metric("Количество BIM моделей", "TypeNames.CoordinationModel", hasBimModels ? CapabilityStatus.Available : CapabilityStatus.Partial, hasBimModels ? CapabilityStatus.Ready : CapabilityStatus.NeedsRuntime, null));
            list.Add(Metric("Количество частей моделей", "TypeNames.ModelPart", report.BimModelPartsCount >= 0 ? CapabilityStatus.Available : CapabilityStatus.Partial, CapabilityStatus.Ready, null));
            list.Add(Metric("Количество BIM элементов", "IModelSearchService index", hasBimIndex ? CapabilityStatus.Available : CapabilityStatus.Partial, hasBimIndex ? CapabilityStatus.Ready : CapabilityStatus.NeedsRuntime, "No viewer required"));
            list.Add(Metric("Распределение элементов по типам", "IModelSearchService Property.Type", hasBimIndex ? CapabilityStatus.Available : CapabilityStatus.Partial, hasBimIndex ? CapabilityStatus.Ready : CapabilityStatus.NeedsRuntime, "Known IFC types + (other)"));
            list.Add(Metric("Полнота атрибутирования BIM", "LoadElementProperties sample", hasBimElements ? CapabilityStatus.Partial : CapabilityStatus.NotVerified, CapabilityStatus.NeedsRuntime, null));
            list.Add(Metric("Иерархия проекта", "ParentId / Children", CapabilityStatus.Available, CapabilityStatus.Ready, "Lazy tree only"));
            list.Add(Metric("Подрядчики / организации", "IOrganisationUnit + OrgUnit attrs", hasOrgs ? CapabilityStatus.Available : CapabilityStatus.Partial, hasOrgs ? CapabilityStatus.Ready : CapabilityStatus.NeedsRuntime, null));
            list.Add(Metric("Пользователи", "IPerson", hasPersons ? CapabilityStatus.Available : CapabilityStatus.Partial, hasPersons ? CapabilityStatus.Ready : CapabilityStatus.NeedsRuntime, "Minimal identity only"));

            dq.Add(Metric("Заполненность атрибута", "Sampled attribute values", CapabilityStatus.Available, CapabilityStatus.Ready, "FillRate on sample"));
            dq.Add(Metric("Обязательные поля", "IAttribute.IsObligatory", CapabilityStatus.Available, CapabilityStatus.Ready, null));
            dq.Add(Metric("Пустые значения", "null/empty attribute values", CapabilityStatus.Available, CapabilityStatus.Ready, null));
            dq.Add(Metric("Неизвестные references", "OrgUnit/UserState ids vs catalogs", CapabilityStatus.Partial, CapabilityStatus.NeedsRuntime, null));
            dq.Add(Metric("Broken person references", "OrgUnit.Person / GetPerson", CapabilityStatus.Partial, CapabilityStatus.NeedsRuntime, null));
            dq.Add(Metric("Broken organisation references", "GetOrganisationUnit", CapabilityStatus.Partial, CapabilityStatus.NeedsRuntime, null));
            dq.Add(Metric("Broken BIM relations", "bimObjectId resolve", CapabilityStatus.Partial, CapabilityStatus.NeedsRuntime, null));
            dq.Add(Metric("Объекты без UserState", "UserState attributes empty", userStateAttrs ? CapabilityStatus.Available : CapabilityStatus.Partial, CapabilityStatus.Ready, null));
            dq.Add(Metric("BIM элементы без GlobalId", "PropertyNames.GlobalId index", hasBimIndex ? CapabilityStatus.Available : CapabilityStatus.Partial, hasBimIndex ? CapabilityStatus.Ready : CapabilityStatus.NeedsRuntime, hasBimIndex ? "From search index per part" : "Sample probe only"));
            dq.Add(Metric("BIM элементы без свойств", "LoadElementProperties count=0", hasBimElements ? CapabilityStatus.Partial : CapabilityStatus.NotVerified, CapabilityStatus.NeedsRuntime, null));
            dq.Add(Metric("ModifiedDate completeness", "IDataObject.Modified", CapabilityStatus.NotExposed, CapabilityStatus.Blocked, "Field not on IDataObject"));

            report.AnalyticsCapabilities = list;
            report.DataQualityCapabilities = dq;

            if (list.Any(m => m.Status == CapabilityStatus.Blocked || m.Status == CapabilityStatus.BlockedBySdk) &&
                list.Count(m => m.Status == CapabilityStatus.Ready) > 5)
            {
                report.AnalyticsReadiness = CapabilityStatus.Partial;
            }
            else if (list.Count(m => m.Status == CapabilityStatus.Ready) > 0)
            {
                report.AnalyticsReadiness = CapabilityStatus.Partial;
            }
            else
            {
                report.AnalyticsReadiness = CapabilityStatus.Blocked;
            }

            // Overall: inventory itself ready for runtime when types discovered.
            if (hasTypes && !report.Cancelled)
                report.FinalStatus = "PARTIAL_RUNTIME_INVENTORY";
            else if (report.Cancelled)
                report.FinalStatus = "PARTIAL_RUNTIME_INVENTORY";
            else
                report.FinalStatus = "BLOCKED_BY_SDK";
        }

        private static AnalyticsCapability Metric(string metric, string required, string availability, string status, string notes)
        {
            return new AnalyticsCapability
            {
                Metric = metric,
                RequiredData = required,
                Availability = availability,
                Status = status,
                Notes = notes
            };
        }
    }
}
