using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Ascon.Pilot.Bim.SDK;
using Ascon.Pilot.SDK;
using PilotBim.Analytics.Data;
using PilotBim.Analytics.Diagnostics;
using PilotBim.Analytics.Models;
using BimTypeNames = Ascon.Pilot.Bim.SDK.TypeNames;

namespace PilotBim.Analytics.Discovery
{
    internal sealed class BimPartCatalogBuilder
    {
        private readonly IObjectsRepository _repository;
        private readonly PilotObjectScanner _scanner;

        public BimPartCatalogBuilder(IObjectsRepository repository, PilotObjectScanner scanner)
        {
            _repository = repository;
            _scanner = scanner;
        }

        public List<BimPartRef> Build(HierarchyWalkResult walk, CancellationToken token, Action<string> progress)
        {
            var map = new Dictionary<Guid, BimPartRef>();

            if (walk != null)
            {
                foreach (var part in walk.BimParts)
                {
                    if (part == null || part.PartId == Guid.Empty)
                        continue;
                    map[part.PartId] = Clone(part);
                }

                foreach (var partId in walk.BimModelPartIds)
                {
                    if (map.ContainsKey(partId))
                        continue;
                    map[partId] = new BimPartRef { PartId = partId };
                }
            }

            if (_scanner.SearchAvailable)
            {
                progress("Resolving BIM model parts via search...");
                TryAddPartsFromSearch(map, token);
            }

            progress("Resolving BIM part/model names...");
            ResolveNames(map, token);

            return map.Values
                .OrderBy(p => p.ModelName ?? string.Empty)
                .ThenBy(p => p.PartName ?? string.Empty)
                .ToList();
        }

        private void TryAddPartsFromSearch(Dictionary<Guid, BimPartRef> map, CancellationToken token)
        {
            IType partType = null;
            try
            {
                partType = _repository.GetType(BimTypeNames.ModelPart);
            }
            catch (Exception ex)
            {
                AnalyticsLogger.Warning("bim-part-catalog", "GetType ModelPart: " + ex.Message);
                return;
            }

            if (partType == null)
                return;

            var gate = new System.Threading.ManualResetEventSlim(false);
            var abandoned = 0;
            _scanner.SearchByType(partType.Id, 5000, (ids, total) =>
            {
                try
                {
                    if (!AsyncCallbackGuard.ShouldAccept(System.Threading.Interlocked.CompareExchange(ref abandoned, 0, 0)))
                        return;
                    if (ids == null)
                        return;
                    foreach (var id in ids)
                    {
                        if (token.IsCancellationRequested)
                            break;
                        if (!AsyncCallbackGuard.ShouldAccept(System.Threading.Interlocked.CompareExchange(ref abandoned, 0, 0)))
                            return;
                        if (!map.ContainsKey(id))
                            map[id] = new BimPartRef { PartId = id };
                    }
                }
                finally
                {
                    gate.Set();
                }
            }, ex =>
            {
                AnalyticsLogger.Warning("bim-part-catalog", "search parts: " + ex.Message);
                gate.Set();
            });

            if (!gate.Wait(TimeSpan.FromSeconds(30)))
                AsyncCallbackGuard.Abandon(ref abandoned);
        }

        private void ResolveNames(Dictionary<Guid, BimPartRef> map, CancellationToken token)
        {
            var modelNames = new Dictionary<Guid, string>();

            foreach (var part in map.Values)
            {
                if (token.IsCancellationRequested)
                    break;

                var obj = _scanner.TryGetCached(part.PartId)
                    ?? _scanner.SubscribeObject(part.PartId, TimeSpan.FromSeconds(6));
                if (obj == null)
                    continue;

                if (string.IsNullOrWhiteSpace(part.PartName))
                    part.PartName = obj.DisplayName;

                if (part.ModelId == Guid.Empty && obj.ParentId != Guid.Empty)
                    part.ModelId = obj.ParentId;

                if (part.ModelId != Guid.Empty && string.IsNullOrWhiteSpace(part.ModelName))
                {
                    string modelName;
                    if (modelNames.TryGetValue(part.ModelId, out modelName))
                    {
                        part.ModelName = modelName;
                    }
                    else
                    {
                        var modelObj = _scanner.TryGetCached(part.ModelId)
                            ?? _scanner.SubscribeObject(part.ModelId, TimeSpan.FromSeconds(6));
                        if (modelObj != null)
                        {
                            part.ModelName = modelObj.DisplayName;
                            modelNames[part.ModelId] = part.ModelName;
                        }
                    }
                }
            }
        }

        private static BimPartRef Clone(BimPartRef part)
        {
            return new BimPartRef
            {
                PartId = part.PartId,
                PartName = part.PartName,
                ModelId = part.ModelId,
                ModelName = part.ModelName
            };
        }
    }
}
