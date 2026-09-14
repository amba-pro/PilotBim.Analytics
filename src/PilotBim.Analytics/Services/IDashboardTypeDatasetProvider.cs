using System.Threading;
using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.Services
{
    /// <summary>
    /// Internal test/production seam for one TypeId materialization.
    /// Not a repository. Not a public API. Not MEF-exported.
    /// </summary>
    internal interface IDashboardTypeDatasetProvider
    {
        DashboardTypeDataset Materialize(int typeId, CancellationToken cancellationToken);
    }
}
