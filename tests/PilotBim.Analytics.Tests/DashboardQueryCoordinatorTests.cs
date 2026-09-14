using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PilotBim.Analytics.Models;
using PilotBim.Analytics.Services;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    public sealed class DashboardQueryCoordinatorTests
    {
        private const int TypeA = 5;
        private const int TypeB = 6;
        private const int TypePartial = 7;
        private const int TypeFailed = 8;
        private const int TypeRefresh = 9;

        [Fact]
        public void Construct_DoesNotMaterialize()
        {
            var provider = new FakeProvider();
            using (var coordinator = Create(provider))
            {
                Assert.Equal(0, provider.Calls);
                Assert.Equal(0, coordinator.CachedTypeCount);
            }
        }

        [Fact]
        public async Task Sequential_SameTypeId_MaterializesOnce()
        {
            var provider = CompleteProvider(TypeA, Row(TypeA), Row(TypeA), Row(TypeA));
            using (var coordinator = Create(provider, Catalog(TypeA)))
            {
                var first = await coordinator.ExecuteAsync(Scalar(TypeA), CancellationToken.None);
                var second = await coordinator.ExecuteAsync(Scalar(TypeA), CancellationToken.None);

                Assert.Equal(1, provider.Calls);
                Assert.Equal(WidgetQueryStatus.Success, first.Status);
                Assert.Equal(WidgetQueryStatus.Success, second.Status);
                Assert.Equal(3, first.Dataset.Rows[0].Value);
                Assert.Equal(3, second.Dataset.Rows[0].Value);
                Assert.Equal(1, coordinator.CachedTypeCount);
            }
        }

        [Fact]
        public async Task Concurrent_SameTypeId_MaterializesOnce()
        {
            var started = new ManualResetEventSlim(false);
            var release = new ManualResetEventSlim(false);
            var provider = new FakeProvider
            {
                Started = started,
                BlockUntil = release,
                Factory = id => Complete(id, Row(id), Row(id))
            };

            using (var coordinator = Create(provider, Catalog(TypeA)))
            {
                var t1 = coordinator.ExecuteAsync(Scalar(TypeA), CancellationToken.None);
                var t2 = coordinator.ExecuteAsync(Scalar(TypeA), CancellationToken.None);
                var t3 = coordinator.ExecuteAsync(Scalar(TypeA), CancellationToken.None);

                Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
                Thread.Sleep(80);
                Assert.Equal(1, provider.Calls);
                Assert.Equal(1, coordinator.CachedTypeCount);

                release.Set();
                var results = await Task.WhenAll(t1, t2, t3);

                Assert.Equal(1, provider.Calls);
                Assert.All(results, r => Assert.Equal(WidgetQueryStatus.Success, r.Status));
                Assert.All(results, r => Assert.Equal(2, r.Dataset.Rows[0].Value));
            }
        }

        [Fact]
        public async Task DifferentTypeIds_MaterializeSeparately()
        {
            var provider = new FakeProvider
            {
                Factory = id => Complete(id, Row(id))
            };
            var catalog = new PilotFieldCatalogBuilder().Build(new[]
            {
                TypeRec(TypeA),
                TypeRec(TypeB)
            });

            using (var coordinator = Create(provider, catalog, SnapshotTypes(("A", 1, TypeA))))
            {
                var a = await coordinator.ExecuteAsync(Scalar(TypeA), CancellationToken.None);
                var b = await coordinator.ExecuteAsync(Scalar(TypeB), CancellationToken.None);

                Assert.Equal(2, provider.Calls);
                Assert.Equal(new[] { TypeA, TypeB }, provider.TypeIds.ToArray());
                Assert.Equal(WidgetQueryStatus.Success, a.Status);
                Assert.Equal(WidgetQueryStatus.Success, b.Status);
                Assert.Equal(1, a.Dataset.Rows[0].Value);
                Assert.Equal(1, b.Dataset.Rows[0].Value);
                Assert.Equal(2, coordinator.CachedTypeCount);
            }
        }

        [Fact]
        public async Task Snapshot_EntityTypeIdNull_DoesNotCallProvider()
        {
            var provider = new FakeProvider();
            var snapshot = SnapshotTypes(("Door", 10, 1), ("Window", 3, 2));
            var query = ProjectCountBy(DashboardFieldIds.SystemTypeId);
            var direct = new SnapshotWidgetQueryEngine().Execute(snapshot, query);

            using (var coordinator = Create(provider, Catalog(TypeA), snapshot))
            {
                var result = await coordinator.ExecuteAsync(query, CancellationToken.None);

                Assert.Equal(0, provider.Calls);
                Assert.Equal(0, coordinator.CachedTypeCount);
                Assert.Equal(direct.Status, result.Status);
                Assert.Equal(direct.Dataset.Rows.Count, result.Dataset.Rows.Count);
                Assert.Equal(direct.Dataset.Rows[0].Key, result.Dataset.Rows[0].Key);
                Assert.Equal(direct.Dataset.Rows[0].Value, result.Dataset.Rows[0].Value);
            }
        }

        [Fact]
        public async Task Snapshot_Filters_Unsupported_ProviderNotCalled()
        {
            var provider = new FakeProvider();
            var filter = new DashboardFilterDefinition(
                DashboardFieldIds.SystemTypeId,
                DashboardFilterOperator.Equals,
                DashboardFilterValue.Integer(1));
            var query = new DashboardWidgetQuery(
                DashboardQueryScopeKind.CurrentProject,
                DashboardFieldIds.SystemTypeId,
                DashboardQueryMeasure.Count,
                DashboardQuerySort.ValueDescending,
                null,
                null,
                new[] { filter });

            using (var coordinator = Create(provider, Catalog(TypeA), SnapshotTypes(("A", 1, 1))))
            {
                var result = await coordinator.ExecuteAsync(query, CancellationToken.None);

                Assert.Equal(0, provider.Calls);
                Assert.Equal(WidgetQueryStatus.UnsupportedQuery, result.Status);
                Assert.Empty(result.Dataset.Rows);
            }
        }

        [Fact]
        public async Task TypeScoped_Complete_ScalarCount()
        {
            var provider = CompleteProvider(TypeA, Row(TypeA), Row(TypeA));
            using (var coordinator = Create(provider, Catalog(TypeA)))
            {
                var result = await coordinator.ExecuteAsync(Scalar(TypeA), CancellationToken.None);
                Assert.Equal(WidgetQueryStatus.Success, result.Status);
                Assert.Single(result.Dataset.Rows);
                Assert.Equal(2, result.Dataset.Rows[0].Value);
            }
        }

        [Fact]
        public async Task TypeScoped_EmptyComplete_ScalarZero()
        {
            var provider = CompleteProvider(TypeA);
            using (var coordinator = Create(provider, Catalog(TypeA)))
            {
                var result = await coordinator.ExecuteAsync(Scalar(TypeA), CancellationToken.None);
                Assert.Equal(WidgetQueryStatus.Success, result.Status);
                Assert.Equal(0, result.Dataset.Rows[0].Value);
            }
        }

        [Fact]
        public async Task TypeScoped_CustomAttributeDimension()
        {
            var field = DashboardFieldIds.Attribute(TypeA, "RemarkType");
            var provider = new FakeProvider
            {
                Factory = id => Complete(
                    id,
                    Row(id, Text(field, "Coordination")),
                    Row(id, Text(field, "Coordination")),
                    Row(id, Text(field, "Attributes")))
            };
            var catalog = Catalog(TypeA, Attr("RemarkType", "Remark type", "String"));

            using (var coordinator = Create(provider, catalog))
            {
                var result = await coordinator.ExecuteAsync(CountBy(TypeA, field), CancellationToken.None);
                Assert.Equal(WidgetQueryStatus.Success, result.Status);
                Assert.Equal(2, result.Dataset.Rows.Count);
                Assert.Equal("Coordination", result.Dataset.Rows[0].Key);
                Assert.Equal(2, result.Dataset.Rows[0].Value);
            }
        }

        [Fact]
        public async Task TypeScoped_FilterPreserved()
        {
            var field = DashboardFieldIds.Attribute(TypeA, "Status");
            var provider = new FakeProvider
            {
                Factory = id => Complete(
                    id,
                    Row(id, Text(field, "Open")),
                    Row(id, Text(field, "Closed")),
                    Row(id, Text(field, "Open")))
            };
            var catalog = Catalog(TypeA, Attr("Status", "Status", "String"));
            var query = new DashboardWidgetQuery(
                DashboardQueryScopeKind.CurrentProject,
                null,
                DashboardQueryMeasure.Count,
                DashboardQuerySort.ValueDescending,
                null,
                TypeA,
                new[]
                {
                    new DashboardFilterDefinition(field, DashboardFilterOperator.Equals, DashboardFilterValue.Text("Open"))
                });

            using (var coordinator = Create(provider, catalog))
            {
                var result = await coordinator.ExecuteAsync(query, CancellationToken.None);
                Assert.Equal(WidgetQueryStatus.Success, result.Status);
                Assert.Equal(2, result.Dataset.Rows[0].Value);
            }
        }

        [Fact]
        public async Task SameTypeId_DifferentFilters_OneMaterialization()
        {
            var status = DashboardFieldIds.Attribute(TypeA, "Status");
            var owner = DashboardFieldIds.Attribute(TypeA, "Owner");
            var provider = new FakeProvider
            {
                Factory = id => Complete(
                    id,
                    Row(id, Text(status, "Open"), Text(owner, "User1")),
                    Row(id, Text(status, "Closed"), Text(owner, "User1")),
                    Row(id, Text(status, "Open"), Text(owner, "User2")))
            };
            var catalog = Catalog(TypeA, Attr("Status", "Status", "String"), Attr("Owner", "Owner", "String"));

            using (var coordinator = Create(provider, catalog))
            {
                var open = await coordinator.ExecuteAsync(
                    FilteredScalar(TypeA, Eq(status, "Open")),
                    CancellationToken.None);
                var user1 = await coordinator.ExecuteAsync(
                    FilteredScalar(TypeA, Eq(owner, "User1")),
                    CancellationToken.None);

                Assert.Equal(1, provider.Calls);
                Assert.Equal(2, open.Dataset.Rows[0].Value);
                Assert.Equal(2, user1.Dataset.Rows[0].Value);
            }
        }

        [Fact]
        public async Task SameTypeId_DifferentDimensions_OneMaterialization()
        {
            var status = DashboardFieldIds.Attribute(TypeA, "Status");
            var owner = DashboardFieldIds.Attribute(TypeA, "Owner");
            var provider = new FakeProvider
            {
                Factory = id => Complete(
                    id,
                    Row(id, Text(status, "Open"), Text(owner, "User1")),
                    Row(id, Text(status, "Closed"), Text(owner, "User1")))
            };
            var catalog = Catalog(TypeA, Attr("Status", "Status", "String"), Attr("Owner", "Owner", "String"));

            using (var coordinator = Create(provider, catalog))
            {
                var byStatus = await coordinator.ExecuteAsync(CountBy(TypeA, status), CancellationToken.None);
                var byOwner = await coordinator.ExecuteAsync(CountBy(TypeA, owner), CancellationToken.None);

                Assert.Equal(1, provider.Calls);
                Assert.Equal(2, byStatus.Dataset.Rows.Count);
                Assert.Single(byOwner.Dataset.Rows);
                Assert.Equal(2, byOwner.Dataset.Rows[0].Value);
            }
        }

        [Fact]
        public async Task Partial_Cached_IncompleteData_NoRetry()
        {
            var provider = new FakeProvider
            {
                Factory = id => new DashboardTypeDataset(
                    id, 4, 3, DashboardTypeCoverage.Partial, "partial", new[] { Row(id), Row(id), Row(id) }, 0, 0)
            };

            using (var coordinator = Create(provider, Catalog(TypePartial)))
            {
                var first = await coordinator.ExecuteAsync(Scalar(TypePartial), CancellationToken.None);
                var second = await coordinator.ExecuteAsync(CountBy(TypePartial, DashboardFieldIds.SystemTypeId), CancellationToken.None);

                Assert.Equal(1, provider.Calls);
                Assert.Equal(WidgetQueryStatus.IncompleteData, first.Status);
                Assert.Equal(WidgetQueryStatus.IncompleteData, second.Status);
                Assert.Empty(first.Dataset.Rows);
                Assert.Empty(second.Dataset.Rows);
            }
        }

        [Fact]
        public async Task Failed_Cached_IncompleteData_NoRetry()
        {
            var provider = new FakeProvider
            {
                Factory = id => new DashboardTypeDataset(
                    id, 4, 0, DashboardTypeCoverage.Failed, "failed", new DashboardObjectRow[0], 0, 0)
            };

            using (var coordinator = Create(provider, Catalog(TypeFailed)))
            {
                var first = await coordinator.ExecuteAsync(Scalar(TypeFailed), CancellationToken.None);
                var second = await coordinator.ExecuteAsync(Scalar(TypeFailed), CancellationToken.None);

                Assert.Equal(1, provider.Calls);
                Assert.Equal(WidgetQueryStatus.IncompleteData, first.Status);
                Assert.Equal(WidgetQueryStatus.IncompleteData, second.Status);
            }
        }

        [Fact]
        public async Task NewSession_RematerializesAfterDispose()
        {
            var provider = CompleteProvider(TypeRefresh, Row(TypeRefresh));

            using (var first = Create(provider, Catalog(TypeRefresh)))
            {
                var result = await first.ExecuteAsync(Scalar(TypeRefresh), CancellationToken.None);
                Assert.Equal(WidgetQueryStatus.Success, result.Status);
                Assert.Equal(1, provider.Calls);
            }

            using (var second = Create(provider, Catalog(TypeRefresh)))
            {
                var result = await second.ExecuteAsync(Scalar(TypeRefresh), CancellationToken.None);
                Assert.Equal(WidgetQueryStatus.Success, result.Status);
                Assert.Equal(2, provider.Calls);
            }
        }

        [Fact]
        public async Task Dispose_CancelsSessionToken_InFlightDoesNotSucceed()
        {
            var started = new ManualResetEventSlim(false);
            var sawCancel = new ManualResetEventSlim(false);
            var provider = new FakeProvider
            {
                Started = started,
                OnWait = token =>
                {
                    try
                    {
                        var block = new ManualResetEventSlim(false);
                        block.Wait(TimeSpan.FromSeconds(10), token);
                    }
                    catch (OperationCanceledException)
                    {
                        sawCancel.Set();
                        throw;
                    }
                }
            };

            var coordinator = Create(provider, Catalog(TypeA));
            var pending = coordinator.ExecuteAsync(Scalar(TypeA), CancellationToken.None);
            Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
            coordinator.Dispose();

            Assert.True(sawCancel.Wait(TimeSpan.FromSeconds(5)));
            await Assert.ThrowsAsync<ObjectDisposedException>(() => pending);
            await Assert.ThrowsAsync<ObjectDisposedException>(
                () => coordinator.ExecuteAsync(Scalar(TypeA), CancellationToken.None));
        }

        [Fact]
        public async Task ExecuteAfterDispose_ThrowsObjectDisposed()
        {
            var provider = new FakeProvider();
            var coordinator = Create(provider, Catalog(TypeA));
            coordinator.Dispose();

            await Assert.ThrowsAsync<ObjectDisposedException>(
                () => coordinator.ExecuteAsync(ProjectCountBy(DashboardFieldIds.SystemTypeId), CancellationToken.None));
            Assert.Equal(0, provider.Calls);
        }

        [Fact]
        public async Task CallerToken_DoesNotCancelSharedMaterialization()
        {
            var started = new ManualResetEventSlim(false);
            var release = new ManualResetEventSlim(false);
            var cancelledShared = 0;
            var provider = new FakeProvider
            {
                Started = started,
                BlockUntil = release,
                Factory = id =>
                {
                    return Complete(id, Row(id), Row(id));
                },
                OnWait = token =>
                {
                    if (token.IsCancellationRequested)
                        Interlocked.Increment(ref cancelledShared);
                }
            };

            using (var coordinator = Create(provider, Catalog(TypeA)))
            {
                var callerCts = new CancellationTokenSource();
                var a = coordinator.ExecuteAsync(Scalar(TypeA), callerCts.Token);
                Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
                var b = coordinator.ExecuteAsync(Scalar(TypeA), CancellationToken.None);

                callerCts.Cancel();
                Thread.Sleep(50);
                Assert.Equal(0, Volatile.Read(ref cancelledShared));
                Assert.False(a.IsCompleted);

                release.Set();
                var ra = await a;
                var rb = await b;

                Assert.Equal(1, provider.Calls);
                Assert.Equal(WidgetQueryStatus.Success, ra.Status);
                Assert.Equal(WidgetQueryStatus.Success, rb.Status);
                Assert.Equal(2, rb.Dataset.Rows[0].Value);
            }
        }

        [Fact]
        public async Task UnexpectedProviderFault_CachedForSession()
        {
            var provider = new FakeProvider
            {
                ThrowOnMaterialize = new InvalidOperationException("boom")
            };

            using (var coordinator = Create(provider, Catalog(TypeA)))
            {
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => coordinator.ExecuteAsync(Scalar(TypeA), CancellationToken.None));
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => coordinator.ExecuteAsync(Scalar(TypeA), CancellationToken.None));
                Assert.Equal(1, provider.Calls);
            }
        }

        [Fact]
        public async Task NullQuery_Invalid_WithoutProvider()
        {
            var provider = new FakeProvider();
            using (var coordinator = Create(provider))
            {
                var result = await coordinator.ExecuteAsync(null, CancellationToken.None);
                Assert.Equal(WidgetQueryStatus.InvalidQuery, result.Status);
                Assert.Equal(0, provider.Calls);
            }
        }

        private static DashboardQueryCoordinator Create(
            FakeProvider provider,
            DashboardFieldCatalog catalog = null,
            ProjectAnalyticsSnapshot snapshot = null)
        {
            return new DashboardQueryCoordinator(
                snapshot ?? SnapshotTypes(("A", 1, 1)),
                catalog ?? Catalog(TypeA),
                provider);
        }

        private static FakeProvider CompleteProvider(int typeId, params DashboardObjectRow[] rows)
        {
            return new FakeProvider
            {
                Factory = id => Complete(id, rows)
            };
        }

        private static DashboardWidgetQuery Scalar(int typeId)
        {
            return new DashboardWidgetQuery(
                DashboardQueryScopeKind.CurrentProject,
                null,
                DashboardQueryMeasure.Count,
                DashboardQuerySort.ValueDescending,
                null,
                typeId);
        }

        private static DashboardWidgetQuery CountBy(int typeId, string fieldId)
        {
            return new DashboardWidgetQuery(
                DashboardQueryScopeKind.CurrentProject,
                fieldId,
                DashboardQueryMeasure.Count,
                DashboardQuerySort.ValueDescending,
                null,
                typeId);
        }

        private static DashboardWidgetQuery ProjectCountBy(string fieldId)
        {
            return new DashboardWidgetQuery(
                DashboardQueryScopeKind.CurrentProject,
                fieldId,
                DashboardQueryMeasure.Count,
                DashboardQuerySort.ValueDescending,
                null);
        }

        private static DashboardWidgetQuery FilteredScalar(int typeId, DashboardFilterDefinition filter)
        {
            return new DashboardWidgetQuery(
                DashboardQueryScopeKind.CurrentProject,
                null,
                DashboardQueryMeasure.Count,
                DashboardQuerySort.ValueDescending,
                null,
                typeId,
                new[] { filter });
        }

        private static DashboardFilterDefinition Eq(string field, string text)
        {
            return new DashboardFilterDefinition(field, DashboardFilterOperator.Equals, DashboardFilterValue.Text(text));
        }

        private static DashboardTypeDataset Complete(int typeId, params DashboardObjectRow[] rows)
        {
            var list = rows ?? new DashboardObjectRow[0];
            return new DashboardTypeDataset(
                typeId,
                list.Length,
                list.Length,
                DashboardTypeCoverage.Complete,
                "complete",
                list,
                0,
                0);
        }

        private static DashboardFieldCatalog Catalog(int typeId, params AttributeInventoryRecord[] attrs)
        {
            return new PilotFieldCatalogBuilder().Build(new[] { TypeRec(typeId, attrs) });
        }

        private static TypeInventoryRecord TypeRec(int typeId, params AttributeInventoryRecord[] attrs)
        {
            return new TypeInventoryRecord
            {
                TypeId = typeId,
                Name = "type" + typeId,
                Title = "Type " + typeId,
                Attributes = (attrs ?? new AttributeInventoryRecord[0]).ToList()
            };
        }

        private static AttributeInventoryRecord Attr(string name, string title, string valueType)
        {
            return new AttributeInventoryRecord
            {
                Name = name,
                AttributeId = name,
                Title = title,
                ValueType = valueType
            };
        }

        private static DashboardObjectRow Row(int typeId, params KeyValuePair<string, DashboardFieldValue>[] fields)
        {
            var map = new Dictionary<string, DashboardFieldValue>(StringComparer.Ordinal);
            foreach (var pair in fields)
                map[pair.Key] = pair.Value;
            return new DashboardObjectRow(Guid.NewGuid(), typeId, null, map);
        }

        private static KeyValuePair<string, DashboardFieldValue> Text(string id, string value)
        {
            return new KeyValuePair<string, DashboardFieldValue>(
                id,
                new DashboardFieldValue(DashboardFieldType.Text, value, null, null));
        }

        private static ProjectAnalyticsSnapshot SnapshotTypes(params (string name, long count, int id)[] items)
        {
            return new ProjectAnalyticsSnapshot
            {
                ObjectsByType = items.Select(t => new TypeCountRow
                {
                    TypeId = t.id,
                    TypeName = t.name,
                    Count = t.count
                }).ToList()
            };
        }

        private sealed class FakeProvider : IDashboardTypeDatasetProvider
        {
            public int Calls;
            public readonly List<int> TypeIds = new List<int>();
            public ManualResetEventSlim Started;
            public ManualResetEventSlim BlockUntil;
            public Action<CancellationToken> OnWait;
            public Func<int, DashboardTypeDataset> Factory;
            public Exception ThrowOnMaterialize;

            public DashboardTypeDataset Materialize(int typeId, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref Calls);
                lock (TypeIds)
                    TypeIds.Add(typeId);

                if (Started != null)
                    Started.Set();

                if (OnWait != null)
                    OnWait(cancellationToken);

                if (BlockUntil != null)
                    BlockUntil.Wait(cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                if (ThrowOnMaterialize != null)
                    throw ThrowOnMaterialize;

                if (Factory != null)
                    return Factory(typeId);

                return Complete(typeId);
            }
        }
    }
}
