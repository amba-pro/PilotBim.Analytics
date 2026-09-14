using System.Linq;
using PilotBim.Analytics.Models;
using PilotBim.Analytics.Services;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    public sealed class DashboardGridLayoutEngineTests
    {
        [Fact]
        public void TryValidate_AcceptsValidRect()
        {
            string error;
            Assert.True(DashboardGridLayoutEngine.TryValidate(new DashboardGridRect(0, 0, 6, 2), out error));
            Assert.Null(error);
        }

        [Fact]
        public void TryValidate_RejectsNegativeXAndY_ZeroSize_AndOverflow()
        {
            string error;
            Assert.False(DashboardGridLayoutEngine.TryValidate(new DashboardGridRect(-1, 0, 6, 2), out error));
            Assert.False(DashboardGridLayoutEngine.TryValidate(new DashboardGridRect(0, -1, 6, 2), out error));
            Assert.False(DashboardGridLayoutEngine.TryValidate(new DashboardGridRect(0, 0, 0, 2), out error));
            Assert.False(DashboardGridLayoutEngine.TryValidate(new DashboardGridRect(0, 0, 6, 0), out error));
            Assert.False(DashboardGridLayoutEngine.TryValidate(new DashboardGridRect(0, 0, 13, 2), out error));
            Assert.False(DashboardGridLayoutEngine.TryValidate(new DashboardGridRect(8, 0, 6, 2), out error));
            Assert.False(DashboardGridLayoutEngine.TryValidate(new DashboardGridRect(12, 0, 1, 2), out error));
        }

        [Fact]
        public void Clamp_EnforcesMinMaxAndColumnBounds()
        {
            var negative = DashboardGridLayoutEngine.Clamp(new DashboardGridRect(-4, -2, 0, 0));
            Assert.Equal(new DashboardGridRect(0, 0, DashboardGridLayoutEngine.MinWidth, DashboardGridLayoutEngine.MinHeight), negative);

            var wide = DashboardGridLayoutEngine.Clamp(new DashboardGridRect(0, 0, 20, 2));
            Assert.Equal(12, wide.Width);
            Assert.Equal(0, wide.X);

            var overflow = DashboardGridLayoutEngine.Clamp(new DashboardGridRect(10, 0, 6, 2));
            Assert.Equal(6, overflow.X);
            Assert.Equal(6, overflow.Width);
        }

        [Fact]
        public void Overlaps_InteriorOverlap_NotEdgeTouch()
        {
            var a = new DashboardGridRect(0, 0, 6, 2);
            var b = new DashboardGridRect(5, 0, 6, 2);
            var edge = new DashboardGridRect(6, 0, 6, 2);
            var below = new DashboardGridRect(0, 2, 6, 2);
            Assert.True(DashboardGridLayoutEngine.Overlaps(a, b));
            Assert.False(DashboardGridLayoutEngine.Overlaps(a, edge));
            Assert.False(DashboardGridLayoutEngine.Overlaps(a, below));
        }

        [Fact]
        public void FirstFit_Empty_HalfThenFullRow()
        {
            var empty = Def();
            Assert.Equal(new DashboardGridRect(0, 0, 6, 2), DashboardGridLayoutEngine.FirstFit(empty.Widgets, 6, 2));

            empty.Widgets.Add(W("a", 0, 0, 6, 2));
            Assert.Equal(new DashboardGridRect(6, 0, 6, 2), DashboardGridLayoutEngine.FirstFit(empty.Widgets, 6, 2));

            empty.Widgets.Add(W("b", 6, 0, 6, 2));
            Assert.Equal(new DashboardGridRect(0, 2, 12, 3), DashboardGridLayoutEngine.FirstFit(empty.Widgets, 12, 3));
        }

        [Fact]
        public void FirstFit_UsesGap_AndDoesNotMoveExisting()
        {
            var def = Def(
                W("left", 0, 0, 6, 3),
                W("right", 6, 2, 6, 3));
            var gap = DashboardGridLayoutEngine.FirstFit(def.Widgets, 6, 2);
            Assert.Equal(new DashboardGridRect(6, 0, 6, 2), gap);
            Assert.Equal(0, Find(def, "left").Layout.X);
            Assert.Equal(6, Find(def, "right").Layout.X);
        }

        [Fact]
        public void PlaceNew_DoesNotMoveExistingWidgets()
        {
            var def = Def(W("a", 0, 0, 6, 2));
            var added = W("b", 0, 0, 6, 2);
            def.Widgets.Add(added);
            DashboardGridLayoutEngine.PlaceNew(def, added);
            Assert.Equal(0, Find(def, "a").Layout.X);
            Assert.Equal(0, Find(def, "a").Layout.Y);
            Assert.Equal(6, added.Layout.X);
            Assert.Equal(0, added.Layout.Y);
        }

        [Fact]
        public void Move_EmptySpace_LeftRight_AndRows()
        {
            var def = Def(W("a", 0, 0, 6, 2), W("b", 6, 0, 6, 2));
            DashboardGridLayoutEngine.Move(def, "a", new DashboardGridRect(0, 4, 6, 2));
            Assert.Equal(0, Find(def, "a").Layout.X);
            Assert.Equal(4, Find(def, "a").Layout.Y);
            Assert.Equal(6, Find(def, "b").Layout.X);
            Assert.Equal(0, Find(def, "b").Layout.Y);

            DashboardGridLayoutEngine.Move(def, "b", new DashboardGridRect(3, 0, 6, 2));
            Assert.Equal(3, Find(def, "b").Layout.X);
        }

        [Fact]
        public void Move_BeyondBoundaries_IsClamped()
        {
            var def = Def(W("a", 3, 1, 6, 2));
            DashboardGridLayoutEngine.Move(def, "a", new DashboardGridRect(-8, 1, 6, 2));
            Assert.Equal(0, Find(def, "a").Layout.X);
            DashboardGridLayoutEngine.Move(def, "a", new DashboardGridRect(20, 1, 6, 2));
            Assert.Equal(6, Find(def, "a").Layout.X);
        }

        [Fact]
        public void Move_CollisionPushesTargetDown_ActiveStays_UnrelatedUnchanged()
        {
            var def = Def(
                W("a", 0, 0, 6, 2),
                W("b", 6, 0, 6, 2),
                W("c", 0, 8, 6, 2));
            DashboardGridLayoutEngine.Move(def, "a", new DashboardGridRect(6, 0, 6, 2));
            Assert.Equal(6, Find(def, "a").Layout.X);
            Assert.Equal(0, Find(def, "a").Layout.Y);
            Assert.Equal(6, Find(def, "b").Layout.X);
            Assert.Equal(2, Find(def, "b").Layout.Y);
            Assert.Equal(0, Find(def, "c").Layout.X);
            Assert.Equal(8, Find(def, "c").Layout.Y);
        }

        [Fact]
        public void Move_CollisionCascade_IsDeterministic()
        {
            var def = Def(
                W("a", 0, 0, 12, 2),
                W("b", 0, 2, 12, 2),
                W("c", 0, 4, 12, 2));
            DashboardGridLayoutEngine.Move(def, "a", new DashboardGridRect(0, 2, 12, 2));
            Assert.Equal(2, Find(def, "a").Layout.Y);
            Assert.Equal(4, Find(def, "b").Layout.Y);
            Assert.Equal(6, Find(def, "c").Layout.Y);

            var again = Def(
                W("a", 0, 0, 12, 2),
                W("b", 0, 2, 12, 2),
                W("c", 0, 4, 12, 2));
            DashboardGridLayoutEngine.Move(again, "a", new DashboardGridRect(0, 2, 12, 2));
            Assert.Equal(Find(def, "b").Layout.Y, Find(again, "b").Layout.Y);
            Assert.Equal(Find(def, "c").Layout.Y, Find(again, "c").Layout.Y);
        }

        [Fact]
        public void Resize_GrowShrink_MaxMin_AndCollision()
        {
            var def = Def(W("a", 0, 0, 6, 2), W("b", 0, 2, 6, 2));
            DashboardGridLayoutEngine.Resize(def, "a", new DashboardGridRect(0, 0, 12, 2));
            Assert.Equal(12, Find(def, "a").Layout.Width);
            DashboardGridLayoutEngine.Resize(def, "a", new DashboardGridRect(0, 0, 6, 4));
            Assert.Equal(4, Find(def, "a").Layout.Height);
            Assert.Equal(4, Find(def, "b").Layout.Y);

            DashboardGridLayoutEngine.Resize(def, "a", new DashboardGridRect(0, 0, 3, 2));
            Assert.Equal(3, Find(def, "a").Layout.Width);
            Assert.Equal(2, Find(def, "a").Layout.Height);

            DashboardGridLayoutEngine.Resize(def, "a", new DashboardGridRect(0, 0, 1, 1));
            Assert.Equal(DashboardGridLayoutEngine.MinWidth, Find(def, "a").Layout.Width);
            Assert.Equal(DashboardGridLayoutEngine.MinHeight, Find(def, "a").Layout.Height);

            DashboardGridLayoutEngine.Resize(def, "a", new DashboardGridRect(0, 0, 20, 2));
            Assert.Equal(12, Find(def, "a").Layout.Width);
        }

        [Fact]
        public void HiddenWidgets_DoNotOccupy_AndUnhideResolvesCollision()
        {
            var def = Def(
                W("hidden", 0, 0, 6, 2, visible: false),
                W("visible", 0, 0, 6, 2));
            Assert.False(DashboardGridLayoutEngine.AnyVisibleOverlap(def.Widgets));
            Assert.Equal(new DashboardGridRect(6, 0, 6, 2), DashboardGridLayoutEngine.FirstFit(def.Widgets, 6, 2));

            DashboardGridLayoutEngine.Unhide(def, "hidden");
            Assert.True(Find(def, "hidden").Layout.IsVisible);
            Assert.Equal(0, Find(def, "hidden").Layout.X);
            Assert.Equal(0, Find(def, "hidden").Layout.Y);
            Assert.Equal(2, Find(def, "visible").Layout.Y);
            Assert.False(DashboardGridLayoutEngine.AnyVisibleOverlap(def.Widgets));
        }

        [Fact]
        public void SameOperation_IsDeterministic()
        {
            var a = Def(W("a", 0, 0, 6, 3), W("b", 6, 0, 6, 3));
            var b = Def(W("a", 0, 0, 6, 3), W("b", 6, 0, 6, 3));
            DashboardGridLayoutEngine.Move(a, "a", new DashboardGridRect(6, 0, 6, 3));
            DashboardGridLayoutEngine.Move(b, "a", new DashboardGridRect(6, 0, 6, 3));
            Assert.Equal(Find(a, "a").Layout.X, Find(b, "a").Layout.X);
            Assert.Equal(Find(a, "b").Layout.Y, Find(b, "b").Layout.Y);
        }

        private static DashboardDefinition Def(params DashboardWidgetDefinition[] widgets)
        {
            return new DashboardDefinition
            {
                SchemaVersion = DashboardPersistenceV2.CurrentSchemaVersion,
                Id = "default",
                Title = "Dashboard",
                ProjectKey = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                Widgets = widgets.ToList()
            };
        }

        private static DashboardWidgetDefinition W(string id, int x, int y, int width, int height, bool visible = true)
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
                    IsVisible = visible,
                    ColumnSpan = width <= 6 ? 1 : 2,
                    Order = y * DashboardGridLayoutEngine.Columns + x
                },
                Legacy = new DashboardLegacyWidgetContent
                {
                    WidgetKind = DashboardWidgetKinds.Chart,
                    ChartSource = "Types",
                    ChartKind = "Bar",
                    TopN = 8
                }
            };
        }

        private static DashboardWidgetDefinition Find(DashboardDefinition definition, string id)
        {
            return definition.Widgets.Single(w => w.Id == id);
        }
    }
}
