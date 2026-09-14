using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using Ascon.Pilot.SDK;
using PilotBim.Analytics.Models;
using PilotBim.Analytics.Services;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    public sealed class DashboardPersistenceV2Tests
    {
        private static readonly Guid ProjectA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid ProjectB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        [Fact]
        public void DefaultDefinition_IsValid()
        {
            using (var session = new StoreSession())
            {
                var def = session.Store.CreateDefault(ProjectA);
                string error;
                Assert.True(DashboardDefinitionValidator.TryValidate(def, out error), error);
                Assert.Equal(DashboardPersistenceV2.CurrentSchemaVersion, def.SchemaVersion);
                Assert.Equal("default", def.Id);
                Assert.Equal("Dashboard", def.Title);
                Assert.Equal(ProjectA.ToString("D"), def.ProjectKey);
                Assert.Empty(def.Widgets);
            }
        }

        [Fact]
        public void SchemaVersion2_RoundTrip_PreservesDashboardAndWidgetIds()
        {
            using (var session = new StoreSession())
            {
                var def = MixedDashboard();
                Assert.Equal(DashboardDefinitionSaveStatus.Success, session.Store.Save(def).Status);
                var loaded = session.Store.Load(ProjectA);
                Assert.Equal(DashboardDefinitionLoadStatus.Success, loaded.Status);
                Assert.Equal(2, loaded.Definition.SchemaVersion);
                Assert.Equal("default", loaded.Definition.Id);
                Assert.Equal("Plant dashboard", loaded.Definition.Title);
                Assert.Equal(ProjectA.ToString("D"), loaded.Definition.ProjectKey);
                Assert.Equal(2, loaded.Definition.Widgets.Count);
                Assert.Equal("kpi", loaded.Definition.Widgets[0].Id);
                Assert.Equal("remarks-by-type", loaded.Definition.Widgets[1].Id);
                Assert.Equal("KPI / сводка", loaded.Definition.Widgets[0].Title);
                Assert.Equal("Remarks", loaded.Definition.Widgets[1].Title);
                Assert.Equal(2, loaded.Definition.Widgets[0].Layout.ColumnSpan);
                Assert.Equal(1, loaded.Definition.Widgets[1].Layout.ColumnSpan);
                Assert.Equal(DashboardPersistenceV2.ContentLegacy, loaded.Definition.Widgets[0].ContentKind);
                Assert.Equal(DashboardWidgetKinds.Kpi, loaded.Definition.Widgets[0].Legacy.WidgetKind);
                Assert.Equal(DashboardPersistenceV2.ContentQuery, loaded.Definition.Widgets[1].ContentKind);
            }
        }

        [Fact]
        public void DuplicateWidgetId_Rejected()
        {
            var def = sessionlessDefault();
            def.Widgets.Add(LegacyWidget("kpi", 0));
            def.Widgets.Add(LegacyWidget("kpi", 1));
            string error;
            Assert.False(DashboardDefinitionValidator.TryValidate(def, out error));
            Assert.Contains("duplicate", error);
        }

        [Fact]
        public void InvalidContentMode_Rejected()
        {
            var def = sessionlessDefault();
            var widget = QueryWidget("q1", 0, ScalarQuery());
            widget.Legacy = new DashboardLegacyWidgetContent { WidgetKind = DashboardWidgetKinds.Chart };
            def.Widgets.Add(widget);
            string error;
            Assert.False(DashboardDefinitionValidator.TryValidate(def, out error));
        }

        [Fact]
        public void QueryWidget_MissingVisualization_Rejected()
        {
            var def = sessionlessDefault();
            var widget = QueryWidget("q1", 0, ScalarQuery());
            widget.Visualization = null;
            def.Widgets.Add(widget);
            string error;
            Assert.False(DashboardDefinitionValidator.TryValidate(def, out error));
        }

        [Fact]
        public void UnknownFutureSchema_NotApplied_AndNotOverwritten()
        {
            using (var session = new StoreSession())
            {
                var path = session.Store.GetPath(ProjectA);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, "{\"SchemaVersion\":99,\"Id\":\"default\"}", Encoding.UTF8);
                var original = File.ReadAllBytes(path);

                var loaded = session.Store.Load(ProjectA);
                Assert.Equal(DashboardDefinitionLoadStatus.UnsupportedVersion, loaded.Status);
                Assert.Null(loaded.Definition);
                Assert.Equal(original, File.ReadAllBytes(path));
            }
        }

        [Fact]
        public void SchemaVersion3_RoundTrip_PreservesGridRect()
        {
            using (var session = new StoreSession())
            {
                var def = session.Store.CreateDefault(ProjectA);
                def.Widgets.Add(new DashboardWidgetDefinition
                {
                    Id = "q1",
                    Title = "Query",
                    ContentKind = DashboardPersistenceV2.ContentQuery,
                    Layout = new DashboardWidgetLayoutDefinition
                    {
                        X = 3,
                        Y = 4,
                        Width = 6,
                        Height = 3,
                        IsVisible = true,
                        ColumnSpan = 1,
                        Order = 4 * 12 + 3
                    },
                    Query = DashboardQueryPersistence.ToDocument(ScalarQuery()),
                    Visualization = new DashboardVisualizationDefinition { Type = "Kpi" }
                });
                Assert.Equal(DashboardDefinitionSaveStatus.Success, session.Store.Save(def).Status);
                var loaded = session.Store.Load(ProjectA);
                Assert.Equal(DashboardDefinitionLoadStatus.Success, loaded.Status);
                Assert.Equal(3, loaded.Definition.SchemaVersion);
                var widget = loaded.Definition.Widgets[0];
                Assert.Equal(3, widget.Layout.X);
                Assert.Equal(4, widget.Layout.Y);
                Assert.Equal(6, widget.Layout.Width);
                Assert.Equal(3, widget.Layout.Height);
            }
        }

        [Fact]
        public void SchemaVersion2_StillLoads()
        {
            using (var session = new StoreSession())
            {
                var def = MixedDashboard();
                Assert.Equal(2, def.SchemaVersion);
                Assert.Equal(DashboardDefinitionSaveStatus.Success, session.Store.Save(def).Status);
                var loaded = session.Store.Load(ProjectA);
                Assert.Equal(DashboardDefinitionLoadStatus.Success, loaded.Status);
                Assert.Equal(2, loaded.Definition.SchemaVersion);
            }
        }

        [Fact]
        public void SchemaVersion3_ProjectMismatch_Blocked()
        {
            using (var session = new StoreSession())
            {
                var def = session.Store.CreateDefault(ProjectB);
                Assert.Equal(DashboardDefinitionSaveStatus.Success, session.Store.Save(def).Status);
                var pathA = session.Store.GetPath(ProjectA);
                Directory.CreateDirectory(Path.GetDirectoryName(pathA));
                File.Copy(session.Store.GetPath(ProjectB), pathA, true);
                var before = File.ReadAllBytes(pathA);
                var loaded = session.Store.Load(ProjectA);
                Assert.Equal(DashboardDefinitionLoadStatus.ProjectMismatch, loaded.Status);
                Assert.Equal(before, File.ReadAllBytes(pathA));
            }
        }

        [Fact]
        public void CorruptV3_Preserved()
        {
            using (var session = new StoreSession())
            {
                var path = session.Store.GetPath(ProjectA);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, "{\"SchemaVersion\":3,not-json", Encoding.UTF8);
                var original = File.ReadAllBytes(path);
                var loaded = session.Store.Load(ProjectA);
                Assert.Equal(DashboardDefinitionLoadStatus.Corrupt, loaded.Status);
                Assert.Equal(original, File.ReadAllBytes(path));
            }
        }

        [Fact]
        public void VisibleOverlapV3_Rejected()
        {
            var def = sessionlessDefault();
            def.SchemaVersion = 3;
            def.Widgets.Add(QueryWidget("a", 0, ScalarQuery()));
            def.Widgets[0].Layout.X = 0;
            def.Widgets[0].Layout.Y = 0;
            def.Widgets[0].Layout.Width = 6;
            def.Widgets[0].Layout.Height = 2;
            def.Widgets.Add(QueryWidget("b", 1, ScalarQuery()));
            def.Widgets[1].Layout.X = 3;
            def.Widgets[1].Layout.Y = 0;
            def.Widgets[1].Layout.Width = 6;
            def.Widgets[1].Layout.Height = 2;
            string error;
            Assert.False(DashboardDefinitionValidator.TryValidate(def, out error));
            Assert.Contains("overlap", error);
        }

        [Fact]
        public void ScalarCountQuery_RoundTrip()
        {
            RoundTripQuery(new DashboardWidgetQuery(
                DashboardQueryScopeKind.CurrentProject,
                null,
                DashboardQueryMeasure.Count,
                DashboardQuerySort.ValueDescending,
                null));
        }

        [Fact]
        public void TypeIdAndAttributeDimension_RoundTrip()
        {
            RoundTripQuery(new DashboardWidgetQuery(
                DashboardQueryScopeKind.CurrentProject,
                DashboardFieldIds.Attribute(123, "RemarkType"),
                DashboardQueryMeasure.Count,
                DashboardQuerySort.LabelAscending,
                12,
                123));
        }

        [Fact]
        public void SortAndLimit_RoundTrip()
        {
            RoundTripQuery(new DashboardWidgetQuery(
                DashboardQueryScopeKind.CurrentProject,
                DashboardFieldIds.SystemTypeId,
                DashboardQueryMeasure.Count,
                DashboardQuerySort.ValueAscending,
                5));
        }

        [Fact]
        public void MultipleFilters_PreserveOrder()
        {
            var field = DashboardFieldIds.Attribute(12, "Status");
            var owner = DashboardFieldIds.Attribute(12, "Responsible");
            RoundTripQuery(new DashboardWidgetQuery(
                DashboardQueryScopeKind.CurrentProject,
                field,
                DashboardQueryMeasure.Count,
                DashboardQuerySort.ValueDescending,
                null,
                12,
                new[]
                {
                    new DashboardFilterDefinition(field, DashboardFilterOperator.Equals, DashboardFilterValue.Text("Open")),
                    new DashboardFilterDefinition(owner, DashboardFilterOperator.NotEquals, DashboardFilterValue.Identity(DashboardFieldType.User, "7")),
                    new DashboardFilterDefinition(field, DashboardFilterOperator.IsNotEmpty, null)
                }));
        }

        [Fact]
        public void IsEmpty_NoValue_RoundTrip()
        {
            RoundTripQuery(new DashboardWidgetQuery(
                DashboardQueryScopeKind.CurrentProject,
                null,
                DashboardQueryMeasure.Count,
                DashboardQuerySort.ValueDescending,
                null,
                12,
                new[]
                {
                    new DashboardFilterDefinition(DashboardFieldIds.Attribute(12, "Note"), DashboardFilterOperator.IsEmpty, null)
                }));
        }

        [Fact]
        public void FilterValues_RoundTrip_AllKinds()
        {
            AssertFilter(DashboardFilterValue.Text("Иванов"));
            AssertFilter(DashboardFilterValue.Text("line1\nline2 \"quoted\""));
            AssertFilter(DashboardFilterValue.Integer(42));
            AssertFilter(DashboardFilterValue.Integer(-7));
            AssertFilter(DashboardFilterValue.Number(1.5));
            AssertFilter(DashboardFilterValue.Number(2.0));
            AssertFilter(DashboardFilterValue.Boolean(true));
            AssertFilter(DashboardFilterValue.Boolean(false));
            var guid = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
            AssertFilter(DashboardFilterValue.Guid(guid));
            AssertFilter(DashboardFilterValue.Identity(DashboardFieldType.User, "7"));
            AssertFilter(DashboardFilterValue.Identity(DashboardFieldType.Reference, "77"));
            AssertFilter(DashboardFilterValue.Identity(DashboardFieldType.Enum, guid.ToString("D")));
        }

        [Fact]
        public void FilterNumber_CultureChange_DoesNotChangeIdentity()
        {
            var original = CultureInfo.CurrentCulture;
            var originalUi = CultureInfo.CurrentUICulture;
            try
            {
                var ru = CultureInfo.GetCultureInfo("ru-RU");
                CultureInfo.CurrentCulture = ru;
                CultureInfo.CurrentUICulture = ru;
                AssertFilter(DashboardFilterValue.Number(1.5));
                AssertFilter(DashboardFilterValue.Integer(1000));

                string kind, encoded, error;
                Assert.True(DashboardFilterValueCodec.TryEncode(DashboardFilterValue.Number(1.5), out kind, out encoded, out error));
                Assert.Equal("1.5", encoded);
                Assert.DoesNotContain(",", encoded);
            }
            finally
            {
                CultureInfo.CurrentCulture = original;
                CultureInfo.CurrentUICulture = originalUi;
            }
        }

        [Fact]
        public void LegacyFile_NoSchemaVersion_Detected()
        {
            var state = new DashboardLayoutState
            {
                Widgets = new List<DashboardWidgetState>
                {
                    new DashboardWidgetState { Id = "kpi", Title = "KPI / сводка", WidgetKind = DashboardWidgetKinds.Kpi }
                }
            };
            using (var ms = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(DashboardLayoutState)).WriteObject(ms, state);
                ms.Position = 0;
                Assert.Equal(DashboardPersistenceKind.LegacyV1, DashboardDefinitionStore.Detect(ms));
            }
        }

        [Fact]
        public void LegacyMigration_PreservesKnownSettings_AndDeterministicIds()
        {
            var state = RepresentativeLegacy();
            var first = DashboardDefinitionV2Migrator.FromLegacy(state, ProjectA);
            var second = DashboardDefinitionV2Migrator.FromLegacy(state, ProjectA);

            string error;
            Assert.True(DashboardDefinitionValidator.TryValidate(first, out error), error);
            Assert.Equal(first.Widgets.Select(w => w.Id), second.Widgets.Select(w => w.Id));
            Assert.Equal(new[] { "kpi", "bim", "responsible", "chart-types" }, first.Widgets.Select(w => w.Id).ToArray());
            Assert.Equal(new[] { 0, 1, 2, 3 }, first.Widgets.Select(w => w.Layout.Order).ToArray());
            Assert.Equal(2, first.Widgets[0].Layout.ColumnSpan);
            Assert.Equal(1, first.Widgets[3].Layout.ColumnSpan);
            Assert.False(first.Widgets[2].Layout.IsVisible);
            Assert.Equal(DashboardWidgetKinds.Kpi, first.Widgets[0].Legacy.WidgetKind);
            Assert.Equal(DashboardWidgetKinds.Chart, first.Widgets[3].Legacy.WidgetKind);
            Assert.Equal("Types", first.Widgets[3].Legacy.ChartSource);
            Assert.Equal("Pie", first.Widgets[3].Legacy.ChartKind);
            Assert.Equal(8, first.Widgets[3].Legacy.TopN);
            Assert.Equal("Objects by type", first.Widgets[3].Title);
            Assert.All(first.Widgets, w => Assert.Equal(DashboardPersistenceV2.ContentLegacy, w.ContentKind));
            Assert.All(first.Widgets, w => Assert.Null(w.Query));
        }

        [Fact]
        public void LegacyMigration_DoesNotRewriteSourceFile()
        {
            var dir = Path.Combine(Path.GetTempPath(), "PilotBim.Analytics.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var path = Path.Combine(dir, "dashboard-layout.json");
                var state = RepresentativeLegacy();
                using (var stream = File.Create(path))
                    new DataContractJsonSerializer(typeof(DashboardLayoutState)).WriteObject(stream, state);
                var before = File.ReadAllBytes(path);

                DashboardLayoutState loaded;
                using (var stream = File.OpenRead(path))
                    loaded = new DataContractJsonSerializer(typeof(DashboardLayoutState)).ReadObject(stream) as DashboardLayoutState;
                var migrated = DashboardDefinitionV2Migrator.FromLegacy(loaded, ProjectA);

                Assert.NotNull(migrated);
                Assert.Equal(before, File.ReadAllBytes(path));
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch { }
            }
        }

        [Fact]
        public void MissingFile_DoesNotCreate()
        {
            using (var session = new StoreSession())
            {
                var loaded = session.Store.Load(ProjectA);
                Assert.Equal(DashboardDefinitionLoadStatus.Missing, loaded.Status);
                Assert.False(File.Exists(session.Store.GetPath(ProjectA)));
            }
        }

        [Fact]
        public void CorruptJson_PreservesFile()
        {
            using (var session = new StoreSession())
            {
                var path = session.Store.GetPath(ProjectA);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, "{ not json", Encoding.UTF8);
                var original = File.ReadAllBytes(path);

                var loaded = session.Store.Load(ProjectA);
                Assert.Equal(DashboardDefinitionLoadStatus.Corrupt, loaded.Status);
                Assert.Null(loaded.Definition);
                Assert.Equal(original, File.ReadAllBytes(path));
            }
        }

        [Fact]
        public void AtomicSave_ReplacesAndLeavesNoTmp()
        {
            using (var session = new StoreSession())
            {
                var def = session.Store.CreateDefault(ProjectA);
                def.Title = "First";
                Assert.Equal(DashboardDefinitionSaveStatus.Success, session.Store.Save(def).Status);
                def.Title = "Second";
                Assert.Equal(DashboardDefinitionSaveStatus.Success, session.Store.Save(def).Status);

                var loaded = session.Store.Load(ProjectA);
                Assert.Equal("Second", loaded.Definition.Title);
                var dir = Path.GetDirectoryName(session.Store.GetPath(ProjectA));
                Assert.Empty(Directory.GetFiles(dir, "*.tmp"));
            }
        }

        [Fact]
        public void FailedSave_DoesNotDestroyPreviousFile()
        {
            using (var session = new StoreSession())
            {
                var def = session.Store.CreateDefault(ProjectA);
                def.Title = "Keep me";
                Assert.Equal(DashboardDefinitionSaveStatus.Success, session.Store.Save(def).Status);
                var path = session.Store.GetPath(ProjectA);

                DashboardDefinitionSaveResult save;
                using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    def.Title = "Should not persist";
                    save = session.Store.Save(def);
                }

                Assert.Equal(DashboardDefinitionSaveStatus.IoFailure, save.Status);
                var loaded = session.Store.Load(ProjectA);
                Assert.Equal(DashboardDefinitionLoadStatus.Success, loaded.Status);
                Assert.Equal("Keep me", loaded.Definition.Title);
            }
        }

        [Fact]
        public void ProjectMismatch_NotApplied()
        {
            using (var session = new StoreSession())
            {
                var def = MixedDashboard();
                Assert.Equal(DashboardDefinitionSaveStatus.Success, session.Store.Save(def).Status);
                var pathB = session.Store.GetPath(ProjectB);
                Directory.CreateDirectory(Path.GetDirectoryName(pathB));
                File.Copy(session.Store.GetPath(ProjectA), pathB, true);

                var loaded = session.Store.Load(ProjectB);
                Assert.Equal(DashboardDefinitionLoadStatus.ProjectMismatch, loaded.Status);
                Assert.Null(loaded.Definition);
            }
        }

        [Fact]
        public void ProjectPaths_AreDeterministicAndIsolated()
        {
            using (var session = new StoreSession())
            {
                var a = session.Store.GetPath(ProjectA);
                var b = session.Store.GetPath(ProjectB);
                Assert.NotEqual(a, b);
                Assert.Equal(session.Store.GetPath(ProjectA), a);
                Assert.Contains(ProjectA.ToString("D"), a);
                Assert.EndsWith("dashboard.json", a);
                Assert.DoesNotContain("Plant", a);
            }
        }

        [Fact]
        public void SameProjectKey_Loads_DifferentKey_Mismatch()
        {
            using (var session = new StoreSession())
            {
                Assert.Equal(DashboardDefinitionSaveStatus.Success, session.Store.Save(MixedDashboard()).Status);
                Assert.Equal(DashboardDefinitionLoadStatus.Success, session.Store.Load(ProjectA).Status);
                Assert.Equal(DashboardDefinitionLoadStatus.Missing, session.Store.Load(ProjectB).Status);
            }
        }

        [Fact]
        public void IObjectsRepository_ExposesGetDatabaseId()
        {
            var method = typeof(IObjectsRepository).GetMethod("GetDatabaseId", Type.EmptyTypes);
            Assert.NotNull(method);
            Assert.Equal(typeof(Guid), method.ReturnType);
        }

        [Fact]
        public void ProjectFolderName_IsCanonicalD()
        {
            Assert.Equal("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", DashboardProjectKey.ToFolderName(ProjectA));
            Guid parsed;
            Assert.True(DashboardProjectKey.TryParse("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA", out parsed));
            Assert.Equal(ProjectA, parsed);
            Assert.True(DashboardProjectKey.AreEqual(ProjectA.ToString("D").ToUpperInvariant(), ProjectA));
        }

        private static void RoundTripQuery(DashboardWidgetQuery query)
        {
            using (var session = new StoreSession())
            {
                var def = session.Store.CreateDefault(ProjectA);
                def.Widgets.Add(QueryWidget("q1", 0, query));
                Assert.Equal(DashboardDefinitionSaveStatus.Success, session.Store.Save(def).Status);
                var loaded = session.Store.Load(ProjectA);
                Assert.Equal(DashboardDefinitionLoadStatus.Success, loaded.Status);
                DashboardWidgetQuery round;
                string error;
                Assert.True(DashboardQueryPersistence.TryToQuery(loaded.Definition.Widgets[0].Query, out round, out error), error);
                Assert.True(DashboardQueryPersistence.AreSemanticallyEqual(query, round));
            }
        }

        private static void AssertFilter(DashboardFilterValue value)
        {
            var query = new DashboardWidgetQuery(
                DashboardQueryScopeKind.CurrentProject,
                null,
                DashboardQueryMeasure.Count,
                DashboardQuerySort.ValueDescending,
                null,
                12,
                new[]
                {
                    new DashboardFilterDefinition("attribute:12:Field", DashboardFilterOperator.Equals, value)
                });
            RoundTripQuery(query);
        }

        private static DashboardDefinition MixedDashboard()
        {
            var def = sessionlessDefault();
            def.Title = "Plant dashboard";
            def.Widgets.Add(LegacyWidget("kpi", 0));
            def.Widgets.Add(QueryWidget(
                "remarks-by-type",
                1,
                new DashboardWidgetQuery(
                    DashboardQueryScopeKind.CurrentProject,
                    DashboardFieldIds.Attribute(123, "RemarkType"),
                    DashboardQueryMeasure.Count,
                    DashboardQuerySort.ValueDescending,
                    10,
                    123,
                    new[]
                    {
                        new DashboardFilterDefinition(
                            DashboardFieldIds.Attribute(123, "Status"),
                            DashboardFilterOperator.Equals,
                            DashboardFilterValue.Text("Open"))
                    }),
                "Bar",
                1,
                "Remarks"));
            return def;
        }

        private static DashboardLayoutState RepresentativeLegacy()
        {
            return new DashboardLayoutState
            {
                Blocks = new List<DashboardBlockState>(),
                Widgets = new List<DashboardWidgetState>
                {
                    new DashboardWidgetState
                    {
                        Id = "kpi",
                        Title = "KPI / сводка",
                        WidgetKind = DashboardWidgetKinds.Kpi,
                        IsVisible = true,
                        Order = 0,
                        ColumnSpan = 2,
                        ChartSource = "Types",
                        ChartKind = "HorizontalBar",
                        TopN = 12
                    },
                    new DashboardWidgetState
                    {
                        Id = "bim",
                        Title = "BIM",
                        WidgetKind = DashboardWidgetKinds.Bim,
                        IsVisible = true,
                        Order = 1,
                        ColumnSpan = 2,
                        ChartSource = "Types",
                        ChartKind = "HorizontalBar",
                        TopN = 12
                    },
                    new DashboardWidgetState
                    {
                        Id = "responsible",
                        Title = "Ответственные",
                        WidgetKind = DashboardWidgetKinds.Responsible,
                        IsVisible = false,
                        Order = 2,
                        ColumnSpan = 2,
                        ChartSource = "Responsible",
                        ChartKind = "HorizontalBar",
                        TopN = 12
                    },
                    new DashboardWidgetState
                    {
                        Id = "chart-types",
                        Title = "Objects by type",
                        WidgetKind = DashboardWidgetKinds.Chart,
                        IsVisible = true,
                        Order = 3,
                        ColumnSpan = 1,
                        ChartSource = "Types",
                        ChartKind = "Pie",
                        TopN = 8
                    }
                }
            };
        }

        private static DashboardDefinition sessionlessDefault()
        {
            return new DashboardDefinition
            {
                SchemaVersion = 2,
                Id = "default",
                Title = "Dashboard",
                ProjectKey = ProjectA.ToString("D"),
                Widgets = new List<DashboardWidgetDefinition>()
            };
        }

        private static DashboardWidgetDefinition LegacyWidget(string id, int order)
        {
            return new DashboardWidgetDefinition
            {
                Id = id,
                Title = "KPI / сводка",
                ContentKind = DashboardPersistenceV2.ContentLegacy,
                Layout = new DashboardWidgetLayoutDefinition
                {
                    Order = order,
                    ColumnSpan = 2,
                    IsVisible = true,
                    X = 0,
                    Y = order * 2,
                    Width = 12,
                    Height = 2
                },
                Legacy = new DashboardLegacyWidgetContent
                {
                    WidgetKind = DashboardWidgetKinds.Kpi,
                    ChartSource = "Types",
                    ChartKind = "HorizontalBar",
                    TopN = 12
                }
            };
        }

        private static DashboardWidgetDefinition QueryWidget(
            string id,
            int order,
            DashboardWidgetQuery query,
            string visualization = "Auto",
            int columnSpan = 2,
            string title = "Query")
        {
            return new DashboardWidgetDefinition
            {
                Id = id,
                Title = title,
                ContentKind = DashboardPersistenceV2.ContentQuery,
                Layout = new DashboardWidgetLayoutDefinition
                {
                    Order = order,
                    ColumnSpan = columnSpan,
                    IsVisible = true,
                    X = 0,
                    Y = order * 3,
                    Width = columnSpan <= 1 ? 6 : 12,
                    Height = 3
                },
                Query = DashboardQueryPersistence.ToDocument(query),
                Visualization = new DashboardVisualizationDefinition { Type = visualization }
            };
        }

        private static DashboardWidgetQuery ScalarQuery()
        {
            return new DashboardWidgetQuery(
                DashboardQueryScopeKind.CurrentProject,
                null,
                DashboardQueryMeasure.Count,
                DashboardQuerySort.ValueDescending,
                null);
        }

        private sealed class StoreSession : IDisposable
        {
            public StoreSession()
            {
                Root = Path.Combine(Path.GetTempPath(), "PilotBim.Analytics.Tests", "db7-" + Guid.NewGuid().ToString("N"));
                Store = new DashboardDefinitionStore(Root);
            }

            public string Root { get; private set; }
            public DashboardDefinitionStore Store { get; private set; }

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
    }
}
