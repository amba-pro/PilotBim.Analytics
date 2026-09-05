using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Ascon.Pilot.SDK;
using PilotBim.Analytics.Diagnostics;
using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.Data
{
    /// <summary>
    /// BFS hierarchy walk via Root → Children → SubscribeObjects.
    /// Fallback when ISearchService is unavailable. Read-only.
    /// </summary>
    internal sealed class HierarchyWalkSampler
    {
        private readonly IObjectsRepository _repo;
        private readonly PilotObjectScanner _scanner;

        public HierarchyWalkSampler(IObjectsRepository repo, PilotObjectScanner scanner)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));
        }

        public HierarchyWalkResult Walk(ScanMode mode, CancellationToken ct)
        {
            var result = new HierarchyWalkResult();
            int sampleLimit = (int)mode;
            int visitBudget = mode == ScanMode.Fast ? 2000
                : mode == ScanMode.Standard ? 25000
                : 100000;

            Guid? rootId = ResolveRootId();
            if (rootId == null)
            {
                result.Error = "Root object not resolved (GetRootObject / SystemObjectIds.RootObjectId)";
                AnalyticsLogger.Warning("hierarchy-walk", result.Error);
                return result;
            }

            AnalyticsLogger.Info("hierarchy-walk", "start root=" + rootId + " sampleLimit=" + sampleLimit + " visitBudget=" + visitBudget);

            var queue = new Queue<Guid>();
            var visited = new HashSet<Guid>();
            queue.Enqueue(rootId.Value);
            visited.Add(rootId.Value);

            var pendingChildren = new List<Guid>();

            while (queue.Count > 0 && result.VisitedCount < visitBudget && !ct.IsCancellationRequested)
            {
                Guid id = queue.Dequeue();
                IDataObject obj = _scanner.SubscribeObject(id, TimeSpan.FromSeconds(8));
                if (obj == null)
                {
                    result.FailedLoads++;
                    continue;
                }

                result.VisitedCount++;
                int typeId = SafeTypeId(obj);
                if (typeId >= 0)
                {
                    TypeBucket bucket;
                    if (!result.ByType.TryGetValue(typeId, out bucket))
                    {
                        bucket = new TypeBucket { TypeId = typeId };
                        result.ByType[typeId] = bucket;
                    }
                    bucket.Count++;
                    if (bucket.Samples.Count < sampleLimit)
                        bucket.Samples.Add(obj);

                    string typeName = SafeTypeName(obj);
                    if (IsCoordinationModel(typeName))
                        result.BimModelIds.Add(obj.Id);
                    if (IsModelPart(typeName))
                    {
                        result.BimModelPartIds.Add(obj.Id);
                        result.BimParts.Add(new BimPartRef
                        {
                            PartId = obj.Id,
                            PartName = obj.DisplayName,
                            ModelId = obj.ParentId
                        });
                    }
                }

                try
                {
                    if (obj.Children != null)
                    {
                        foreach (Guid childId in obj.Children)
                        {
                            if (visited.Add(childId))
                                pendingChildren.Add(childId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    AnalyticsLogger.Warning("hierarchy-walk", "Children read failed id=" + id + ": " + ex.Message);
                }

                // Batch-prefetch next children periodically to reduce Subscribe chatter.
                if (pendingChildren.Count >= 40 || queue.Count == 0)
                {
                    PrefetchBatch(pendingChildren, ct);
                    foreach (Guid child in pendingChildren)
                    {
                        if (result.VisitedCount + queue.Count >= visitBudget)
                        {
                            result.Truncated = true;
                            break;
                        }
                        queue.Enqueue(child);
                    }
                    pendingChildren.Clear();
                    if (result.Truncated)
                        break;
                }
            }

            if (result.VisitedCount >= visitBudget || queue.Count > 0 || pendingChildren.Count > 0)
                result.Truncated = true;

            AnalyticsLogger.Info("hierarchy-walk", "done visited=" + result.VisitedCount
                + " types=" + result.ByType.Count
                + " bimModels=" + result.BimModelIds.Count
                + " bimParts=" + result.BimModelPartIds.Count
                + " truncated=" + result.Truncated
                + " failedLoads=" + result.FailedLoads);
            return result;
        }

        private void PrefetchBatch(List<Guid> ids, CancellationToken ct)
        {
            if (ids == null || ids.Count == 0 || ct.IsCancellationRequested)
                return;
            try
            {
                _scanner.SubscribeObjects(ids, TimeSpan.FromSeconds(20));
            }
            catch (Exception ex)
            {
                AnalyticsLogger.Warning("hierarchy-walk", "Prefetch batch failed: " + ex.Message);
            }
        }

        private Guid? ResolveRootId()
        {
            try
            {
#pragma warning disable 612
                var data = _repo.GetRootObject();
#pragma warning restore 612
                if (data != null && data.Id != Guid.Empty)
                    return data.Id;
            }
            catch (Exception ex)
            {
                AnalyticsLogger.Warning("hierarchy-walk", "GetRootObject failed: " + ex.Message);
            }

            try
            {
                Guid id = SystemObjectIds.RootObjectId;
                if (id != Guid.Empty)
                    return id;
            }
            catch (Exception ex)
            {
                AnalyticsLogger.Warning("hierarchy-walk", "SystemObjectIds.RootObjectId failed: " + ex.Message);
            }

            return null;
        }

        private static int SafeTypeId(IDataObject obj)
        {
            try { return obj.Type != null ? obj.Type.Id : -1; }
            catch { return -1; }
        }

        private static string SafeTypeName(IDataObject obj)
        {
            try { return obj.Type != null ? (obj.Type.Name ?? string.Empty) : string.Empty; }
            catch { return string.Empty; }
        }

        private static bool IsCoordinationModel(string typeName)
        {
            return string.Equals(typeName, "bim_coordinationModel", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsModelPart(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return false;
            return typeName.IndexOf("bim_modelPart", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    internal sealed class HierarchyWalkResult
    {
        public Dictionary<int, TypeBucket> ByType { get; } = new Dictionary<int, TypeBucket>();
        public List<Guid> BimModelIds { get; } = new List<Guid>();
        public List<Guid> BimModelPartIds { get; } = new List<Guid>();
        public List<BimPartRef> BimParts { get; } = new List<BimPartRef>();
        public int VisitedCount { get; set; }
        public int FailedLoads { get; set; }
        public bool Truncated { get; set; }
        public string Error { get; set; }

        public int TotalObjects => ByType.Values.Sum(b => b.Count);
    }

    internal sealed class TypeBucket
    {
        public int TypeId { get; set; }
        public int Count { get; set; }
        public List<IDataObject> Samples { get; } = new List<IDataObject>();
    }
}
