using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PilotBim.Analytics.Models;
using PilotBim.Analytics.Services;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    public sealed class DashboardDefinitionV4Tests
    {
        private static readonly Guid ProjectA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        [Fact]
        public void V4_FilterRoundTrip_PreservesIdentity()
        {
            using (var session = new StoreSession())
            {
                var def = session.Store.CreateDefault(ProjectA);
                def.Widgets.Add(Query("q1", 10));
                def.DashboardFilters.Add(Filter("f1", 10, "q1", "Owner"));
                Assert.Equal(DashboardDefinitionSaveStatus.Success, session.Store.Save(def).Status);
                var loaded = session.Store.Load(ProjectA);
                Assert.Equal(DashboardDefinitionLoadStatus.Success, loaded.Status);
                Assert.Equal(4, loaded.Definition.SchemaVersion);
                Assert.Single(loaded.Definition.DashboardFilters);
                var filter = loaded.Definition.DashboardFilters[0];
                Assert.Equal("f1", filter.Id);
                Assert.Equal("Owner", filter.Title);
                Assert.Equal(10, filter.EntityTypeId);
                Assert.Equal(DashboardFieldIds.Attribute(10, "Owner"), filter.FieldId);
                Assert.Equal("Equals", filter.Operator);
                Assert.Equal("user:7", filter.Value);
                Assert.Equal(new[] { "q1" }, filter.TargetWidgetIds);
                Assert.False(filter.Disabled);
            }
        }

        [Fact]
        public void V3_ToCurrent_EmptyFilters_NoWrite()
        {
            using (var session = new StoreSession())
            {
                var v3 = session.Store.CreateDefault(ProjectA);
                v3.SchemaVersion = 3;
                v3.Widgets.Add(Query("q1", 10));
                Assert.Equal(DashboardDefinitionSaveStatus.Success, session.Store.Save(v3).Status);
                var path = session.Store.GetPath(ProjectA);
                var before = File.ReadAllBytes(path);
                var loaded = session.Store.Load(ProjectA);
                Assert.Equal(3, loaded.Definition.SchemaVersion);
                var current = DashboardDefinitionV4Migrator.ToCurrent(loaded.Definition);
                Assert.Equal(4, current.SchemaVersion);
                Assert.Empty(current.DashboardFilters);
                Assert.Equal(before, File.ReadAllBytes(path));
            }
        }

        [Fact]
        public void V2_ToCurrent_EmptyFilters()
        {
            var v2 = new DashboardDefinition
            {
                SchemaVersion = 2,
                Id = "default",
                Title = "Dashboard",
                ProjectKey = ProjectA.ToString("D"),
                Widgets = new List<DashboardWidgetDefinition> { Query("q1", 10) }
            };
            var current = DashboardDefinitionV4Migrator.ToCurrent(v2);
            Assert.Equal(4, current.SchemaVersion);
            Assert.Empty(current.DashboardFilters);
            Assert.Equal("q1", current.Widgets[0].Id);
        }

        [Fact]
        public void V1_ToCurrent_EmptyFilters()
        {
            var current = DashboardDefinitionV4Migrator.ToCurrent(
                DashboardDefinitionV3Migrator.FromLegacy(DashboardLayoutStore.Default(), ProjectA));
            Assert.Equal(4, current.SchemaVersion);
            Assert.Empty(current.DashboardFilters);
        }

        [Fact]
        public void UnknownV5_Rejected_NotOverwritten()
        {
            using (var session = new StoreSession())
            {
                var path = session.Store.GetPath(ProjectA);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, "{\"SchemaVersion\":5,\"Id\":\"default\"}", Encoding.UTF8);
                var original = File.ReadAllBytes(path);
                var loaded = session.Store.Load(ProjectA);
                Assert.Equal(DashboardDefinitionLoadStatus.UnsupportedVersion, loaded.Status);
                Assert.Equal(original, File.ReadAllBytes(path));
            }
        }

        [Fact]
        public void V4_ProjectMismatch_Preserved()
        {
            using (var session = new StoreSession())
            {
                var other = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
                var def = session.Store.CreateDefault(other);
                Assert.Equal(DashboardDefinitionSaveStatus.Success, session.Store.Save(def).Status);
                var pathA = session.Store.GetPath(ProjectA);
                Directory.CreateDirectory(Path.GetDirectoryName(pathA));
                File.Copy(session.Store.GetPath(other), pathA, true);
                var before = File.ReadAllBytes(pathA);
                var loaded = session.Store.Load(ProjectA);
                Assert.Equal(DashboardDefinitionLoadStatus.ProjectMismatch, loaded.Status);
                Assert.Equal(before, File.ReadAllBytes(pathA));
            }
        }

        [Fact]
        public void V4_Load_WritesNothing()
        {
            using (var session = new StoreSession())
            {
                var def = session.Store.CreateDefault(ProjectA);
                def.Widgets.Add(Query("q1", 10));
                Assert.Equal(DashboardDefinitionSaveStatus.Success, session.Store.Save(def).Status);
                var path = session.Store.GetPath(ProjectA);
                var before = File.ReadAllBytes(path);
                var loaded = session.Store.Load(ProjectA);
                Assert.Equal(DashboardDefinitionLoadStatus.Success, loaded.Status);
                Assert.Equal(4, loaded.Definition.SchemaVersion);
                Assert.Equal(before, File.ReadAllBytes(path));
            }
        }

        [Fact]
        public void CorruptV4_Preserved()
        {
            using (var session = new StoreSession())
            {
                var path = session.Store.GetPath(ProjectA);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, "{\"SchemaVersion\":4,not-json", Encoding.UTF8);
                var original = File.ReadAllBytes(path);
                var loaded = session.Store.Load(ProjectA);
                Assert.Equal(DashboardDefinitionLoadStatus.Corrupt, loaded.Status);
                Assert.Equal(original, File.ReadAllBytes(path));
            }
        }

        private static DashboardWidgetDefinition Query(string id, int typeId)
        {
            return new DashboardWidgetDefinition
            {
                Id = id,
                Title = id,
                ContentKind = DashboardPersistenceV2.ContentQuery,
                Layout = new DashboardWidgetLayoutDefinition
                {
                    X = 0,
                    Y = 0,
                    Width = 6,
                    Height = 2,
                    IsVisible = true,
                    ColumnSpan = 1,
                    Order = 0
                },
                Query = DashboardQueryPersistence.ToDocument(new DashboardWidgetQuery(
                    DashboardQueryScopeKind.CurrentProject,
                    null,
                    DashboardQueryMeasure.Count,
                    DashboardQuerySort.ValueDescending,
                    null,
                    typeId)),
                Visualization = new DashboardVisualizationDefinition { Type = "Kpi" }
            };
        }

        private static DashboardLevelFilterDefinition Filter(string id, int typeId, string widgetId, string title)
        {
            var field = DashboardFieldIds.Attribute(typeId, title);
            DashboardFilterDocument doc;
            string error;
            Assert.True(DashboardQueryPersistence.TryToFilterDocument(
                new DashboardFilterDefinition(field, DashboardFilterOperator.Equals, DashboardFilterValue.Identity(DashboardFieldType.User, "user:7")),
                out doc,
                out error), error);
            return new DashboardLevelFilterDefinition
            {
                Id = id,
                Title = title,
                EntityTypeId = typeId,
                FieldId = field,
                Operator = doc.Operator,
                ValueKind = doc.ValueKind,
                Value = doc.Value,
                TargetWidgetIds = new List<string> { widgetId }
            };
        }

        private sealed class StoreSession : IDisposable
        {
            public StoreSession()
            {
                Root = Path.Combine(Path.GetTempPath(), "PilotBim.Analytics.Tests", "db12-" + Guid.NewGuid().ToString("N"));
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
