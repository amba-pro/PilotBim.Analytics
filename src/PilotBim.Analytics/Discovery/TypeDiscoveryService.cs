using System;
using System.Collections.Generic;
using System.Linq;
using Ascon.Pilot.SDK;
using PilotBim.Analytics.Data;
using PilotBim.Analytics.Diagnostics;
using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.Discovery
{
    internal sealed class TypeDiscoveryService
    {
        private readonly IObjectsRepository _repository;

        public TypeDiscoveryService(IObjectsRepository repository)
        {
            _repository = repository;
        }

        public List<TypeInventoryRecord> DiscoverTypes()
        {
            var list = new List<TypeInventoryRecord>();
            try
            {
                foreach (var type in _repository.GetTypes().OrderBy(t => t.Title ?? t.Name))
                {
                    if (type == null)
                        continue;

                    var record = new TypeInventoryRecord
                    {
                        TypeId = type.Id,
                        Name = type.Name,
                        Title = string.IsNullOrWhiteSpace(type.Title) ? type.Name : type.Title,
                        Kind = type.Kind.ToString(),
                        IsService = type.IsService,
                        IsDeleted = type.IsDeleted,
                        HasFiles = type.HasFiles,
                        AttributeCount = type.Attributes != null ? type.Attributes.Count : 0,
                        Status = CapabilityStatus.Available
                    };
                    list.Add(record);
                }
            }
            catch (Exception ex)
            {
                AnalyticsLogger.Error("type-discovery", ex);
                throw;
            }

            return list;
        }
    }

    internal sealed class AttributeDiscoveryService
    {
        public List<AttributeInventoryRecord> BuildAttributeShells(IType type)
        {
            var result = new List<AttributeInventoryRecord>();
            if (type == null || type.Attributes == null)
                return result;

            foreach (var attr in type.Attributes)
            {
                if (attr == null)
                    continue;

                var record = new AttributeInventoryRecord
                {
                    TypeId = type.Id,
                    TypeName = string.IsNullOrWhiteSpace(type.Title) ? type.Name : type.Title,
                    AttributeId = attr.Name,
                    Name = attr.Name,
                    Title = string.IsNullOrWhiteSpace(attr.Title) ? attr.Name : attr.Title,
                    ValueType = attr.Type.ToString(),
                    IsObligatory = attr.IsObligatory,
                    IsService = attr.IsService,
                    ConfigurationSummary = PilotSdkDiscoveryService.TruncateConfig(AttributeExtensions.Configuration2(attr)),
                    PopulationStatus = AttributePopulationStatus.Empty,
                    ReferenceKind = MapReferenceKind(attr.Type)
                };
                result.Add(record);
            }

            return result;
        }

        public void ProfileObject(IDataObject obj, IList<AttributeInventoryRecord> attributes, TypeInventoryRecord typeRecord)
        {
            if (obj == null || attributes == null)
                return;

            foreach (var attr in attributes)
            {
                attr.SampledCount++;
                object value = null;
                var readable = true;
                try
                {
                    if (obj.Attributes != null && obj.Attributes.ContainsKey(attr.Name))
                        value = obj.Attributes[attr.Name];
                }
                catch (Exception)
                {
                    readable = false;
                    attr.PopulationStatus = AttributePopulationStatus.Unreadable;
                }

                if (!readable)
                {
                    attr.EmptyCount++;
                    continue;
                }

                if (ReferenceResolver.IsEmptyValue(value))
                {
                    attr.EmptyCount++;
                }
                else
                {
                    attr.PopulatedCount++;
                    var sample = ReferenceResolver.SafeSampleString(value);
                    if (!string.IsNullOrEmpty(sample)
                        && attr.SampleValues.Count < 5
                        && !attr.SampleValues.Contains(sample))
                    {
                        attr.SampleValues.Add(sample);
                    }

                    CollectReferences(attr, typeRecord, value);
                }
            }
        }

        public void FinalizeAttribute(AttributeInventoryRecord attr)
        {
            if (attr.SampledCount <= 0)
            {
                attr.FillRate = 0;
                attr.PopulationStatus = AttributePopulationStatus.Empty;
                return;
            }

            attr.FillRate = (double)attr.PopulatedCount / attr.SampledCount;
            if (attr.PopulationStatus == AttributePopulationStatus.Unreadable)
                return;

            if (attr.PopulatedCount == 0)
                attr.PopulationStatus = AttributePopulationStatus.Empty;
            else if (attr.EmptyCount == 0)
                attr.PopulationStatus = AttributePopulationStatus.Available;
            else
                attr.PopulationStatus = AttributePopulationStatus.PartiallyPopulated;
        }

        private static void CollectReferences(AttributeInventoryRecord attr, TypeInventoryRecord typeRecord, object value)
        {
            if (attr.ValueType == AttributeType.UserState.ToString())
            {
                Guid stateId;
                if (ReferenceResolver.TryGetGuid(value, out stateId))
                {
                    attr.DistinctReferenceCount++;
                    if (typeRecord.ObservedStateCounts.ContainsKey(stateId))
                        typeRecord.ObservedStateCounts[stateId]++;
                    else
                        typeRecord.ObservedStateCounts[stateId] = 1;
                }
                return;
            }

            if (attr.ValueType == AttributeType.OrgUnit.ToString())
            {
                var ids = ReferenceResolver.EnumerateIntIds(value).Distinct().ToList();
                attr.DistinctReferenceCount += ids.Count;
                if (!typeRecord.OrgAttributeHits.ContainsKey(attr.Name))
                    typeRecord.OrgAttributeHits[attr.Name] = 0;
                typeRecord.OrgAttributeHits[attr.Name] += ids.Count;

                // OrgUnit attributes store organisation unit / position ids.
                // Person linkage is resolved later via OrganisationUnitExtensions.Person when available.
                return;
            }
        }

        private static string MapReferenceKind(AttributeType type)
        {
            return type.ToString();
        }
    }

    internal sealed class SystemFieldDiscoveryService
    {
        public void EnrichFromSample(ProjectInventoryReport report, IEnumerable<IDataObject> samples)
        {
            if (report.SystemFields == null || report.SystemFields.Count == 0)
                report.SystemFields = new PilotSdkDiscoveryService().BuildStaticSystemFieldMatrix();

            var first = samples != null ? samples.FirstOrDefault(o => o != null && o.State == DataState.Loaded) : null;
            if (first == null)
                return;

            SetExample(report, "ObjectId", first.Id.ToString());
            SetExample(report, "TypeId", first.Type != null ? first.Type.Id.ToString() : null);
            SetExample(report, "ParentId", first.ParentId.ToString());
            SetExample(report, "CreatorId", first.Creator != null ? first.Creator.Id.ToString() : null);
            SetExample(report, "CreatedDate", first.Created.ToString("o"));
            SetExample(report, "IsDeleted",
                first.ObjectStateInfo != null
                    ? first.ObjectStateInfo.State.ToString()
                    : null);
            SetExample(report, "Files", first.Files != null ? first.Files.Count.ToString() : "0");
            SetExample(report, "Children", first.Children != null ? first.Children.Count.ToString() : "0");
            SetExample(report, "RelatedObjects", first.Relations != null ? first.Relations.Count.ToString() : "0");
            if (first.ObjectStateInfo != null)
            {
                SetExample(report, "ObjectState", first.ObjectStateInfo.State.ToString());
                SetExample(report, "StateChangeDate", first.ObjectStateInfo.Date.ToString("o"));
                SetExample(report, "StateChangePersonId", first.ObjectStateInfo.PersonId.ToString());
            }

            if (first.ActualFileSnapshot != null)
            {
                SetExample(report, "Version",
                    "ActualFileSnapshot.Created=" + first.ActualFileSnapshot.Created.ToString("o")
                    + "; Previous=" + (first.PreviousFileSnapshots != null ? first.PreviousFileSnapshots.Count : 0));
                var versionField = report.SystemFields.FirstOrDefault(f => f.Field == "Version");
                if (versionField != null && versionField.Availability == CapabilityStatus.Partial)
                    versionField.Availability = CapabilityStatus.Available;
            }

            // ModifiedDate remains NOT_EXPOSED unless history sample provides approximation note.
            var modified = report.SystemFields.FirstOrDefault(f => f.Field == "ModifiedDate");
            if (modified != null)
            {
                modified.Notes = "NOT_EXPOSED on IDataObject. Approximation candidates: latest IHistoryItem.Created, IFilesSnapshot.Created.";
            }
        }

        private static void SetExample(ProjectInventoryReport report, string field, string example)
        {
            var item = report.SystemFields.FirstOrDefault(f => f.Field == field);
            if (item != null && !string.IsNullOrEmpty(example))
                item.Example = example;
        }
    }
}
