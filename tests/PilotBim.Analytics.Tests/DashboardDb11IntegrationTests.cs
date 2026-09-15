using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PilotBim.Analytics.Models;
using PilotBim.Analytics.Services;
using PilotBim.Analytics.ViewModels;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    public sealed class DashboardDb11IntegrationTests
    {
        private static readonly Guid ProjectA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private const int TypeA = 100;
        private const string AttrName = "RemarkType";

        [Fact]
        public async Task Resize_DoesNotRerunQuery_KeepsDatasetAndCoordinator()
        {
            using (var env = new Env())
            {
                env.Store.Save(V3(QueryWidget("q1", "Kpi", true, 0, 0, 6, 2)));
                var provider = env.CompleteProvider();
                using (var presenter = env.CreatePresenter(provider))
                {
                    presenter.ReplaceDataSession(null, Catalog(), Types());
                    await presenter.RefreshTask;
                    var coordinator = presenter.Coordinator;
                    var calls = provider.Calls;
                    var vm = Vm(presenter, "q1");
                    var value = vm.KpiDisplayValue;
                    var status = vm.QueryStatus;
                    Assert.Equal(DashboardQueryWidgetRuntimeStatus.Success, status);
                    string error;
                    Assert.True(presenter.TryCommitWidgetRect("q1", new DashboardGridRect(0, 0, 12, 4), true, out error), error);
                    await presenter.RefreshTask;
                    Assert.Same(coordinator, presenter.Coordinator);
                    Assert.Equal(calls, provider.Calls);
                    Assert.Equal(status, Vm(presenter, "q1").QueryStatus);
                    Assert.Equal(value, Vm(presenter, "q1").KpiDisplayValue);
                    Assert.NotEqual(DashboardQueryWidgetRuntimeStatus.Loading, Vm(presenter, "q1").QueryStatus);
                }
            }
        }

        [Fact]
        public async Task Auto_RemainsPersisted_ResolvedAtRuntime()
        {
            using (var env = new Env())
            {
                env.Store.Save(V3(QueryWidget("q1", "Auto", true, 0, 0, 6, 2)));
                using (var presenter = env.CreatePresenter(env.CompleteProvider()))
                {
                    presenter.ReplaceDataSession(null, Catalog(), Types());
                    await presenter.RefreshTask;
                    Assert.Equal("Auto", presenter.GetWidgetDefinition("q1").Visualization.Type);
                    Assert.Equal("Kpi", Vm(presenter, "q1").ResolvedVisualization);
                    Assert.True(Vm(presenter, "q1").ShowQueryKpiCard);
                }
            }
        }

        [Fact]
        public async Task VisualizationChange_ExpandsBelowMin_AndResolvesCollision()
        {
            using (var env = new Env())
            {
                env.Store.Save(V3(
                    QueryWidget("kpi", "Kpi", true, 0, 0, 3, 2),
                    QueryWidget("other", "Bar", false, 0, 2, 6, 3)));
                using (var presenter = env.CreatePresenter(env.CompleteProvider()))
                {
                    presenter.ReplaceDataSession(null, Catalog(), Types());
                    await presenter.RefreshTask;
                    var edited = QueryWidget("kpi", "Table", true, 0, 0, 3, 2);
                    string error;
                    Assert.True(presenter.TrySaveQueryWidget(edited, out error), error);
                    var layout = presenter.GetWidgetDefinition("kpi").Layout;
                    Assert.True(layout.Width >= 4);
                    Assert.True(layout.Height >= 3);
                    Assert.Equal("Table", presenter.GetWidgetDefinition("kpi").Visualization.Type);
                    Assert.False(DashboardGridLayoutEngine.AnyVisibleOverlap(presenter.CurrentDefinition.Widgets));
                }
            }
        }

        [Fact]
        public async Task ResizeBelowVisualizationMin_Clamps()
        {
            using (var env = new Env())
            {
                env.Store.Save(V3(QueryWidget("bar", "Bar", false, 0, 0, 6, 3)));
                using (var presenter = env.CreatePresenter(env.CompleteProvider()))
                {
                    presenter.ReplaceDataSession(null, Catalog(), Types());
                    await presenter.RefreshTask;
                    string error;
                    Assert.True(presenter.TryCommitWidgetRect("bar", new DashboardGridRect(0, 0, 2, 1), true, out error), error);
                    var layout = presenter.GetWidgetDefinition("bar").Layout;
                    Assert.Equal(4, layout.Width);
                    Assert.Equal(3, layout.Height);
                }
            }
        }

        [Fact]
        public void PreviewAndDashboard_ShareAdapterResolution()
        {
            var rows = new WidgetDataRow[6];
            for (var i = 0; i < rows.Length; i++)
                rows[i] = new WidgetDataRow("k" + i, "L" + i, i + 1);
            var dataset = new WidgetDataset(rows);
            var preview = DashboardWidgetDatasetAdapter.TryRender(dataset, "Auto", true);
            var dashboard = DashboardWidgetDatasetAdapter.TryRender(dataset, "Auto", true);
            Assert.Equal("HorizontalBar", preview.ResolvedVisualization);
            Assert.Equal(preview.ResolvedVisualization, dashboard.ResolvedVisualization);
            Assert.Equal(preview.ChartKind, dashboard.ChartKind);
            Assert.Equal(preview.Points.Count, dashboard.Points.Count);
        }

        private static DashboardWidgetVm Vm(AnalyticsDashboardPresenter presenter, string id)
        {
            return presenter.DashboardWidgets.Single(w => w.Id == id);
        }

        private static DashboardDefinition V3(params DashboardWidgetDefinition[] widgets)
        {
            return new DashboardDefinition
            {
                SchemaVersion = 3,
                Id = DashboardPersistenceV2.DefaultDashboardId,
                Title = DashboardPersistenceV2.DefaultTitle,
                ProjectKey = ProjectA.ToString("D"),
                Widgets = widgets.ToList()
            };
        }

        private static DashboardWidgetDefinition QueryWidget(
            string id,
            string visualization,
            bool scalar,
            int x,
            int y,
            int width,
            int height)
        {
            var query = new DashboardWidgetQuery(
                DashboardQueryScopeKind.CurrentProject,
                scalar ? null : DashboardFieldIds.Attribute(TypeA, AttrName),
                DashboardQueryMeasure.Count,
                DashboardQuerySort.ValueDescending,
                null,
                TypeA);
            return new DashboardWidgetDefinition
            {
                Id = id,
                Title = id,
                ContentKind = DashboardPersistenceV2.ContentQuery,
                Layout = new DashboardWidgetLayoutDefinition
                {
                    X = x,
                    Y = y,
                    Width = width,
                    Height = height,
                    IsVisible = true,
                    ColumnSpan = width <= 6 ? 1 : 2,
                    Order = y * 12 + x
                },
                Query = DashboardQueryPersistence.ToDocument(query),
                Visualization = new DashboardVisualizationDefinition { Type = visualization }
            };
        }

        private static DashboardFieldCatalog Catalog()
        {
            return new PilotFieldCatalogBuilder().Build(new[] { TypeRec(TypeA) });
        }

        private static IReadOnlyList<DashboardObjectTypeOption> Types()
        {
            return DashboardObjectTypeOption.FromInventory(new[] { TypeRec(TypeA) });
        }

        private static TypeInventoryRecord TypeRec(int typeId)
        {
            return new TypeInventoryRecord
            {
                TypeId = typeId,
                Name = "type" + typeId,
                Title = "Type " + typeId,
                Attributes = new List<AttributeInventoryRecord>
                {
                    new AttributeInventoryRecord
                    {
                        Name = AttrName,
                        AttributeId = AttrName,
                        Title = "Тип замечания",
                        ValueType = "String"
                    }
                }
            };
        }

        private static DashboardTypeDataset Complete(int typeId, params DashboardObjectRow[] rows)
        {
            var list = rows ?? new DashboardObjectRow[0];
            return new DashboardTypeDataset(typeId, list.Length, list.Length, DashboardTypeCoverage.Complete, "complete", list, 0, 0);
        }

        private static DashboardObjectRow Row(int typeId)
        {
            var map = new Dictionary<string, DashboardFieldValue>(StringComparer.Ordinal)
            {
                {
                    DashboardFieldIds.Attribute(typeId, AttrName),
                    new DashboardFieldValue(DashboardFieldType.Text, "Open", "Open", "Open")
                }
            };
            return new DashboardObjectRow(Guid.NewGuid(), typeId, null, map);
        }

        private sealed class Env : IDisposable
        {
            public Env()
            {
                Root = Path.Combine(Path.GetTempPath(), "PilotBim.Analytics.Tests", "db11-" + Guid.NewGuid().ToString("N"));
                V2Root = Path.Combine(Root, "Dashboards");
                V1Path = Path.Combine(Root, "dashboard-layout.json");
                Store = new DashboardDefinitionStore(V2Root);
            }

            public string Root { get; private set; }
            public string V2Root { get; private set; }
            public string V1Path { get; private set; }
            public DashboardDefinitionStore Store { get; private set; }
            public FakeProvider LastProvider { get; set; }

            public FakeProvider CompleteProvider()
            {
                LastProvider = new FakeProvider { Factory = id => Complete(id, Row(id), Row(id)) };
                return LastProvider;
            }

            public AnalyticsDashboardPresenter CreatePresenter(IDashboardTypeDatasetProvider provider = null)
            {
                LastProvider = provider as FakeProvider ?? LastProvider ?? CompleteProvider();
                var runtime = new AnalyticsDashboardRuntimeOptions
                {
                    ProjectKey = ProjectA,
                    DefinitionStore = Store,
                    LayoutStore = new DashboardLayoutStore(V1Path),
                    TypeDatasetProvider = provider ?? LastProvider,
                    PostToUi = action => { if (action != null) action(); }
                };
                return new AnalyticsDashboardPresenter(_ => { }, () => null, runtime);
            }

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(Root))
                        Directory.Delete(Root, true);
                }
                catch
                {
                }
            }
        }

        private sealed class FakeProvider : IDashboardTypeDatasetProvider
        {
            public int Calls;
            public Func<int, DashboardTypeDataset> Factory;

            public DashboardTypeDataset Materialize(int typeId, CancellationToken cancellationToken)
            {
                Calls++;
                return Factory(typeId);
            }
        }
    }
}
