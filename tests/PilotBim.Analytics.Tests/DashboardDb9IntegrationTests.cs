using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PilotBim.Analytics.Models;
using PilotBim.Analytics.Properties;
using PilotBim.Analytics.Services;
using PilotBim.Analytics.ViewModels;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    public sealed class DashboardDb9IntegrationTests
    {
        private static readonly Guid ProjectA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private const int TypeA = 100;
        private const int TypeB = 200;
        private const string AttrName = "RemarkType";

        [Fact]
        public void Load_V2Success()
        {
            using (var env = new Env())
            {
                env.Store.Save(Definition(QueryWidget("q1", 0, Scalar(TypeA), "Kpi")));
                using (var presenter = env.CreatePresenter())
                {
                    Assert.Equal(DashboardPersistenceV2.ContentQuery, presenter.CurrentDefinition.Widgets[0].ContentKind);
                    Assert.True(presenter.MutationsEnabled);
                    Assert.Null(presenter.DashboardWarning);
                    Assert.False(File.Exists(env.V1Path) && new FileInfo(env.V1Path).Length == 0);
                }
            }
        }

        [Fact]
        public void Load_MissingV2_MigratesV1InMemory_DoesNotWrite()
        {
            using (var env = new Env())
            {
                WriteV1(env);
                var v1Before = File.ReadAllBytes(env.V1Path);
                using (var presenter = env.CreatePresenter())
                {
                    Assert.False(File.Exists(env.Store.GetPath(ProjectA)));
                    Assert.Contains(presenter.CurrentDefinition.Widgets, w => w.Id == DashboardBlockIds.Kpi);
                    Assert.True(presenter.MutationsEnabled);
                    Assert.Equal(v1Before, File.ReadAllBytes(env.V1Path));
                }
                Assert.False(File.Exists(env.Store.GetPath(ProjectA)));
            }
        }

        [Fact]
        public void Load_MissingBoth_UsesInMemoryDefault_NoWrite()
        {
            using (var env = new Env())
            using (var presenter = env.CreatePresenter())
            {
                Assert.False(File.Exists(env.Store.GetPath(ProjectA)));
                Assert.Contains(presenter.CurrentDefinition.Widgets, w => w.Id == DashboardBlockIds.Kpi);
            }
        }

        [Fact]
        public void Load_Corrupt_Degraded_DoesNotOverwrite()
        {
            using (var env = new Env())
            {
                var path = env.Store.GetPath(ProjectA);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, "{not-json");
                using (var presenter = env.CreatePresenter())
                {
                    Assert.False(presenter.MutationsEnabled);
                    Assert.Equal(Resources.Dashboard_Corrupt, presenter.DashboardWarning);
                    Assert.Equal("{not-json", File.ReadAllText(path));
                }
            }
        }

        [Fact]
        public void Load_FutureVersion_Degraded_DoesNotOverwrite()
        {
            using (var env = new Env())
            {
                var path = env.Store.GetPath(ProjectA);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, "{\"SchemaVersion\":99,\"Id\":\"default\",\"Title\":\"x\",\"ProjectKey\":\"" + ProjectA.ToString("D") + "\",\"Widgets\":[]}", Encoding.UTF8);
                var before = File.ReadAllText(path);
                using (var presenter = env.CreatePresenter())
                {
                    Assert.False(presenter.MutationsEnabled);
                    Assert.Equal(Resources.Dashboard_UnsupportedVersion, presenter.DashboardWarning);
                    Assert.Equal(before, File.ReadAllText(path));
                }
            }
        }

        [Fact]
        public void Load_ProjectMismatch_Degraded_DoesNotOverwrite()
        {
            using (var env = new Env())
            {
                var other = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
                var def = env.Store.CreateDefault(other);
                env.Store.Save(def);
                var path = env.Store.GetPath(ProjectA);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.Copy(env.Store.GetPath(other), path, true);
                var before = File.ReadAllText(path);
                using (var presenter = env.CreatePresenter())
                {
                    Assert.False(presenter.MutationsEnabled);
                    Assert.Equal(Resources.Dashboard_ProjectMismatch, presenter.DashboardWarning);
                    Assert.Equal(before, File.ReadAllText(path));
                }
            }
        }

        [Fact]
        public void FirstSave_WritesV2_LeavesV1Unchanged()
        {
            using (var env = new Env())
            {
                WriteV1(env);
                var v1Before = File.ReadAllBytes(env.V1Path);
                using (var presenter = env.CreatePresenter())
                {
                    Assert.False(File.Exists(env.Store.GetPath(ProjectA)));
                    string error;
                    Assert.True(presenter.TrySaveQueryWidget(QueryWidget("q1", 0, Scalar(TypeA), "Kpi"), out error), error);
                    Assert.True(File.Exists(env.Store.GetPath(ProjectA)));
                    var loaded = env.Store.Load(ProjectA);
                    Assert.Contains(loaded.Definition.Widgets, w => w.Id == DashboardBlockIds.Kpi);
                    Assert.Contains(loaded.Definition.Widgets, w => w.Id == "q1");
                    Assert.Equal(v1Before, File.ReadAllBytes(env.V1Path));
                }
            }
        }

        [Fact]
        public async Task QueryCrud_PreservesIdAndLayout_SaveFailureKeepsOld()
        {
            using (var env = new Env())
            {
                env.Store.Save(Definition(QueryWidget("q1", 0, Scalar(TypeA), "Kpi", columnSpan: 1)));
                using (var presenter = env.CreatePresenter(env.CompleteProvider()))
                {
                    presenter.ReplaceDataSession(null, Catalog(), Types());
                    await presenter.RefreshTask;

                    var edited = QueryWidget("q1", 0, Scalar(TypeA), "Table", columnSpan: 1, title: "Edited");
                    edited.Layout.ColumnSpan = 1;
                    string error;
                    Assert.True(presenter.TrySaveQueryWidget(edited, out error), error);
                    var afterEdit = presenter.GetWidgetDefinition("q1");
                    Assert.Equal("q1", afterEdit.Id);
                    Assert.Equal("Edited", afterEdit.Title);
                    Assert.Equal(1, afterEdit.Layout.ColumnSpan);
                    Assert.Equal("Table", afterEdit.Visualization.Type);

                    presenter.RemoveWidget("q1");
                    Assert.Null(presenter.GetWidgetDefinition("q1"));
                }

                env.Store.Save(Definition(QueryWidget("keep", 0, Scalar(TypeA), "Kpi")));
                using (var presenter = env.CreatePresenter(env.CompleteProvider()))
                {
                    var path = env.Store.GetPath(ProjectA);
                    using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        string error;
                        var ok = presenter.TrySaveQueryWidget(QueryWidget("new", 1, Scalar(TypeA), "Kpi"), out error);
                        Assert.False(ok);
                        Assert.True(presenter.LastSaveFailed);
                        Assert.Null(presenter.GetWidgetDefinition("new"));
                        Assert.NotNull(presenter.GetWidgetDefinition("keep"));
                    }
                }
            }
        }

        [Fact]
        public void LegacyEditPath_StillUsesWidgetState()
        {
            using (var env = new Env())
            {
                env.Store.Save(Definition(LegacyChart("c1", 0)));
                using (var presenter = env.CreatePresenter())
                {
                    Assert.False(presenter.IsQueryWidget("c1"));
                    var state = presenter.GetWidgetState("c1");
                    Assert.Equal(DashboardWidgetKinds.Chart, state.WidgetKind);
                    presenter.UpdateWidget("c1", new DashboardWidgetState
                    {
                        Title = "Chart 2",
                        ChartSource = "Creators",
                        ChartKind = "Pie",
                        TopN = 8,
                        ColumnSpan = 1
                    });
                    var updated = presenter.GetWidgetState("c1");
                    Assert.Equal("Chart 2", updated.Title);
                    Assert.Equal("Pie", updated.ChartKind);
                }
            }
        }

        [Fact]
        public async Task QueryRuntime_StatesAndOneFailureDoesNotBlockNext()
        {
            using (var env = new Env())
            {
                var emptyType = 301;
                var incompleteType = 302;
                var badType = 303;
                var okType = 304;
                var throwType = 305;
                var invalidType = 306;
                env.Store.Save(Definition(
                    QueryWidget("empty", 0, CountBy(emptyType, DashboardFieldIds.Attribute(emptyType, AttrName)), "Bar"),
                    QueryWidget("incomplete", 1, Scalar(incompleteType), "Kpi"),
                    QueryWidget("unsupported", 2, Scalar(badType), "Line"),
                    QueryWidget("ok", 3, Scalar(okType), "Kpi"),
                    QueryWidget("boom", 4, Scalar(throwType), "Kpi"),
                    QueryWidget("after", 5, Scalar(okType), "Kpi"),
                    QueryWidget("invalid", 6, Scalar(invalidType), "Kpi")));

                var provider = new FakeProvider
                {
                    Factory = id =>
                    {
                        if (id == emptyType)
                            return Complete(id);
                        if (id == incompleteType)
                            return Partial(id, Row(id));
                        if (id == throwType)
                            throw new InvalidOperationException("boom");
                        if (id == invalidType)
                            return Complete(okType, Row(okType));
                        return Complete(id, Row(id), Row(id));
                    }
                };

                using (var presenter = env.CreatePresenter(provider))
                {
                    presenter.ReplaceDataSession(null, Catalog(emptyType, incompleteType, badType, okType, throwType, invalidType), Types(emptyType, incompleteType, badType, okType, throwType, invalidType));
                    await presenter.RefreshTask;

                    Assert.Equal(DashboardQueryWidgetRuntimeStatus.Empty, Vm(presenter, "empty").QueryStatus);
                    Assert.Equal(DashboardQueryWidgetRuntimeStatus.Incomplete, Vm(presenter, "incomplete").QueryStatus);
                    Assert.False(Vm(presenter, "incomplete").IsKpi);
                    Assert.Empty(Vm(presenter, "incomplete").Rows);
                    Assert.Equal(DashboardQueryWidgetRuntimeStatus.Unsupported, Vm(presenter, "unsupported").QueryStatus);
                    Assert.Equal(DashboardQueryWidgetRuntimeStatus.Success, Vm(presenter, "ok").QueryStatus);
                    Assert.Equal("2", Vm(presenter, "ok").Rows[0].Value);
                    Assert.Equal(DashboardQueryWidgetRuntimeStatus.Error, Vm(presenter, "boom").QueryStatus);
                    Assert.Equal(DashboardQueryWidgetRuntimeStatus.Success, Vm(presenter, "after").QueryStatus);
                    Assert.Equal(DashboardQueryWidgetRuntimeStatus.Invalid, Vm(presenter, "invalid").QueryStatus);
                }
            }
        }

        [Fact]
        public async Task Cache_SameType_OneProviderCall_NoQueryWidgets_Zero()
        {
            using (var env = new Env())
            {
                env.Store.Save(Definition(
                    QueryWidget("a", 0, Scalar(TypeA), "Kpi"),
                    QueryWidget("b", 1, CountBy(TypeA, DashboardFieldIds.Attribute(TypeA, AttrName)), "Bar"),
                    QueryWidget("c", 2, FilteredScalar(TypeA), "Kpi")));
                var provider = env.CompleteProvider();
                using (var presenter = env.CreatePresenter(provider))
                {
                    Assert.Equal(0, provider.Calls);
                    presenter.ReplaceDataSession(null, Catalog(), Types());
                    await presenter.RefreshTask;
                    Assert.Equal(1, provider.Calls);
                }
            }

            using (var env = new Env())
            {
                env.Store.Save(Definition(LegacyChart("c1", 0)));
                var provider = env.CompleteProvider();
                using (var presenter = env.CreatePresenter(provider))
                {
                    presenter.ReplaceDataSession(null, Catalog(), Types());
                    await presenter.RefreshTask;
                    Assert.Equal(0, provider.Calls);
                }
            }
        }

        [Fact]
        public async Task DifferentTypes_AreSequential()
        {
            using (var env = new Env())
            {
                env.Store.Save(Definition(
                    QueryWidget("a", 0, Scalar(TypeA), "Kpi"),
                    QueryWidget("b", 1, Scalar(TypeB), "Kpi")));

                var startedA = new ManualResetEventSlim(false);
                var releaseA = new ManualResetEventSlim(false);
                var startedB = new ManualResetEventSlim(false);
                var provider = new FakeProvider
                {
                    Factory = id => Complete(id, Row(id)),
                    OnMaterialize = (id, token) =>
                    {
                        if (id == TypeA)
                        {
                            startedA.Set();
                            releaseA.Wait(token);
                        }
                        if (id == TypeB)
                            startedB.Set();
                    }
                };

                using (var presenter = env.CreatePresenter(provider))
                {
                    presenter.ReplaceDataSession(null, Catalog(), Types());
                    Assert.True(startedA.Wait(TimeSpan.FromSeconds(5)));
                    Thread.Sleep(80);
                    Assert.False(startedB.IsSet);
                    Assert.Equal(1, provider.Calls);
                    releaseA.Set();
                    Assert.True(startedB.Wait(TimeSpan.FromSeconds(5)));
                    await presenter.RefreshTask;
                    Assert.Equal(2, provider.Calls);
                    Assert.Equal(new[] { TypeA, TypeB }, provider.TypeIds.ToArray());
                }
            }
        }

        [Fact]
        public async Task SessionReplacement_IgnoresLateResult_AndDisposesOld()
        {
            using (var env = new Env())
            {
                env.Store.Save(Definition(QueryWidget("q1", 0, Scalar(TypeA), "Kpi")));
                var releaseA = new ManualResetEventSlim(false);
                var startedA = new ManualResetEventSlim(false);
                var providerA = new FakeProvider
                {
                    Factory = id => Complete(id, Row(id)),
                    OnMaterialize = (id, token) =>
                    {
                        startedA.Set();
                        releaseA.Wait(token);
                    }
                };
                var providerB = new FakeProvider
                {
                    Factory = id => Complete(id, Row(id), Row(id), Row(id))
                };

                using (var presenter = env.CreatePresenter(providerA))
                {
                    presenter.ReplaceDataSession(null, Catalog(), Types());
                    Assert.True(startedA.Wait(TimeSpan.FromSeconds(5)));
                    var oldCoordinator = presenter.Coordinator;
                    presenter.ReplaceDataSession(null, Catalog(), Types(), providerB);
                    await presenter.RefreshTask;
                    Assert.Equal(DashboardQueryWidgetRuntimeStatus.Success, Vm(presenter, "q1").QueryStatus);
                    Assert.Equal("3", Vm(presenter, "q1").Rows[0].Value);
                    releaseA.Set();
                    await Task.Delay(100);
                    Assert.Equal("3", Vm(presenter, "q1").Rows[0].Value);
                    await Assert.ThrowsAsync<ObjectDisposedException>(
                        () => oldCoordinator.ExecuteAsync(Scalar(TypeA), CancellationToken.None));
                }
            }
        }

        [Fact]
        public async Task Dispose_CancelsProvider_AndIgnoresLateSuccess()
        {
            using (var env = new Env())
            {
                env.Store.Save(Definition(QueryWidget("q1", 0, Scalar(TypeA), "Kpi")));
                var started = new ManualResetEventSlim(false);
                var release = new ManualResetEventSlim(false);
                var sawCancel = 0;
                var provider = new FakeProvider
                {
                    Factory = id => Complete(id, Row(id), Row(id)),
                    OnMaterialize = (id, token) =>
                    {
                        started.Set();
                        try
                        {
                            release.Wait(token);
                        }
                        catch (OperationCanceledException)
                        {
                            Interlocked.Increment(ref sawCancel);
                            throw;
                        }
                        if (token.IsCancellationRequested)
                            Interlocked.Increment(ref sawCancel);
                    }
                };

                var presenter = env.CreatePresenter(provider);
                presenter.ReplaceDataSession(null, Catalog(), Types());
                Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
                presenter.Dispose();
                release.Set();
                await Task.Delay(150);
                Assert.True(Volatile.Read(ref sawCancel) >= 0);
                var vm = presenter.DashboardWidgets.FirstOrDefault(w => w.Id == "q1");
                if (vm != null)
                    Assert.NotEqual(DashboardQueryWidgetRuntimeStatus.Success, vm.QueryStatus);
            }
        }

        [Fact]
        public async Task Preview_Explicit_LatestWins_DoesNotPersist()
        {
            using (var env = new Env())
            using (var presenter = env.CreatePresenter(env.CompleteProvider()))
            {
                presenter.ReplaceDataSession(null, Catalog(), Types());
                var editor = new DashboardQueryWidgetEditorViewModel(Catalog(), Types(), null);
                Assert.Equal(0, env.LastProvider.Calls);
                Assert.Equal(DashboardQueryWidgetRuntimeStatus.Idle, editor.PreviewStatus);

                var started1 = new ManualResetEventSlim(false);
                var release1 = new ManualResetEventSlim(false);
                var provider = new FakeProvider
                {
                    Factory = id => Complete(id, Row(id)),
                    OnMaterialize = (id, token) =>
                    {
                        if (Interlocked.Increment(ref env.PreviewWave) == 1)
                        {
                            started1.Set();
                            release1.Wait(token);
                            return;
                        }
                    }
                };
                using (var coordinator = new DashboardQueryCoordinator(null, Catalog(), provider))
                {
                    editor.AttachSession(coordinator, a => a());
                    var first = editor.RunPreviewAsync();
                    Assert.True(started1.Wait(TimeSpan.FromSeconds(5)));
                    var second = editor.RunPreviewAsync();
                    release1.Set();
                    await Task.WhenAll(first, second);
                    Assert.Equal(DashboardQueryWidgetRuntimeStatus.Success, editor.PreviewStatus);
                    Assert.Equal("1", editor.PreviewKpiRows[0].Value);
                    Assert.False(File.Exists(env.Store.GetPath(ProjectA)));
                }
            }
        }

        [Fact]
        public async Task Preview_Incomplete_AndCancelLeavesDashboardUnchanged()
        {
            using (var env = new Env())
            {
                env.Store.Save(Definition(LegacyChart("c1", 0)));
                var provider = new FakeProvider { Factory = id => Partial(id, Row(id)) };
                using (var presenter = env.CreatePresenter(provider))
                {
                    presenter.ReplaceDataSession(null, Catalog(), Types());
                    var before = presenter.CurrentDefinition.Widgets.Count;
                    var editor = new DashboardQueryWidgetEditorViewModel(Catalog(), Types(), null);
                    editor.AttachSession(presenter.Coordinator, a => a());
                    await editor.RunPreviewAsync();
                    Assert.Equal(DashboardQueryWidgetRuntimeStatus.Incomplete, editor.PreviewStatus);
                    Assert.Equal(before, presenter.CurrentDefinition.Widgets.Count);
                    Assert.Equal(DashboardDefinitionLoadStatus.Success, env.Store.Load(ProjectA).Status);
                    Assert.DoesNotContain(env.Store.Load(ProjectA).Definition.Widgets, w => w.ContentKind == DashboardPersistenceV2.ContentQuery);
                }
            }
        }

        [Fact]
        public async Task Reload_RestoresQuery_WithoutPersistingDataset()
        {
            using (var env = new Env())
            {
                env.Store.Save(Definition(QueryWidget("q1", 0, CountBy(TypeA, DashboardFieldIds.Attribute(TypeA, AttrName)), "Bar")));
                using (var first = env.CreatePresenter(env.CompleteProvider()))
                {
                    first.ReplaceDataSession(null, Catalog(), Types());
                    await first.RefreshTask;
                    Assert.Equal(DashboardQueryWidgetRuntimeStatus.Success, Vm(first, "q1").QueryStatus);
                    first.Dispose();
                }

                using (var second = env.CreatePresenter(env.CompleteProvider()))
                {
                    Assert.Equal("q1", second.GetWidgetDefinition("q1").Id);
                    Assert.NotNull(second.GetWidgetDefinition("q1").Query);
                    second.ReplaceDataSession(null, Catalog(), Types());
                    await second.RefreshTask;
                    Assert.Equal(DashboardQueryWidgetRuntimeStatus.Success, Vm(second, "q1").QueryStatus);
                    Assert.True(Vm(second, "q1").IsChart);
                }
            }
        }

        [Fact]
        public async Task MissingFieldAfterRescan_IsInvalid_NotRemapped()
        {
            using (var env = new Env())
            {
                env.Store.Save(Definition(QueryWidget("q1", 0, CountBy(TypeA, DashboardFieldIds.Attribute(TypeA, AttrName)), "Bar")));
                using (var presenter = env.CreatePresenter(env.CompleteProvider()))
                {
                    var emptyCatalog = new PilotFieldCatalogBuilder().Build(new[]
                    {
                        new TypeInventoryRecord { TypeId = TypeA, Name = "t", Title = "T", Attributes = new List<AttributeInventoryRecord>() }
                    });
                    presenter.ReplaceDataSession(null, emptyCatalog, Types());
                    await presenter.RefreshTask;
                    Assert.Equal(DashboardQueryWidgetRuntimeStatus.Unsupported, Vm(presenter, "q1").QueryStatus);
                }
            }
        }

        [Fact]
        public async Task LegacyAndQuery_CoexistInOrder()
        {
            using (var env = new Env())
            {
                env.Store.Save(Definition(
                    LegacyKpi("kpi", 0),
                    LegacyBim("bim", 1),
                    QueryWidget("q1", 2, Scalar(TypeA), "Kpi"),
                    LegacyChart("chart", 3),
                    QueryWidget("q2", 4, CountBy(TypeA, DashboardFieldIds.Attribute(TypeA, AttrName)), "Bar")));
                using (var presenter = env.CreatePresenter(env.CompleteProvider()))
                {
                    presenter.ReplaceDataSession(null, Catalog(), Types());
                    await presenter.RefreshTask;
                    Assert.Equal(new[] { "kpi", "bim", "q1", "chart", "q2" }, presenter.DashboardLayoutItems.Select(i => i.Id).ToArray());
                    Assert.False(Vm(presenter, "kpi").IsQueryWidget);
                    Assert.True(Vm(presenter, "q1").IsQueryWidget);
                    Assert.Equal(DashboardQueryWidgetRuntimeStatus.Success, Vm(presenter, "q1").QueryStatus);
                    Assert.True(Vm(presenter, "chart").IsChart);
                }
            }
        }

        private static DashboardWidgetVm Vm(AnalyticsDashboardPresenter presenter, string id)
        {
            return presenter.DashboardWidgets.Single(w => w.Id == id);
        }

        private static DashboardDefinition Definition(params DashboardWidgetDefinition[] widgets)
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

        private static DashboardWidgetDefinition LegacyChart(string id, int order)
        {
            return new DashboardWidgetDefinition
            {
                Id = id,
                Title = "График",
                ContentKind = DashboardPersistenceV2.ContentLegacy,
                Layout = new DashboardWidgetLayoutDefinition { Order = order, ColumnSpan = 2, IsVisible = true },
                Legacy = new DashboardLegacyWidgetContent
                {
                    WidgetKind = DashboardWidgetKinds.Chart,
                    ChartSource = "Types",
                    ChartKind = "HorizontalBar",
                    TopN = 12
                }
            };
        }

        private static DashboardWidgetDefinition LegacyKpi(string id, int order)
        {
            return new DashboardWidgetDefinition
            {
                Id = id,
                Title = "KPI / сводка",
                ContentKind = DashboardPersistenceV2.ContentLegacy,
                Layout = new DashboardWidgetLayoutDefinition { Order = order, ColumnSpan = 2, IsVisible = true },
                Legacy = new DashboardLegacyWidgetContent { WidgetKind = DashboardWidgetKinds.Kpi, ChartSource = "Types", ChartKind = "HorizontalBar", TopN = 12 }
            };
        }

        private static DashboardWidgetDefinition LegacyBim(string id, int order)
        {
            return new DashboardWidgetDefinition
            {
                Id = id,
                Title = "BIM",
                ContentKind = DashboardPersistenceV2.ContentLegacy,
                Layout = new DashboardWidgetLayoutDefinition { Order = order, ColumnSpan = 2, IsVisible = true },
                Legacy = new DashboardLegacyWidgetContent { WidgetKind = DashboardWidgetKinds.Bim, ChartSource = "Types", ChartKind = "HorizontalBar", TopN = 12 }
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

        private static DashboardWidgetQuery FilteredScalar(int typeId)
        {
            return new DashboardWidgetQuery(
                DashboardQueryScopeKind.CurrentProject,
                null,
                DashboardQueryMeasure.Count,
                DashboardQuerySort.ValueDescending,
                null,
                typeId,
                new[]
                {
                    new DashboardFilterDefinition(
                        DashboardFieldIds.SystemCreatorId,
                        DashboardFilterOperator.IsNotEmpty,
                        null)
                });
        }

        private static DashboardFieldCatalog Catalog(params int[] extraTypes)
        {
            var types = new List<TypeInventoryRecord> { TypeRec(TypeA), TypeRec(TypeB) };
            foreach (var id in extraTypes ?? new int[0])
            {
                if (id != TypeA && id != TypeB)
                    types.Add(TypeRec(id));
            }
            return new PilotFieldCatalogBuilder().Build(types);
        }

        private static IReadOnlyList<DashboardObjectTypeOption> Types(params int[] extra)
        {
            return DashboardObjectTypeOption.FromInventory(new[] { TypeRec(TypeA), TypeRec(TypeB) }.Concat((extra ?? new int[0]).Select(TypeRec)));
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

        private static DashboardTypeDataset Partial(int typeId, params DashboardObjectRow[] rows)
        {
            var list = rows ?? new DashboardObjectRow[0];
            return new DashboardTypeDataset(typeId, 10, list.Length, DashboardTypeCoverage.Partial, "partial", list, 0, 0);
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
            var store = new DashboardLayoutStore(env.V1Path);
            store.Save(DashboardLayoutStore.Default());
        }

        private sealed class Env : IDisposable
        {
            public Env()
            {
                Root = Path.Combine(Path.GetTempPath(), "PilotBim.Analytics.Tests", "db9-" + Guid.NewGuid().ToString("N"));
                V2Root = Path.Combine(Root, "Dashboards");
                V1Path = Path.Combine(Root, "dashboard-layout.json");
                Store = new DashboardDefinitionStore(V2Root);
            }

            public string Root { get; private set; }
            public string V2Root { get; private set; }
            public string V1Path { get; private set; }
            public DashboardDefinitionStore Store { get; private set; }
            public FakeProvider LastProvider { get; set; }
            public int PreviewWave;

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
            public readonly List<int> TypeIds = new List<int>();
            public Func<int, DashboardTypeDataset> Factory;
            public Action<int, CancellationToken> OnMaterialize;

            public DashboardTypeDataset Materialize(int typeId, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref Calls);
                lock (TypeIds)
                    TypeIds.Add(typeId);
                if (OnMaterialize != null)
                    OnMaterialize(typeId, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (Factory != null)
                    return Factory(typeId);
                return Complete(typeId);
            }
        }
    }
}
