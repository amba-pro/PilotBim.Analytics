using System;
using System.Collections.Generic;
using System.Linq;
using Ascon.Pilot.SDK;
using PilotBim.Analytics.Data;
using PilotBim.Analytics.Diagnostics;
using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.Discovery
{
    internal sealed class DocumentDiscoveryService
    {
        public List<DocumentCapabilityRecord> BuildCapabilities()
        {
            return new List<DocumentCapabilityRecord>
            {
                Cap("File metadata", CapabilityStatus.Available, "IFile.Name/Size/Created/Modified", "Do not download file content"),
                Cap("Files on object", CapabilityStatus.Available, "IDataObject.Files", null),
                Cap("Actual file snapshot", CapabilityStatus.Available, "IDataObject.ActualFileSnapshot", "IFilesSnapshot.Created/CreatorId/Reason/Files"),
                Cap("Previous file snapshots (versions)", CapabilityStatus.Available, "IDataObject.PreviousFileSnapshots", "Document iteration history"),
                Cap("Version author", CapabilityStatus.Available, "IFilesSnapshot.CreatorId / FileExtensions.CreatorId", null),
                Cap("Version date", CapabilityStatus.Available, "IFilesSnapshot.Created / IFile.Created", null),
                Cap("Version count", CapabilityStatus.Available, "1 + PreviousFileSnapshots.Count", "When snapshots loaded on object"),
                Cap("Document status (UserState)", CapabilityStatus.Partial, "AttributeType.UserState attributes", "Config-dependent attribute name"),
                Cap("Document creator", CapabilityStatus.Available, "IDataObject.Creator", null),
                Cap("Document created date", CapabilityStatus.Available, "IDataObject.Created", null)
            };
        }

        public List<DocumentSampleRow> SampleDocuments(IEnumerable<IDataObject> objects, int max = 20)
        {
            var rows = new List<DocumentSampleRow>();
            if (objects == null)
                return rows;

            foreach (var obj in objects)
            {
                var action = NextSampleAction(obj == null, rows.Count, max);
                if (action == SampleLoopAction.Stop)
                    break;
                if (action == SampleLoopAction.Skip)
                    continue;
                if (obj.Type == null || !obj.Type.HasFiles)
                {
                    if (obj.Files == null || obj.Files.Count == 0)
                        if (obj.ActualFileSnapshot == null || obj.ActualFileSnapshot.Files == null || obj.ActualFileSnapshot.Files.Count == 0)
                            continue;
                }

                try
                {
                    var files = obj.ActualFileSnapshot != null && obj.ActualFileSnapshot.Files != null
                        ? obj.ActualFileSnapshot.Files
                        : obj.Files;
                    var latest = files != null ? files.FirstOrDefault() : null;
                    rows.Add(new DocumentSampleRow
                    {
                        ObjectId = obj.Id,
                        DisplayName = obj.DisplayName,
                        TypeName = obj.Type != null ? (obj.Type.Title ?? obj.Type.Name) : null,
                        FileCount = files != null ? files.Count : 0,
                        PreviousSnapshotCount = obj.PreviousFileSnapshots != null ? obj.PreviousFileSnapshots.Count : 0,
                        LatestFileName = latest != null ? latest.Name : null,
                        LatestFileSize = latest != null ? latest.Size : (long?)null,
                        SnapshotCreated = obj.ActualFileSnapshot != null ? obj.ActualFileSnapshot.Created : (DateTime?)null,
                        SnapshotCreatorId = obj.ActualFileSnapshot != null ? obj.ActualFileSnapshot.CreatorId : (int?)null
                    });
                }
                catch (Exception ex)
                {
                    AnalyticsLogger.Warning("document-sample", obj.Id + " " + ex.Message);
                }
            }

            return rows;
        }

        internal enum SampleLoopAction
        {
            Process,
            Skip,
            Stop
        }

        /// <summary>
        /// Loop control for SampleDocuments. Null objects must be skipped, not terminate the scan.
        /// </summary>
        internal static SampleLoopAction NextSampleAction(bool objIsNull, int rowsCount, int max)
        {
            if (rowsCount >= max)
                return SampleLoopAction.Stop;
            if (objIsNull)
                return SampleLoopAction.Skip;
            return SampleLoopAction.Process;
        }

        private static DocumentCapabilityRecord Cap(string name, string availability, string source, string notes)
        {
            return new DocumentCapabilityRecord
            {
                Capability = name,
                Availability = availability,
                SdkSource = source,
                Notes = notes
            };
        }
    }

    internal sealed class HistoryDiscoveryService
    {
        private readonly IObjectsRepository _repository;

        public HistoryDiscoveryService(IObjectsRepository repository)
        {
            _repository = repository;
        }

        public List<HistoryCapabilityRecord> BuildCapabilities()
        {
            return new List<HistoryCapabilityRecord>
            {
                Cap("Object change history", CapabilityStatus.Available, "IObjectsRepository.GetHistoryItems + Data.IHistoryItem", "Ids via DataObjectExtensions.HistoryItems(IDataObject)"),
                Cap("History timestamp", CapabilityStatus.Available, "IHistoryItem.Created", null),
                Cap("History author", CapabilityStatus.Available, "IHistoryItem.CreatorId", null),
                Cap("History reason", CapabilityStatus.Partial, "IHistoryItem.Reason", "Content semantics require runtime validation"),
                Cap("History object snapshot", CapabilityStatus.Available, "IHistoryItem.Object", "May contain object at that revision"),
                Cap("State transition history (dedicated)", CapabilityStatus.NotExposedBySdk, "n/a", "No dedicated StateTransitionHistory API"),
                Cap("State transition inference", CapabilityStatus.NeedsRuntime, "IHistoryItem + UserState attributes", "May be reconstructable from history snapshots"),
                Cap("Version history (files)", CapabilityStatus.Available, "PreviousFileSnapshots / ActualFileSnapshot", "Document file versions"),
                Cap("Change users", CapabilityStatus.Available, "IHistoryItem.CreatorId", null),
                Cap("Change dates", CapabilityStatus.Available, "IHistoryItem.Created", null),
                Cap("Live change stream", CapabilityStatus.Available, "IObjectChangeHandler / IObjectChangeProcessor", "Push notifications, not historical audit"),
                Cap("Possible state transitions (config)", CapabilityStatus.Available, "ITransitionManager / IUserStateMachine", "Not historical")
            };
        }

        public void SampleHistory(IEnumerable<IDataObject> objects, List<HistorySampleEvent> target, int maxEvents = 10)
        {
            if (objects == null || target == null)
                return;

            try
            {
                var historyIds = new List<Guid>();
                foreach (var obj in objects.Take(5))
                {
                    if (obj == null)
                        continue;
                    try
                    {
                        var ids = DataObjectExtensions.HistoryItems(obj);
                        if (ids == null)
                            continue;
                        foreach (var id in ids)
                        {
                            historyIds.Add(id);
                            if (historyIds.Count >= maxEvents)
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        AnalyticsLogger.Warning("history-ids", obj.Id + " " + ex.Message);
                    }

                    if (historyIds.Count >= maxEvents)
                        break;
                }

                if (historyIds.Count == 0)
                {
                    AnalyticsLogger.Info("history-sample", "No HistoryItems ids on sampled objects");
                    return;
                }

                var remaining = new HashSet<Guid>(historyIds.Take(maxEvents));
                var finished = false;
                var gate = new System.Threading.ManualResetEventSlim(false);

                _repository.GetHistoryItems(remaining).Subscribe(new ActionObserver<Ascon.Pilot.SDK.Data.IHistoryItem>(
                    item =>
                    {
                        if (item == null || !remaining.Remove(item.Id))
                            return;
                        target.Add(new HistorySampleEvent
                        {
                            HistoryItemId = item.Id,
                            ObjectId = item.ObjectId,
                            Created = item.Created,
                            CreatorId = item.CreatorId,
                            Reason = ReferenceResolver.SafeSampleString(item.Reason, 100)
                        });
                        if (remaining.Count == 0)
                        {
                            finished = true;
                            gate.Set();
                        }
                    },
                    ex =>
                    {
                        AnalyticsLogger.Error("history-load", ex);
                        finished = true;
                        gate.Set();
                    },
                    () =>
                    {
                        finished = true;
                        gate.Set();
                    }));

                gate.Wait(TimeSpan.FromSeconds(8));
                if (!finished)
                    AnalyticsLogger.Warning("history-sample", "Timed out waiting for GetHistoryItems");
            }
            catch (Exception ex)
            {
                AnalyticsLogger.Error("history-sample", ex);
            }
        }

        private static HistoryCapabilityRecord Cap(string name, string availability, string source, string notes)
        {
            return new HistoryCapabilityRecord
            {
                Capability = name,
                Availability = availability,
                SdkSource = source,
                Notes = notes
            };
        }
    }
}
