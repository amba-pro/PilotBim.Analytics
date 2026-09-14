using System.Threading;
using Ascon.Pilot.SDK;
using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.Services
{
    /// <summary>
    /// Production adapter: blocking <see cref="DashboardTypeDatasetMaterializer"/> behind the coordinator seam.
    /// </summary>
    internal sealed class DashboardTypeDatasetProvider : IDashboardTypeDatasetProvider
    {
        private readonly DashboardTypeDatasetMaterializer _materializer;

        public DashboardTypeDatasetProvider(IObjectsRepository repository, ISearchService search)
        {
            _materializer = new DashboardTypeDatasetMaterializer(repository, search);
        }

        public DashboardTypeDataset Materialize(int typeId, CancellationToken cancellationToken)
        {
            return _materializer.Materialize(typeId, cancellationToken);
        }
    }
}
