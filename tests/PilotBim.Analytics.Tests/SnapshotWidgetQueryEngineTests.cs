using System;
using System.Collections.Generic;
using System.Linq;
using PilotBim.Analytics.Models;
using PilotBim.Analytics.Services;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    public sealed class SnapshotWidgetQueryEngineTests
    {
        private readonly SnapshotWidgetQueryEngine _engine = new SnapshotWidgetQueryEngine();
        private readonly ChartDataService _charts = new ChartDataService();

        [Fact]
        public void Filters_NonEmpty_ReturnsUnsupported()
        {
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
            var result = _engine.Execute(Types(("A", 1, 1)), query);
            Assert.Equal(WidgetQueryStatus.UnsupportedQuery, result.Status);
            Assert.Empty(result.Dataset.Rows);
        }

        [Fact]
        public void EntityTypeId_NonNull_ReturnsUnsupported()
        {
            var query = new DashboardWidgetQuery(
                DashboardQueryScopeKind.CurrentProject,
                DashboardFieldIds.SystemTypeId,
                DashboardQueryMeasure.Count,
                DashboardQuerySort.ValueDescending,
                null,
                12);
            var result = _engine.Execute(Types(("A", 1, 1)), query);
            Assert.Equal(WidgetQueryStatus.UnsupportedQuery, result.Status);
            Assert.Empty(result.Dataset.Rows);
        }

        [Fact]
        public void CountByType_Succeeds()
        {
            var snapshot = Types(("Door", 10, 1), ("Window", 3, 2));
            var result = _engine.Execute(snapshot, CountBy(DashboardFieldIds.SystemTypeId));

            Assert.Equal(WidgetQueryStatus.Success, result.Status);
            Assert.Equal(2, result.Dataset.Rows.Count);
            Assert.Equal("1", result.Dataset.Rows[0].Key);
            Assert.Equal("Door", result.Dataset.Rows[0].Label);
            Assert.Equal(10, result.Dataset.Rows[0].Value);
        }

        [Fact]
        public void CountWithoutDimension_SumsTypeCounts()
        {
            var snapshot = Types(("A", 4, 1), ("B", 6, 2));
            var result = _engine.Execute(snapshot, ScalarCount());

            Assert.Equal(WidgetQueryStatus.Success, result.Status);
            Assert.Single(result.Dataset.Rows);
            Assert.Equal(10, result.Dataset.Rows[0].Value);
            Assert.Equal(string.Empty, result.Dataset.Rows[0].Key);
        }

        [Fact]
        public void UnsupportedDimension_ReturnsUnsupported()
        {
            var result = _engine.Execute(Types(("A", 1, 1)), CountBy(DashboardFieldIds.SystemCreated));
            Assert.Equal(WidgetQueryStatus.UnsupportedQuery, result.Status);
            Assert.Empty(result.Dataset.Rows);
        }

        [Fact]
        public void AttributeDimension_ReturnsUnsupported()
        {
            var result = _engine.Execute(
                Types(("A", 1, 1)),
                CountBy(DashboardFieldIds.Attribute(12, "RemarkType")));
            Assert.Equal(WidgetQueryStatus.UnsupportedQuery, result.Status);
        }

        [Fact]
        public void UnknownMeasure_ReturnsUnsupported()
        {
            var query = new DashboardWidgetQuery(
                DashboardQueryScopeKind.CurrentProject,
                DashboardFieldIds.SystemTypeId,
                (DashboardQueryMeasure)99,
                DashboardQuerySort.ValueDescending,
                null);
            var result = _engine.Execute(Types(("A", 1, 1)), query);
            Assert.Equal(WidgetQueryStatus.UnsupportedQuery, result.Status);
        }

        [Fact]
        public void LimitZero_ReturnsInvalid()
        {
            var result = _engine.Execute(Types(("A", 1, 1)), CountBy(DashboardFieldIds.SystemTypeId, limit: 0));
            Assert.Equal(WidgetQueryStatus.InvalidQuery, result.Status);
        }

        [Fact]
        public void NullQuery_ReturnsInvalid()
        {
            Assert.Equal(WidgetQueryStatus.InvalidQuery, _engine.Execute(Types(("A", 1, 1)), null).Status);
        }

        [Fact]
        public void EmptySnapshot_ReturnsEmpty()
        {
            var result = _engine.Execute(new ProjectAnalyticsSnapshot(), CountBy(DashboardFieldIds.SystemTypeId));
            Assert.Equal(WidgetQueryStatus.Empty, result.Status);
            Assert.Empty(result.Dataset.Rows);
        }

        [Fact]
        public void NullSnapshot_ReturnsEmpty_ForSupportedDimension()
        {
            var result = _engine.Execute(null, CountBy(DashboardFieldIds.SystemTypeId));
            Assert.Equal(WidgetQueryStatus.Empty, result.Status);
        }

        [Fact]
        public void Sort_ValueDescending()
        {
            var rows = ExecuteSort(("A", 1, 1), ("B", 9, 2), ("C", 3, 3), DashboardQuerySort.ValueDescending);
            Assert.Equal(new[] { "B", "C", "A" }, rows.Select(r => r.Label).ToArray());
        }

        [Fact]
        public void Sort_ValueAscending()
        {
            var rows = ExecuteSort(("A", 1, 1), ("B", 9, 2), ("C", 3, 3), DashboardQuerySort.ValueAscending);
            Assert.Equal(new[] { "A", "C", "B" }, rows.Select(r => r.Label).ToArray());
        }

        [Fact]
        public void Sort_LabelAscending()
        {
            var rows = ExecuteSort(("C", 1, 1), ("A", 9, 2), ("B", 3, 3), DashboardQuerySort.LabelAscending);
            Assert.Equal(new[] { "A", "B", "C" }, rows.Select(r => r.Label).ToArray());
        }

        [Fact]
        public void Sort_TieBreaksByKeyOrdinal()
        {
            var snapshot = new ProjectAnalyticsSnapshot
            {
                ObjectsByType = new List<TypeCountRow>
                {
                    new TypeCountRow { TypeId = 11, TypeName = "Same", Count = 5 },
                    new TypeCountRow { TypeId = 10, TypeName = "Same", Count = 5 }
                }
            };
            var result = _engine.Execute(snapshot, CountBy(DashboardFieldIds.SystemTypeId));
            Assert.Equal("10", result.Dataset.Rows[0].Key);
            Assert.Equal("11", result.Dataset.Rows[1].Key);
        }

        [Fact]
        public void Limit_TakesFirstAfterSort()
        {
            var snapshot = Types(("A", 1, 1), ("B", 9, 2), ("C", 3, 3));
            var result = _engine.Execute(snapshot, CountBy(DashboardFieldIds.SystemTypeId, limit: 2));
            Assert.Equal(2, result.Dataset.Rows.Count);
            Assert.Equal("B", result.Dataset.Rows[0].Label);
            Assert.Equal("C", result.Dataset.Rows[1].Label);
        }

        [Fact]
        public void Limit_GreaterThanCount_ReturnsAll()
        {
            var snapshot = Types(("A", 1, 1), ("B", 2, 2));
            var result = _engine.Execute(snapshot, CountBy(DashboardFieldIds.SystemTypeId, limit: 50));
            Assert.Equal(2, result.Dataset.Rows.Count);
        }

        [Fact]
        public void NullAndEmptyLabels_BecomeEmptyString_NotLocalized()
        {
            var snapshot = new ProjectAnalyticsSnapshot
            {
                ObjectsByType = new List<TypeCountRow>
                {
                    new TypeCountRow { TypeId = 1, TypeName = null, Count = 4 },
                    new TypeCountRow { TypeId = 2, TypeName = "  ", Count = 1 }
                }
            };
            var result = _engine.Execute(snapshot, CountBy(DashboardFieldIds.SystemTypeId, DashboardQuerySort.ValueDescending));
            Assert.Equal(WidgetQueryStatus.Success, result.Status);
            Assert.Equal(string.Empty, result.Dataset.Rows[0].Label);
            Assert.Equal("1", result.Dataset.Rows[0].Key);
            Assert.DoesNotContain(result.Dataset.Rows, r => r.Key.IndexOf("не задано", StringComparison.Ordinal) >= 0);
            Assert.DoesNotContain(result.Dataset.Rows, r => r.Label == "?");
        }

        [Fact]
        public void RepeatedExecution_EquivalentOutput()
        {
            var snapshot = Types(("A", 4, 1), ("B", 2, 2));
            var query = CountBy(DashboardFieldIds.SystemTypeId, limit: 2);
            var a = _engine.Execute(snapshot, query);
            var b = _engine.Execute(snapshot, query);
            Assert.Equal(a.Status, b.Status);
            Assert.Equal(
                a.Dataset.Rows.Select(r => r.Key + "|" + r.Label + "|" + r.Value).ToList(),
                b.Dataset.Rows.Select(r => r.Key + "|" + r.Label + "|" + r.Value).ToList());
        }

        [Fact]
        public void Execute_DoesNotMutateSnapshot()
        {
            var snapshot = Types(("A", 4, 1), ("B", 2, 2));
            var original = snapshot.ObjectsByType.ToList();
            _engine.Execute(snapshot, CountBy(DashboardFieldIds.SystemTypeId, limit: 1));
            Assert.Equal(2, snapshot.ObjectsByType.Count);
            Assert.Same(original[0], snapshot.ObjectsByType[0]);
            Assert.Same(original[1], snapshot.ObjectsByType[1]);
            Assert.Equal(4, snapshot.ObjectsByType[0].Count);
        }

        [Fact]
        public void Dataset_HasNoChartGeometry()
        {
            var result = _engine.Execute(Types(("A", 8, 1)), CountBy(DashboardFieldIds.SystemTypeId));
            var row = result.Dataset.Rows[0];
            Assert.Equal("A", row.Label);
            Assert.Equal(8, row.Value);
            Assert.Null(row.GetType().GetProperty("BarWidth"));
            Assert.Null(row.GetType().GetProperty("ColorHex"));
        }

        [Fact]
        public void Parity_Types_MatchesChartDataService()
        {
            AssertParity(
                Types(("C", 5, 1), ("A", 30, 2), ("B", 10, 3)),
                DashboardFieldIds.SystemTypeId,
                AnalyticsChartSource.Types,
                2);
        }

        [Fact]
        public void Parity_Creators_MatchesChartDataService()
        {
            var snapshot = new ProjectAnalyticsSnapshot
            {
                ObjectsByCreator = new List<CreatorCountRow>
                {
                    new CreatorCountRow { CreatorId = 1, DisplayName = "Ann", SampledCount = 4 },
                    new CreatorCountRow { CreatorId = 2, DisplayName = "Bob", SampledCount = 9 },
                    new CreatorCountRow { CreatorId = 3, DisplayName = "Cara", SampledCount = 1 }
                }
            };
            AssertParity(snapshot, DashboardFieldIds.SystemCreatorId, AnalyticsChartSource.Creators, 2);
        }

        [Fact]
        public void Parity_UserStates_MatchesChartDataService()
        {
            var a = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            var b = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
            var snapshot = new ProjectAnalyticsSnapshot
            {
                ObjectsByUserState = new List<StateCountRow>
                {
                    new StateCountRow { StateId = a, StateTitle = "Open", Count = 2 },
                    new StateCountRow { StateId = b, StateTitle = "Closed", Count = 7 }
                }
            };
            AssertParity(snapshot, DashboardFieldIds.SystemUserState, AnalyticsChartSource.UserStates, 0);
        }

        [Fact]
        public void Parity_Responsible_MatchesChartDataService()
        {
            var snapshot = new ProjectAnalyticsSnapshot
            {
                ObjectsByResponsible = new List<ResponsibleCountRow>
                {
                    new ResponsibleCountRow { OrgUnitId = 10, DisplayName = "Ivan", SampledCount = 3 },
                    new ResponsibleCountRow { OrgUnitId = 20, DisplayName = "Olga", SampledCount = 8 }
                }
            };
            AssertParity(snapshot, DashboardFieldIds.SystemResponsible, AnalyticsChartSource.Responsible, 12);
        }

        [Fact]
        public void CreatedMonth_IsSnapshotExecutable_ButNotChartTimelineOrder()
        {
            var snapshot = new ProjectAnalyticsSnapshot
            {
                ObjectsByCreatedMonth = new List<PeriodCountRow>
                {
                    new PeriodCountRow { Period = "2024-01", Count = 1 },
                    new PeriodCountRow { Period = "2024-03", Count = 99 },
                    new PeriodCountRow { Period = "2024-02", Count = 5 }
                }
            };
            var engine = _engine.Execute(snapshot, CountBy(DashboardFieldIds.SystemCreatedMonth));
            var chart = _charts.BuildSeries(snapshot, null, AnalyticsChartSource.CreatedMonth, AnalyticsChartKind.Line, 0);

            Assert.Equal(WidgetQueryStatus.Success, engine.Status);
            Assert.Equal("2024-03", engine.Dataset.Rows[0].Label);
            Assert.Equal("2024-01", chart[0].Label);
        }

        [Fact]
        public void Registry_KnownDimensions_AreSupported()
        {
            Assert.True(SnapshotDimensionRegistry.IsSupported(DashboardFieldIds.SystemTypeId));
            Assert.True(SnapshotDimensionRegistry.IsSupported(DashboardFieldIds.SystemCreatorId));
            Assert.True(SnapshotDimensionRegistry.IsSupported(DashboardFieldIds.SystemCreatedMonth));
            Assert.True(SnapshotDimensionRegistry.IsSupported(DashboardFieldIds.SystemUserState));
            Assert.True(SnapshotDimensionRegistry.IsSupported(DashboardFieldIds.SystemResponsible));
            Assert.False(SnapshotDimensionRegistry.IsSupported(DashboardFieldIds.SystemObjectId));
            Assert.False(SnapshotDimensionRegistry.IsSupported("attribute:1:x"));
        }

        private void AssertParity(
            ProjectAnalyticsSnapshot snapshot,
            string fieldId,
            AnalyticsChartSource source,
            int take)
        {
            int? limit = take > 0 ? take : (int?)null;
            var engine = _engine.Execute(snapshot, CountBy(fieldId, DashboardQuerySort.ValueDescending, limit));
            var chart = _charts.BuildSeries(snapshot, null, source, AnalyticsChartKind.HorizontalBar, take);

            Assert.Equal(WidgetQueryStatus.Success, engine.Status);
            Assert.Equal(chart.Count, engine.Dataset.Rows.Count);
            for (var i = 0; i < chart.Count; i++)
            {
                Assert.Equal(chart[i].Label, engine.Dataset.Rows[i].Label);
                Assert.Equal((long)chart[i].Value, engine.Dataset.Rows[i].Value);
            }
        }

        private IReadOnlyList<WidgetDataRow> ExecuteSort(
            (string name, long count, int id) a,
            (string name, long count, int id) b,
            (string name, long count, int id) c,
            DashboardQuerySort sort)
        {
            var snapshot = Types(a, b, c);
            var result = _engine.Execute(snapshot, CountBy(DashboardFieldIds.SystemTypeId, sort, null));
            Assert.Equal(WidgetQueryStatus.Success, result.Status);
            return result.Dataset.Rows;
        }

        private static DashboardWidgetQuery CountBy(
            string fieldId,
            DashboardQuerySort sort = DashboardQuerySort.ValueDescending,
            int? limit = null)
        {
            return new DashboardWidgetQuery(
                DashboardQueryScopeKind.CurrentProject,
                fieldId,
                DashboardQueryMeasure.Count,
                sort,
                limit);
        }

        private static DashboardWidgetQuery ScalarCount()
        {
            return new DashboardWidgetQuery(
                DashboardQueryScopeKind.CurrentProject,
                null,
                DashboardQueryMeasure.Count,
                DashboardQuerySort.ValueDescending,
                null);
        }

        private static ProjectAnalyticsSnapshot Types(params (string name, long count, int id)[] items)
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
    }
}
