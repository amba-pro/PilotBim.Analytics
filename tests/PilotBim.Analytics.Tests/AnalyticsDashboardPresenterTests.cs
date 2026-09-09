using System.Linq;
using PilotBim.Analytics.Models;
using PilotBim.Analytics.Services;
using PilotBim.Analytics.ViewModels;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    public sealed class AnalyticsDashboardPresenterTests
    {
        [Fact]
        public void AddWidget_NullDraft_DoesNotChangeLayout()
        {
            var presenter = new AnalyticsDashboardPresenter(_ => { }, () => null);
            var before = presenter.DashboardLayoutItems.Count;

            presenter.AddWidget(null);

            Assert.Equal(before, presenter.DashboardLayoutItems.Count);
        }

        [Fact]
        public void MoveBlock_UnknownId_DoesNotChangeOrder()
        {
            var presenter = new AnalyticsDashboardPresenter(_ => { }, () => null);
            var before = presenter.DashboardLayoutItems.Select(i => i.Id).ToList();

            presenter.MoveBlock("__missing__", 1);

            Assert.Equal(before, presenter.DashboardLayoutItems.Select(i => i.Id).ToList());
        }

        [Fact]
        public void RemoveWidget_Builtin_DoesNotRemove()
        {
            var presenter = new AnalyticsDashboardPresenter(_ => { }, () => null);
            var kpi = presenter.GetWidgetState(DashboardBlockIds.Kpi);
            Assert.NotNull(kpi);

            presenter.RemoveWidget(DashboardBlockIds.Kpi);

            Assert.NotNull(presenter.GetWidgetState(DashboardBlockIds.Kpi));
        }

        [Fact]
        public void GetWidgetState_UnknownId_ReturnsNull()
        {
            var presenter = new AnalyticsDashboardPresenter(_ => { }, () => null);
            Assert.Null(presenter.GetWidgetState("__no_such_widget__"));
        }
    }
}
