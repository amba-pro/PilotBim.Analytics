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
    public sealed class DashboardDb10IntegrationTests
    {
        private static readonly Guid ProjectA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private const int TypeA = 100;
        private const string AttrName = "RemarkType";

        [Fact]
        public async Task DragCandidate_SaveSuccessPublishes_SaveFailureRollsBack()
        {
            using (var env = new Env())
            {
                env.Store.Save(V2(QueryWidget("q1", 0, Scalar(TypeA), "Kpi", columnSpan: 1)));
                using (var presenter = env.CreatePresenter(env.CompleteProvider()))
                {
                    presenter.ReplaceDataSession(null, Catalog(), Types());
                    await presenter.RefreshTask;
                    var before = RectOf(presenter, "q1");
                    string error;
                    Assert.True(presenter.TryCommitWidgetRect("q1", new DashboardGridRect(6, 0, 6, 2), false, out error), error);
                    Assert.Equal(6, RectOf(presenter, "q1").X);
                    Assert.Equal(DashboardPersistenceV2.CurrentSchemaVersion, env.Store.Load(ProjectA).Definition.SchemaVersion);
                }
            }

            using (var env = new Env())
            {
                env.Store.Save(V3(QueryWidgetV3("keep", 0, 0, 6, 2)));
                using (var presenter = env.CreatePresenter(env.CompleteProvider()))
                {
                    var path = env.Store.GetPath(ProjectA);
                    using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        string error;
                        var ok = presenter.TryCommitWidgetRect("keep", new DashboardGridRect(6, 0, 6, 2), false, out error);
                        Assert.False(ok);
                        Assert.True(presenter.LastSaveFailed);
                        Assert.Equal(0, RectOf(presenter, "keep").X);
                    }
                }
            }
        }

        [Fact]
        public async Task Resize_SaveSuccess()
        {
            using (var env = new Env())
            {
                env.Store.Save(V3(QueryWidgetV3("q1", 0, 0, 6, 2)));
                using (var presenter = env.CreatePresenter(env.CompleteProvider()))
                {
                    presenter.ReplaceDataSession(null, Catalog(), Types());
                    await presenter.RefreshTask;
                    string error;
                    Assert.True(presenter.TryCommitWidgetRect("q1", new DashboardGridRect(0, 0, 12, 4), true, out error), error);
                    Assert.Equal(12, RectOf(presenter, "q1").Width);
                    Assert.Equal(4, RectOf(presenter, "q1").Height);
                }
            }
        }

        [Fact]
        public async Task LayoutEdit_DoesNotRerunQuery_OrReplaceCoordinator()
        {
            using (var env = new Env())
            {
                env.Store.Save(V3(QueryWidgetV3("q1", 0, 0, 6, 2)));
                var provider = env.CompleteProvider();
                using (var presenter = env.CreatePresenter(provider))
                {
                    presenter.ReplaceDataSession(null, Catalog(), Types());
                    await presenter.RefreshTask;
                    var coordinator = presenter.Coordinator;
                    var calls = provider.Calls;
                    var status = Vm(presenter, "q1").QueryStatus;
                    Assert.Equal(DashboardQueryWidgetRuntimeStatus.Success, status);
                    string error;
                    Assert.True(presenter.TryCommitWidgetRect("q1", new DashboardGridRect(6, 2, 6, 2), false, out error), error);
                    Assert.True(presenter.TryCommitWidgetRect("q1", new DashboardGridRect(6, 2, 12, 3), true, out error), error);
                    await presenter.RefreshTask;
                    Assert.Same(coordinator, presenter.Coordinator);
                    Assert.Equal(calls, provider.Calls);
                    Assert.Equal(DashboardQueryWidgetRuntimeStatus.Success, Vm(presenter, "q1").QueryStatus);
                    Assert.NotEqual(DashboardQueryWidgetRuntimeStatus.Loading, Vm(presenter, "q1").QueryStatus);
                }
            }
        }

        [Fact]
        public async Task LegacyAndQuery_ShareGrid_DeleteLeavesOthers_AddFindsSlot()
        {
            using (var env = new Env())
            {
                env.Store.Save(V3(
                    LegacyV3("kpi", 0, 0, 6, 2, DashboardWidgetKinds.Kpi),
                    QueryWidgetV3("q1", 6, 0, 6, 2)));
                using (var presenter = env.CreatePresenter(env.CompleteProvider()))
                {
                    presenter.ReplaceDataSession(null, Catalog(), Types());
                    await presenter.RefreshTask;
                    Assert.Equal(0, RectOf(presenter, "kpi").X);
                    Assert.Equal(6, RectOf(presenter, "q1").X);

                    presenter.RemoveWidget("q1");
                    Assert.Null(presenter.GetWidgetDefinition("q1"));
                    Assert.Equal(0, RectOf(presenter, "kpi").X);
                    Assert.Equal(0, RectOf(presenter, "kpi").Y);

                    string error;
                    Assert.True(presenter.TrySaveQueryWidget(QueryWidgetV3("q2", 0, 0, 6, 2), out error), error);
                    Assert.Equal(6, RectOf(presenter, "q2").X);
                    Assert.Equal(0, RectOf(presenter, "q2").Y);
                    Assert.Equal(0, RectOf(presenter, "kpi").X);
                }
            }
        }

        [Fact]
        public async Task QueryEdit_PreservesRect()
        {
            using (var env = new Env())
            {
                env.Store.Save(V3(QueryWidgetV3("q1", 3, 4, 6, 3)));
                using (var presenter = env.CreatePresenter(env.CompleteProvider()))
                {
                    presenter.ReplaceDataSession(null, Catalog(), Types());
                    await presenter.RefreshTask;
                    var edited = QueryWidgetV3("q1", 0, 0, 12, 8, title: "Edited", visualization: "Table");
                    string error;
                    Assert.True(presenter.TrySaveQueryWidget(edited, out error), error);
                    var layout = presenter.GetWidgetDefinition("q1").Layout;
                    Assert.Equal("Edited", presenter.GetWidgetDefinition("q1").Title);
                    Assert.Equal("Table", presenter.GetWidgetDefinition("q1").Visualization.Type);
                    Assert.Equal(3, layout.X);
                    Assert.Equal(4, layout.Y);
                    Assert.Equal(6, layout.Width);
                    Assert.Equal(3, layout.Height);
                }
            }
        }

        [Fact]
        public void Hide_ExcludesOccupancy_ShowResolvesCollision()
        {
            using (var env = new Env())
            {
                env.Store.Save(V3(
                    QueryWidgetV3("a", 0, 0, 6, 2),
                    QueryWidgetV3("b", 6, 0, 6, 2)));
                using (var presenter = env.CreatePresenter())
                {
                    presenter.SetBlockVisible("a", false);
                    Assert.DoesNotContain(presenter.DashboardWidgets, w => w.Id == "a");
                    Assert.False(presenter.GetWidgetDefinition("a").Layout.IsVisible);

                    string error;
                    Assert.True(presenter.TryCommitWidgetRect("b", new DashboardGridRect(0, 0, 6, 2), false, out error), error);
                    presenter.SetBlockVisible("a", true);
                    Assert.True(presenter.GetWidgetDefinition("a").Layout.IsVisible);
                    Assert.Equal(0, presenter.GetWidgetDefinition("a").Layout.X);
                    Assert.Equal(0, presenter.GetWidgetDefinition("a").Layout.Y);
                    Assert.True(presenter.GetWidgetDefinition("b").Layout.Y >= 2);
                    Assert.False(DashboardGridLayoutEngine.AnyVisibleOverlap(presenter.CurrentDefinition.Widgets));
                    Assert.Contains(presenter.DashboardWidgets, w => w.Id == "a");
                }
            }
        }

        [Fact]
        public void OpenV2_MigratesInMemory_NoWrite_FirstMutationWritesV3()
        {
            using (var env = new Env())
            {
                env.Store.Save(V2(QueryWidget("q1", 0, Scalar(TypeA), "Kpi", columnSpan: 1)));
                var path = env.Store.GetPath(ProjectA);
                var before = File.ReadAllBytes(path);
                using (var presenter = env.CreatePresenter())
                {
                    Assert.Equal(DashboardPersistenceV2.CurrentSchemaVersion, presenter.CurrentDefinition.SchemaVersion);
                    Assert.Equal(6, presenter.CurrentDefinition.Widgets[0].Layout.Width);
                    Assert.Equal(before, File.ReadAllBytes(path));
                    string error;
                    Assert.True(presenter.TryCommitWidgetRect("q1", new DashboardGridRect(6, 0, 6, 2), false, out error), error);
                }
                var loaded = env.Store.Load(ProjectA);
                Assert.Equal(DashboardPersistenceV2.CurrentSchemaVersion, loaded.Definition.SchemaVersion);
            }
        }

        [Fact]
        public void OpenV1_MigratesInMemory_NoWrite_FirstMutationWritesV3()
        {
            using (var env = new Env())
            {
                WriteV1(env);
                var v1Before = File.ReadAllBytes(env.V1Path);
                using (var presenter = env.CreatePresenter())
                {
                    Assert.Equal(DashboardPersistenceV2.CurrentSchemaVersion, presenter.CurrentDefinition.SchemaVersion);
                    Assert.False(File.Exists(env.Store.GetPath(ProjectA)));
                    presenter.SetBlockVisible(DashboardBlockIds.Kpi, false);
                    presenter.SetBlockVisible(DashboardBlockIds.Kpi, true);
                    Assert.Equal(v1Before, File.ReadAllBytes(env.V1Path));
                    Assert.Equal(DashboardPersistenceV2.CurrentSchemaVersion, env.Store.Load(ProjectA).Definition.SchemaVersion);
                }
            }
        }

        [Fact]
        public void V3Reopen_RestoresLogicalRect()
        {
            using (var env = new Env())
            {
                env.Store.Save(V3(QueryWidgetV3("q1", 2, 5, 6, 3)));
                using (var presenter = env.CreatePresenter())
                {
                    Assert.Equal(new DashboardGridRect(2, 5, 6, 3), RectOf(presenter, "q1"));
                }
                using (var presenter = env.CreatePresenter())
                {
                    Assert.Equal(new DashboardGridRect(2, 5, 6, 3), RectOf(presenter, "q1"));
                }
            }
        }

        [Fact]
        public void Degraded_EditModeUnavailable()
        {
            using (var env = new Env())
            {
                var path = env.Store.GetPath(ProjectA);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, "{not-json");
                using (var presenter = env.CreatePresenter())
                {
                    presenter.SetEditMode(true);
                    Assert.False(presenter.IsEditMode);
                    Assert.False(presenter.MutationsEnabled);
                    string error;
                    Assert.False(presenter.TryCommitWidgetRect("kpi", new DashboardGridRect(0, 0, 6, 2), false, out error));
                }
            }
        }

        private static DashboardGridRect RectOf(AnalyticsDashboardPresenter presenter, string id)
        {
            var layout = presenter.GetWidgetDefinition(id).Layout;
            return new DashboardGridRect(layout.X, layout.Y, layout.Width, layout.Height);
        }

        private static DashboardWidgetVm Vm(AnalyticsDashboardPresenter presenter, string id)
        {
            return presenter.DashboardWidgets.Single(w => w.Id == id);
        }

        private static DashboardDefinition V2(params DashboardWidgetDefinition[] widgets)
        {
            return new DashboardDefinition
            {
                SchemaVersion = 2,
                Id = DashboardPersistenceV2.DefaultDashboardId,
                Title = DashboardPersistenceV2.DefaultTitle,
                ProjectKey = ProjectA.ToString("D"),
                Widgets = widgets.ToList()
            };
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
            int order,
            DashboardWidgetQuery query,
            string visualization,
            int columnSpan = 2,
            string title = "Query")
        {
            return new DashboardWidgetDefinition
            {
                Id = id,
                Title = title,
                ContentKind = DashboardPersistenceV2.ContentQuery,
                Layout = new DashboardWidgetLayoutDefinition { Order = order, ColumnSpan = columnSpan, IsVisible = true },
                Query = DashboardQueryPersistence.ToDocument(query),
                Visualization = new DashboardVisualizationDefinition { Type = visualization }
            };
        }

        private static DashboardWidgetDefinition QueryWidgetV3(
            string id,
            int x,
            int y,
            int width,
            int height,
            string title = "Query",
            string visualization = "Kpi")
        {
            return new DashboardWidgetDefinition
            {
                Id = id,
                Title = title,
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
                Query = DashboardQueryPersistence.ToDocument(Scalar(TypeA)),
                Visualization = new DashboardVisualizationDefinition { Type = visualization }
            };
        }

        private static DashboardWidgetDefinition LegacyV3(string id, int x, int y, int width, int height, string kind)
        {
            return new DashboardWidgetDefinition
            {
                Id = id,
                Title = id,
                ContentKind = DashboardPersistenceV2.ContentLegacy,
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
                Legacy = new DashboardLegacyWidgetContent
                {
                    WidgetKind = kind,
                    ChartSource = "Types",
                    ChartKind = "HorizontalBar",
                    TopN = 12
                }
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

        private static void WriteV1(Env env)
        {
            new DashboardLayoutStore(env.V1Path).Save(DashboardLayoutStore.Default());
        }

        private sealed class Env : IDisposable
        {
            public Env()
            {
                Root = Path.Combine(Path.GetTempPath(), "PilotBim.Analytics.Tests", "db10-" + Guid.NewGuid().ToString("N"));
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
