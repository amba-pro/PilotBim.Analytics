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
    public sealed class DashboardDb12IntegrationTests
    {
        private static readonly Guid ProjectA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private const int TypeRemarks = 100;
        private const int TypeDocs = 200;
        private const string StatusName = "Status";
        private const string GroupName = "RemarkType";

        [Fact]
        public async Task Filter_UpdatesBoundWidgets_PreservesCacheAndCoordinator()
        {
            using (var env = new Env())
            {
                env.Store.Save(Def(
                    QueryWidget("kpi", "Kpi", true, 0, 0, 3, 2, TypeRemarks),
                    QueryWidget("byType", "Bar", false, 3, 0, 4, 3, TypeRemarks),
                    QueryWidget("byStatus", "Bar", false, 7, 0, 5, 3, TypeRemarks, StatusName),
                    QueryWidget("docs", "Kpi", true, 0, 3, 3, 2, TypeDocs)));
                var provider = env.RemarksProvider();
                using (var presenter = env.CreatePresenter(provider))
                {
                    presenter.ReplaceDataSession(null, Catalog(), Types());
                    await presenter.RefreshTask;
                    Assert.Equal(2, provider.Calls);
                    var coordinator = presenter.Coordinator;
                    var docsValue = Vm(presenter, "docs").KpiDisplayValue;
                    Assert.Equal(DashboardVisualizationFormat.Count(6), Vm(presenter, "kpi").KpiDisplayValue);
                    Assert.Equal(3, Vm(presenter, "byType").ChartSeries.Count);
                    Assert.Equal(3, Vm(presenter, "byType").ChartSeries[0].Value);

                    string error;
                    Assert.True(presenter.TrySaveDashboardFilter(Level("f1", StatusName, "Open", "kpi", "byType", "byStatus"), out error), error);
                    await presenter.RefreshTask;

                    Assert.Same(coordinator, presenter.Coordinator);
                    Assert.Equal(2, provider.Calls);
                    Assert.Equal(DashboardVisualizationFormat.Count(3), Vm(presenter, "kpi").KpiDisplayValue);
                    Assert.Equal(2, Vm(presenter, "byType").ChartSeries.Count);
                    Assert.Equal("A", Vm(presenter, "byType").ChartSeries[0].Label);
                    Assert.Equal(2, Vm(presenter, "byType").ChartSeries[0].Value);
                    Assert.Equal(docsValue, Vm(presenter, "docs").KpiDisplayValue);
                    Assert.True(presenter.GetWidgetDefinition("kpi").Query.Filters == null
                        || presenter.GetWidgetDefinition("kpi").Query.Filters.Count == 0);
                    Assert.Equal(4, env.Store.Load(ProjectA).Definition.SchemaVersion);
                    Assert.Single(presenter.DashboardFilterChips);
                }
            }
        }

        [Fact]
        public async Task Filter_AppliesBeforeTopN_ZeroAndIncomplete()
        {
            using (var env = new Env())
            {
                var grouped = QueryWidget("top", "Bar", false, 0, 0, 6, 3, TypeRemarks);
                grouped.Query.Limit = 2;
                env.Store.Save(Def(grouped, QueryWidget("kpi", "Kpi", true, 6, 0, 3, 2, TypeRemarks)));
                using (var presenter = env.CreatePresenter(env.RemarksProvider()))
                {
                    presenter.ReplaceDataSession(null, Catalog(), Types());
                    await presenter.RefreshTask;
                    Assert.Equal(2, Vm(presenter, "top").ChartSeries.Count);
                    Assert.Equal("C", Vm(presenter, "top").ChartSeries[0].Label);

                    string error;
                    Assert.True(presenter.TrySaveDashboardFilter(Level("open", StatusName, "Open", "top", "kpi"), out error), error);
                    await presenter.RefreshTask;
                    Assert.Equal(2, Vm(presenter, "top").ChartSeries.Count);
                    Assert.Equal("A", Vm(presenter, "top").ChartSeries[0].Label);
                    Assert.Equal("B", Vm(presenter, "top").ChartSeries[1].Label);

                    Assert.True(presenter.TrySaveDashboardFilter(Level("none", StatusName, "Missing", "top", "kpi"), out error), error);
                    await presenter.RefreshTask;
                    Assert.Equal(DashboardQueryWidgetRuntimeStatus.Success, Vm(presenter, "kpi").QueryStatus);
                    Assert.Equal(DashboardVisualizationFormat.Count(0), Vm(presenter, "kpi").KpiDisplayValue);
                    Assert.Equal(DashboardQueryWidgetRuntimeStatus.Empty, Vm(presenter, "top").QueryStatus);
                }
            }

            using (var env = new Env())
            {
                env.Store.Save(Def(QueryWidget("kpi", "Kpi", true, 0, 0, 3, 2, TypeRemarks)));
                var field = DashboardFieldIds.Attribute(TypeRemarks, StatusName);
                var provider = new FakeProvider
                {
                    Factory = id => new DashboardTypeDataset(
                        id, 1, 1, DashboardTypeCoverage.Complete, "complete",
                        new[] { Row(id, "A", "Open") }, 0, 1, new[] { field })
                };
                using (var presenter = env.CreatePresenter(provider))
                {
                    presenter.ReplaceDataSession(null, Catalog(), Types());
                    await presenter.RefreshTask;
                    string error;
                    Assert.True(presenter.TrySaveDashboardFilter(Level("f", StatusName, "Open", "kpi"), out error), error);
                    await presenter.RefreshTask;
                    Assert.Equal(DashboardQueryWidgetRuntimeStatus.Incomplete, Vm(presenter, "kpi").QueryStatus);
                }
            }
        }

        [Fact]
        public async Task Bindings_DeleteTypeChangeUnionRefresh_SaveFailure()
        {
            using (var env = new Env())
            {
                env.Store.Save(Def(
                    QueryWidget("a", "Kpi", true, 0, 0, 3, 2, TypeRemarks),
                    QueryWidget("b", "Kpi", true, 3, 0, 3, 2, TypeRemarks),
                    QueryWidget("c", "Kpi", true, 6, 0, 3, 2, TypeRemarks)));
                var provider = env.RemarksProvider();
                using (var presenter = env.CreatePresenter(provider))
                {
                    presenter.ReplaceDataSession(null, Catalog(), Types());
                    await presenter.RefreshTask;
                    var coordinator = presenter.Coordinator;
                    string error;
                    Assert.True(presenter.TrySaveDashboardFilter(Level("f1", StatusName, "Open", "a", "b"), out error), error);
                    await presenter.RefreshTask;
                    Assert.Equal(DashboardVisualizationFormat.Count(3), Vm(presenter, "a").KpiDisplayValue);
                    Assert.Equal(DashboardVisualizationFormat.Count(6), Vm(presenter, "c").KpiDisplayValue);

                    var edited = Level("f1", StatusName, "Open", "b", "c");
                    Assert.True(presenter.TrySaveDashboardFilter(edited, out error), error);
                    await presenter.RefreshTask;
                    Assert.Equal(DashboardVisualizationFormat.Count(6), Vm(presenter, "a").KpiDisplayValue);
                    Assert.Equal(DashboardVisualizationFormat.Count(3), Vm(presenter, "b").KpiDisplayValue);
                    Assert.Equal(DashboardVisualizationFormat.Count(3), Vm(presenter, "c").KpiDisplayValue);
                    Assert.Same(coordinator, presenter.Coordinator);

                    presenter.RemoveWidget("b");
                    Assert.DoesNotContain(
                        presenter.GetDashboardFilter("f1").TargetWidgetIds,
                        id => id == "b");
                    Assert.NotNull(presenter.GetDashboardFilter("f1"));

                    Assert.True(presenter.TrySaveDashboardFilter(Level("f1", StatusName, "Open", "a", "c"), out error), error);
                    var changed = QueryWidget("a", "Kpi", true, 0, 0, 3, 2, TypeDocs);
                    Assert.True(presenter.TrySaveQueryWidget(changed, out error), error);
                    Assert.DoesNotContain(
                        presenter.GetDashboardFilter("f1").TargetWidgetIds,
                        id => id == "a");
                    Assert.Contains("c", presenter.GetDashboardFilter("f1").TargetWidgetIds);
                    Assert.Same(coordinator, presenter.Coordinator);
                }
            }

            using (var env = new Env())
            {
                env.Store.Save(Def(QueryWidget("kpi", "Kpi", true, 0, 0, 3, 2, TypeRemarks)));
                using (var presenter = env.CreatePresenter(env.RemarksProvider()))
                {
                    presenter.ReplaceDataSession(null, Catalog(), Types());
                    await presenter.RefreshTask;
                    var path = env.Store.GetPath(ProjectA);
                    using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        string error;
                        Assert.False(presenter.TrySaveDashboardFilter(Level("f1", StatusName, "Open", "kpi"), out error));
                        Assert.True(presenter.LastSaveFailed);
                        Assert.Null(presenter.GetDashboardFilter("f1"));
                        Assert.Equal(DashboardVisualizationFormat.Count(6), Vm(presenter, "kpi").KpiDisplayValue);
                    }
                }
            }
        }

        [Fact]
        public async Task MissingField_Unavailable_NoRemap_RestoredWhenIdentityReturns()
        {
            using (var env = new Env())
            {
                var def = Def(QueryWidget("kpi", "Kpi", true, 0, 0, 3, 2, TypeRemarks));
                def.DashboardFilters.Add(Level("stale", "OldStatus", "Open", "kpi"));
                env.Store.Save(def);
                using (var presenter = env.CreatePresenter(env.RemarksProvider()))
                {
                    presenter.ReplaceDataSession(null, Catalog(), Types());
                    await presenter.RefreshTask;
                    Assert.Equal(DashboardVisualizationFormat.Count(6), Vm(presenter, "kpi").KpiDisplayValue);
                    Assert.True(presenter.DashboardFilterChips[0].FieldUnavailable);
                    Assert.Equal("stale", presenter.GetDashboardFilter("stale").Id);
                    Assert.Equal(DashboardFieldIds.Attribute(TypeRemarks, "OldStatus"), presenter.GetDashboardFilter("stale").FieldId);

                    var restored = new PilotFieldCatalogBuilder().Build(new[]
                    {
                        TypeRec(TypeRemarks, GroupName, StatusName, "OldStatus"),
                        TypeRec(TypeDocs, "Owner")
                    });
                    presenter.ReplaceDataSession(null, restored, Types());
                    await presenter.RefreshTask;
                    Assert.False(presenter.DashboardFilterChips[0].FieldUnavailable);
                    Assert.Equal(DashboardVisualizationFormat.Count(0), Vm(presenter, "kpi").KpiDisplayValue);
                }
            }
        }

        [Fact]
        public async Task FirstTypeMaterializesOnce_UnrelatedTypeNotLoaded()
        {
            using (var env = new Env())
            {
                env.Store.Save(Def(
                    QueryWidget("a", "Kpi", true, 0, 0, 3, 2, TypeRemarks),
                    QueryWidget("b", "Kpi", true, 3, 0, 3, 2, TypeRemarks),
                    QueryWidget("c", "Kpi", true, 6, 0, 3, 2, TypeRemarks)));
                var provider = env.RemarksProvider();
                using (var presenter = env.CreatePresenter(provider))
                {
                    presenter.ReplaceDataSession(null, Catalog(), Types());
                    await presenter.RefreshTask;
                    Assert.Equal(1, provider.Calls);
                    Assert.True(provider.Seen.SetEquals(new[] { TypeRemarks }));
                    string error;
                    Assert.True(presenter.TrySaveDashboardFilter(Level("f1", StatusName, "Open", "a", "b", "c"), out error), error);
                    await presenter.RefreshTask;
                    Assert.Equal(1, provider.Calls);
                }
            }
        }

        [Fact]
        public async Task RemoveFilter_RestoresBase_SameCoordinator()
        {
            using (var env = new Env())
            {
                env.Store.Save(Def(QueryWidget("kpi", "Kpi", true, 0, 0, 3, 2, TypeRemarks)));
                var provider = env.RemarksProvider();
                using (var presenter = env.CreatePresenter(provider))
                {
                    presenter.ReplaceDataSession(null, Catalog(), Types());
                    await presenter.RefreshTask;
                    var coordinator = presenter.Coordinator;
                    string error;
                    Assert.True(presenter.TrySaveDashboardFilter(Level("f1", StatusName, "Open", "kpi"), out error), error);
                    await presenter.RefreshTask;
                    Assert.True(presenter.TryRemoveDashboardFilter("f1", out error), error);
                    await presenter.RefreshTask;
                    Assert.Same(coordinator, presenter.Coordinator);
                    Assert.Equal(1, provider.Calls);
                    Assert.Equal(DashboardVisualizationFormat.Count(6), Vm(presenter, "kpi").KpiDisplayValue);
                    Assert.Empty(presenter.DashboardFilterChips);
                }
            }
        }

        private static DashboardWidgetVm Vm(AnalyticsDashboardPresenter presenter, string id)
        {
            return presenter.DashboardWidgets.Single(w => w.Id == id);
        }

        private static DashboardDefinition Def(params DashboardWidgetDefinition[] widgets)
        {
            return new DashboardDefinition
            {
                SchemaVersion = DashboardPersistenceV2.CurrentSchemaVersion,
                Id = DashboardPersistenceV2.DefaultDashboardId,
                Title = DashboardPersistenceV2.DefaultTitle,
                ProjectKey = ProjectA.ToString("D"),
                Widgets = widgets.ToList(),
                DashboardFilters = new List<DashboardLevelFilterDefinition>()
            };
        }

        private static DashboardWidgetDefinition QueryWidget(
            string id,
            string visualization,
            bool scalar,
            int x,
            int y,
            int width,
            int height,
            int typeId,
            string dimensionName = GroupName)
        {
            var query = new DashboardWidgetQuery(
                DashboardQueryScopeKind.CurrentProject,
                scalar ? null : DashboardFieldIds.Attribute(typeId, dimensionName),
                DashboardQueryMeasure.Count,
                DashboardQuerySort.ValueDescending,
                null,
                typeId);
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

        private static DashboardLevelFilterDefinition Level(string id, string fieldName, string text, params string[] targets)
        {
            var field = DashboardFieldIds.Attribute(TypeRemarks, fieldName);
            DashboardFilterDocument doc;
            string error;
            Assert.True(DashboardQueryPersistence.TryToFilterDocument(
                new DashboardFilterDefinition(field, DashboardFilterOperator.Equals, DashboardFilterValue.Text(text)),
                out doc,
                out error), error);
            return new DashboardLevelFilterDefinition
            {
                Id = id,
                Title = fieldName,
                EntityTypeId = TypeRemarks,
                FieldId = field,
                Operator = doc.Operator,
                ValueKind = doc.ValueKind,
                Value = doc.Value,
                TargetWidgetIds = targets.ToList()
            };
        }

        private static DashboardFieldCatalog Catalog()
        {
            return new PilotFieldCatalogBuilder().Build(new[]
            {
                TypeRec(TypeRemarks, GroupName, StatusName),
                TypeRec(TypeDocs, "Owner")
            });
        }

        private static IReadOnlyList<DashboardObjectTypeOption> Types()
        {
            return DashboardObjectTypeOption.FromInventory(new[]
            {
                TypeRec(TypeRemarks, GroupName, StatusName),
                TypeRec(TypeDocs, "Owner")
            });
        }

        private static TypeInventoryRecord TypeRec(int typeId, params string[] attrs)
        {
            var list = new List<AttributeInventoryRecord>();
            for (var i = 0; i < attrs.Length; i++)
            {
                list.Add(new AttributeInventoryRecord
                {
                    Name = attrs[i],
                    AttributeId = attrs[i],
                    Title = attrs[i],
                    ValueType = "String"
                });
            }
            return new TypeInventoryRecord
            {
                TypeId = typeId,
                Name = "type" + typeId,
                Title = "Type " + typeId,
                Attributes = list
            };
        }

        private static DashboardObjectRow Row(int typeId, string group, string status)
        {
            var map = new Dictionary<string, DashboardFieldValue>(StringComparer.Ordinal)
            {
                {
                    DashboardFieldIds.Attribute(typeId, GroupName),
                    new DashboardFieldValue(DashboardFieldType.Text, group, group, group)
                },
                {
                    DashboardFieldIds.Attribute(typeId, StatusName),
                    new DashboardFieldValue(DashboardFieldType.Text, status, status, status)
                }
            };
            return new DashboardObjectRow(Guid.NewGuid(), typeId, null, map);
        }

        private sealed class Env : IDisposable
        {
            public Env()
            {
                Root = Path.Combine(Path.GetTempPath(), "PilotBim.Analytics.Tests", "db12-" + Guid.NewGuid().ToString("N"));
                Store = new DashboardDefinitionStore(Path.Combine(Root, "Dashboards"));
            }

            public string Root { get; private set; }
            public DashboardDefinitionStore Store { get; private set; }

            public FakeProvider RemarksProvider()
            {
                return new FakeProvider
                {
                    Factory = id =>
                    {
                        if (id == TypeDocs)
                            return Complete(id, Row(id, "Doc", "Any"), Row(id, "Doc", "Any"));
                        return Complete(
                            id,
                            Row(id, "A", "Open"),
                            Row(id, "A", "Open"),
                            Row(id, "B", "Open"),
                            Row(id, "C", "Closed"),
                            Row(id, "C", "Closed"),
                            Row(id, "C", "Closed"));
                    }
                };
            }

            public AnalyticsDashboardPresenter CreatePresenter(IDashboardTypeDatasetProvider provider)
            {
                var runtime = new AnalyticsDashboardRuntimeOptions
                {
                    ProjectKey = ProjectA,
                    DefinitionStore = Store,
                    LayoutStore = new DashboardLayoutStore(Path.Combine(Root, "dashboard-layout.json")),
                    TypeDatasetProvider = provider,
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

        private static DashboardTypeDataset Complete(int typeId, params DashboardObjectRow[] rows)
        {
            var list = rows ?? new DashboardObjectRow[0];
            return new DashboardTypeDataset(typeId, list.Length, list.Length, DashboardTypeCoverage.Complete, "complete", list, 0, 0);
        }

        private sealed class FakeProvider : IDashboardTypeDatasetProvider
        {
            public int Calls;
            public HashSet<int> Seen = new HashSet<int>();
            public Func<int, DashboardTypeDataset> Factory;

            public DashboardTypeDataset Materialize(int typeId, CancellationToken cancellationToken)
            {
                Calls++;
                Seen.Add(typeId);
                return Factory(typeId);
            }
        }
    }
}
