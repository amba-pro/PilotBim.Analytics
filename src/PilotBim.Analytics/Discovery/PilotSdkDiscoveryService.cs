using System;
using System.Collections.Generic;
using System.Linq;
using Ascon.Pilot.SDK;
using PilotBim.Analytics.Diagnostics;
using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.Discovery
{
    internal sealed class PilotSdkDiscoveryService
    {
        public void PopulateStaticSdkInventory(ProjectInventoryReport report)
        {
            try
            {
                var sdk = typeof(IDataObject).Assembly;
                report.SdkVersion = sdk.GetName().Version != null
                    ? sdk.GetName().Version.ToString()
                    : "unknown";

                report.VerifiedApis.AddRange(new[]
                {
                    Member("IObjectsRepository.GetTypes", CapabilityStatus.Available, "Type metadata"),
                    Member("IObjectsRepository.SubscribeObjects", CapabilityStatus.Available, "Load objects by id"),
                    Member("IObjectsRepository.GetPerson / GetPeople", CapabilityStatus.Available, "Persons"),
                    Member("IObjectsRepository.GetOrganisationUnit / GetOrganisationUnits", CapabilityStatus.Available, "Org units"),
                    Member("ObjectRepositoryExtensions.GetUserStates", CapabilityStatus.Available, "User states catalog"),
                    Member("ObjectRepositoryExtensions.GetUserStateMachines", CapabilityStatus.Available, "State machines / transitions config"),
                    Member("ISearchService.GetObjectQueryBuilder / Search", CapabilityStatus.Available, "Object search + Total"),
                    Member("IQueryBuilder.Must / MaxResults / WithTypeFilter", CapabilityStatus.Available, "Query building"),
                    Member("ObjectFields.TypeId / CreatedDate / CreatorId / ParentId", CapabilityStatus.Available, "Searchable system fields"),
                    Member("IDataObject.Id / ParentId / Created / Creator / Type / Attributes", CapabilityStatus.Available, "Core object fields"),
                    Member("IDataObject.Children / Relations / Files", CapabilityStatus.Available, "Hierarchy / relations / files"),
                    Member("IDataObject.ObjectStateInfo", CapabilityStatus.Available, "Lifecycle ObjectState + state change date/person"),
                    Member("IDataObject.IsDeleted / IsInRecycleBin", CapabilityStatus.Available, "Lifecycle flags"),
                    Member("IDataObject.ActualFileSnapshot / PreviousFileSnapshots", CapabilityStatus.Available, "Document file versions"),
                    Member("IFile.Name / Size / Created / Modified", CapabilityStatus.Available, "File metadata"),
                    Member("FileExtensions.CreatorId(IFile)", CapabilityStatus.Available, "File creator"),
                    Member("IObjectsRepository.GetHistoryItems", CapabilityStatus.Available, "History items by id"),
                    Member("Ascon.Pilot.SDK.Data.IHistoryItem", CapabilityStatus.Available, "Created / CreatorId / Reason / Object"),
                    Member("DataObjectExtensions.HistoryItems(IDataObject)", CapabilityStatus.Available, "History item ids on object"),
                    Member("IAttribute.Name / Title / Type / IsObligatory / Configuration", CapabilityStatus.Available, "Attribute metadata"),
                    Member("AttributeType enum", CapabilityStatus.Available, "Integer..Boolean including UserState/OrgUnit"),
                    Member("IPerson.Id / DisplayName / MainPosition", CapabilityStatus.Available, "Person identity"),
                    Member("IOrganisationUnit.Id / Title / Children", CapabilityStatus.Available, "Org hierarchy children"),
                    Member("IUserState.Id / Name / Title", CapabilityStatus.Available, "States"),
                    Member("ITransitionManager / IUserStateMachine", CapabilityStatus.Available, "Possible transitions (config), not historical trail"),
                    Member("IMessagesRepository", CapabilityStatus.Available, "Chat messages by chat id"),
                    Member("Ascon.Pilot.Bim.SDK.TypeNames", CapabilityStatus.Available, "bim_coordinationModel / bim_modelPart / ..."),
                    Member("IModelStorageProvider.GetStorage", CapabilityStatus.Available, "BIM model storage"),
                    Member("IModelStorage.GetModelPartsIds / GetVersions / LoadElements / LoadElementProperties", CapabilityStatus.Available, "BIM elements + properties"),
                    Member("IModelElement / IModelElementId", CapabilityStatus.Available, "ElementId + ModelPartId"),
                    Member("Ascon.Pilot.Bim.Search.SDK.PropertyNames.GlobalId / GlobalIdReadable", CapabilityStatus.Available, "IFC GlobalId search properties"),
                    Member("IModelSearchManager / IModelSearchService", CapabilityStatus.Available, "BIM element search")
                });

                report.NotVerifiedApis.AddRange(new[]
                {
                    Member("IDataObject.Modified / LastModified", CapabilityStatus.NotExposedBySdk, "No ModifiedDate property on IDataObject"),
                    Member("Dedicated StateTransitionHistory API", CapabilityStatus.NotExposedBySdk, "No public state-transition audit trail type"),
                    Member("Dedicated AuditTrail API", CapabilityStatus.NotExposedBySdk, "Only IHistoryItem / change handlers"),
                    Member("IOrganisationUnit.ParentId", CapabilityStatus.NotExposed, "No ParentId property; parent inferred via Children walk"),
                    Member("Automatic Open/Closed state semantics", CapabilityStatus.NotExposed, "IUserState has no semantic open/closed flag")
                });

                report.RuntimeValidationApis.AddRange(new[]
                {
                    Member("ISearchResult.Total accuracy for all types", CapabilityStatus.NeedsRuntime, "Validate against sample databases"),
                    Member("GetHistoryItems content for state changes", CapabilityStatus.NeedsRuntime, "Whether Reason/Object captures UserState transitions"),
                    Member("IModelStorage.LoadElements cost", CapabilityStatus.NeedsRuntime, "May be heavy; use sample/probe only"),
                    Member("AttributeType.OrgUnit value shape", CapabilityStatus.NeedsRuntime, "int vs int[] of position ids"),
                    Member("bimObjectId attribute presence on remark types", CapabilityStatus.NeedsRuntime, "Config-dependent attribute name")
                });

                report.SystemFields.AddRange(BuildStaticSystemFieldMatrix());
                report.Framework = ".NET Framework 4.7.2";
                report.Architecture = "x86 host (Pilot-BIM), plugin AnyCPU";
                report.PilotVersion = TryFileVersion(@"C:\Program Files\ASCON\Pilot-BIM\Ascon.Pilot.PilotBIM.exe")
                    ?? TryFileVersion(@"C:\Program Files\ASCON\Pilot-BIM\Ascon.Pilot.SDK.dll")
                    ?? "25.9.0.55929 (expected)";
            }
            catch (Exception ex)
            {
                AnalyticsLogger.Error("sdk-discovery", ex);
                report.Errors.Add("SDK discovery: " + ex.Message);
            }
        }

        public List<SystemFieldCapability> BuildStaticSystemFieldMatrix()
        {
            return new List<SystemFieldCapability>
            {
                Field("ObjectId", "IDataObject.Id", CapabilityStatus.Available, null, "Guid"),
                Field("TypeId", "IDataObject.Type.Id / ObjectFields.TypeId", CapabilityStatus.Available, null, "Searchable"),
                Field("ParentId", "IDataObject.ParentId", CapabilityStatus.Available, null, "Hierarchy"),
                Field("StateId", "AttributeType.UserState attribute value + IUserState", CapabilityStatus.Partial, null, "Not a fixed system property; stored in typed attributes"),
                Field("ObjectState", "IDataObject.ObjectStateInfo.State", CapabilityStatus.Available, null, "Alive/InRecycleBin/DeletedPermanently/Frozen/..."),
                Field("CreatorId", "IDataObject.Creator.Id / ObjectFields.CreatorId", CapabilityStatus.Available, null, null),
                Field("CreatedDate", "IDataObject.Created / ObjectFields.CreatedDate", CapabilityStatus.Available, null, null),
                Field("ModifiedDate", "IDataObject.*", CapabilityStatus.NotExposed, null, "No LastModified on IDataObject; may approximate via IHistoryItem.Created / IFilesSnapshot.Created"),
                Field("Version", "IDataObject.PreviousFileSnapshots + ActualFileSnapshot", CapabilityStatus.Partial, null, "File snapshot versions available; object history via GetHistoryItems"),
                Field("IsDeleted", "IDataObject.ObjectStateInfo.State / IsInRecycleBin (IsDeleted deprecated)", CapabilityStatus.Available, null, "Prefer ObjectStateInfo.State"),
                Field("Files", "IDataObject.Files / ActualFileSnapshot.Files", CapabilityStatus.Available, null, "Metadata only"),
                Field("Children", "IDataObject.Children / TypesByChildren", CapabilityStatus.Available, null, "Lazy load recommended"),
                Field("RelatedObjects", "IDataObject.Relations", CapabilityStatus.Available, null, "ObjectRelationType enum"),
                Field("StateChangeDate", "IDataObject.ObjectStateInfo.Date", CapabilityStatus.Available, null, "Lifecycle state change, not UserState card status"),
                Field("StateChangePersonId", "IDataObject.ObjectStateInfo.PersonId", CapabilityStatus.Available, null, null)
            };
        }

        private static SdkMemberRecord Member(string member, string status, string notes)
        {
            return new SdkMemberRecord { Member = member, Status = status, Notes = notes };
        }

        private static SystemFieldCapability Field(string field, string source, string availability, string example, string notes)
        {
            return new SystemFieldCapability
            {
                Field = field,
                SdkSource = source,
                Availability = availability,
                Example = example,
                Notes = notes
            };
        }

        private static string TryFileVersion(string path)
        {
            try
            {
                if (!System.IO.File.Exists(path))
                    return null;
                return System.Diagnostics.FileVersionInfo.GetVersionInfo(path).FileVersion;
            }
            catch
            {
                return null;
            }
        }

        public static string TruncateConfig(string configuration, int max = 120)
        {
            if (string.IsNullOrWhiteSpace(configuration))
                return "";
            var text = configuration.Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (text.Length <= max)
                return text;
            return text.Substring(0, max) + "...";
        }
    }
}
