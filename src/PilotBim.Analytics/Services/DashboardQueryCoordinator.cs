using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Ascon.Pilot.SDK;
using PilotBim.Analytics.Diagnostics;
using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.Services
{
    /// <summary>
    /// One dashboard data session: snapshot + catalog + per-TypeId dataset cache.
    /// Routes queries; materializes each TypeId at most once. Owned by AnalyticsDashboardPresenter.
    /// </summary>
    internal sealed class DashboardQueryCoordinator : IDisposable
    {
        private readonly ProjectAnalyticsSnapshot _snapshot;
        private readonly DashboardFieldCatalog _catalog;
        private readonly IDashboardTypeDatasetProvider _provider;
        private readonly SnapshotWidgetQueryEngine _snapshotEngine = new SnapshotWidgetQueryEngine();
        private readonly ObjectRowsWidgetQueryEngine _objectRowsEngine = new ObjectRowsWidgetQueryEngine();
        private readonly object _sync = new object();
        private readonly Dictionary<int, Task<DashboardTypeDataset>> _cache =
            new Dictionary<int, Task<DashboardTypeDataset>>();
        private readonly CancellationTokenSource _sessionCts = new CancellationTokenSource();
        private int _disposed;

        /// <summary>Production construction. Materializer is not invoked until a TypeId query misses cache.</summary>
        public DashboardQueryCoordinator(
            ProjectAnalyticsSnapshot snapshot,
            DashboardFieldCatalog catalog,
            IObjectsRepository repository,
            ISearchService search)
            : this(snapshot, catalog, new DashboardTypeDatasetProvider(repository, search))
        {
        }

        /// <summary>Test / explicit-provider construction.</summary>
        internal DashboardQueryCoordinator(
            ProjectAnalyticsSnapshot snapshot,
            DashboardFieldCatalog catalog,
            IDashboardTypeDatasetProvider provider)
        {
            if (provider == null)
                throw new ArgumentNullException("provider");

            _snapshot = snapshot;
            _catalog = catalog;
            _provider = provider;
        }

        /// <summary>Cached TypeId operations in this session (in-flight or completed). Not a public API.</summary>
        internal int CachedTypeCount
        {
            get
            {
                lock (_sync)
                    return _cache.Count;
            }
        }

        /// <summary>
        /// Execute one widget query against this session.
        /// Caller <paramref name="cancellationToken"/> is ignored in DB-6 (V1):
        /// shared TypeId materialization is owned by the session token only.
        /// Per-widget wait cancellation is deferred so one caller cannot cancel another widget's load.
        /// After <see cref="Dispose"/>: <see cref="ObjectDisposedException"/>.
        /// </summary>
        public Task<WidgetQueryResult> ExecuteAsync(
            DashboardWidgetQuery query,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            _ = cancellationToken;

            if (query == null)
                return Task.FromResult(WidgetQueryResult.Invalid("query is required"));

            if (!query.EntityTypeId.HasValue)
                return Task.FromResult(_snapshotEngine.Execute(_snapshot, query));

            return ExecuteTypeScopedAsync(query);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            try
            {
                _sessionCts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            lock (_sync)
            {
                _cache.Clear();
            }

            _sessionCts.Dispose();
        }

        private async Task<WidgetQueryResult> ExecuteTypeScopedAsync(DashboardWidgetQuery query)
        {
            DashboardTypeDataset dataset;
            try
            {
                dataset = await GetOrMaterializeAsync(query.EntityTypeId.Value).ConfigureAwait(false);
            }
            catch
            {
                ThrowIfDisposed();
                throw;
            }

            ThrowIfDisposed();
            return _objectRowsEngine.Execute(dataset, _catalog, query);
        }

        private Task<DashboardTypeDataset> GetOrMaterializeAsync(int typeId)
        {
            Task<DashboardTypeDataset> task;
            var miss = false;
            lock (_sync)
            {
                ThrowIfDisposed();
                if (!_cache.TryGetValue(typeId, out task))
                {
                    var sessionToken = _sessionCts.Token;
                    task = Task.Run(() => MaterializeLogged(typeId, sessionToken));
                    _cache[typeId] = task;
                    miss = true;
                }
            }

            if (miss)
            {
                AnalyticsLogger.Info(
                    "DashboardQuery",
                    "Type dataset MISS TypeId=" + typeId);
            }

            return task;
        }

        private DashboardTypeDataset MaterializeLogged(int typeId, CancellationToken sessionToken)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var dataset = _provider.Materialize(typeId, sessionToken);
                sw.Stop();
                if (dataset != null)
                {
                    AnalyticsLogger.Info(
                        "DashboardQuery",
                        "Type dataset materialized TypeId=" + dataset.TypeId
                        + " Coverage=" + dataset.Coverage
                        + " Expected=" + dataset.ExpectedCount
                        + " Loaded=" + dataset.LoadedUniqueCount
                        + " ElapsedMs=" + (int)sw.Elapsed.TotalMilliseconds);
                }

                return dataset;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                AnalyticsLogger.Error(
                    "DashboardQuery",
                    "Type dataset materialization failed TypeId=" + typeId,
                    ex);
                throw;
            }
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) != 0)
                throw new ObjectDisposedException(typeof(DashboardQueryCoordinator).Name);
        }
    }
}
