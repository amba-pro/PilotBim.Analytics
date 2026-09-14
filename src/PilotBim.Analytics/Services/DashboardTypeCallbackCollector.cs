using System;
using System.Collections.Generic;
using PilotBim.Analytics.Data;
using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.Services
{
    /// <summary>
    /// First-wins ObjectId collector guarded by <see cref="CallbackWaitSession"/>.
    /// Late callbacks after timeout/cancel/dispose must not mutate the dataset.
    /// </summary>
    internal static class DashboardTypeCallbackCollector
    {
        public static bool TryAdd(
            CallbackWaitSession session,
            ISet<Guid> requestedIds,
            IDictionary<Guid, DashboardObjectSource> collected,
            DashboardObjectSource source)
        {
            if (session == null || collected == null)
                return false;
            if (!session.ShouldAccept())
                return false;
            if (source == null || source.Id == Guid.Empty)
                return false;
            if (requestedIds != null && !requestedIds.Contains(source.Id))
                return false;
            if (collected.ContainsKey(source.Id))
                return false;
            collected.Add(source.Id, source);
            return true;
        }
    }
}
