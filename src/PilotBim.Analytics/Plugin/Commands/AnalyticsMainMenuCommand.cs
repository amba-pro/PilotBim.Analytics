using System;
using System.ComponentModel.Composition;
using Ascon.Pilot.SDK;
using Ascon.Pilot.SDK.Menu;
using PilotBim.Analytics.Diagnostics;
using PilotBim.Analytics.Properties;

namespace PilotBim.Analytics.Plugin.Commands
{
    [Export(typeof(IMenu<MainViewContext>))]
    public sealed class AnalyticsMainMenuCommand : IMenu<MainViewContext>
    {
        private const string CommandName = "PilotBim_Analytics_DataSources";

        private readonly AnalyticsCommandService _service;

        [ImportingConstructor]
        public AnalyticsMainMenuCommand(AnalyticsCommandService service)
        {
            _service = service;
        }

        public void Build(IMenuBuilder builder, MainViewContext context)
        {
            try
            {
                if (builder == null)
                    return;

                AnalyticsLogger.Info("main-menu-build", "register data sources");
                var item = builder.AddItem(CommandName, 0);
                if (item != null)
                    item.WithHeader(Resources.Menu_Catalog);
            }
            catch (Exception ex)
            {
                AnalyticsLogger.Error("main-menu-build", ex);
            }
        }

        public void OnMenuItemClick(string name, MainViewContext context)
        {
            if (name != CommandName)
                return;

            try
            {
                AnalyticsLogger.Info("main-menu-click", "catalog");
                _service.OpenCatalog();
            }
            catch (Exception ex)
            {
                AnalyticsLogger.Error("main-menu-click", ex);
            }
        }
    }

    [Export(typeof(IMenu<MainViewContext>))]
    public sealed class AnalyticsOverviewMenuCommand : IMenu<MainViewContext>
    {
        private const string CommandName = "PilotBim_Analytics_Overview";

        private readonly AnalyticsCommandService _service;

        [ImportingConstructor]
        public AnalyticsOverviewMenuCommand(AnalyticsCommandService service)
        {
            _service = service;
        }

        public void Build(IMenuBuilder builder, MainViewContext context)
        {
            try
            {
                if (builder == null)
                    return;

                AnalyticsLogger.Info("overview-menu-build", "register overview");
                var item = builder.AddItem(CommandName, 1);
                if (item != null)
                    item.WithHeader(Resources.Menu_Overview);
            }
            catch (Exception ex)
            {
                AnalyticsLogger.Error("overview-menu-build", ex);
            }
        }

        public void OnMenuItemClick(string name, MainViewContext context)
        {
            if (name != CommandName)
                return;

            try
            {
                AnalyticsLogger.Info("overview-menu-click", "overview");
                _service.OpenAnalytics();
            }
            catch (Exception ex)
            {
                AnalyticsLogger.Error("overview-menu-click", ex);
            }
        }
    }
}
