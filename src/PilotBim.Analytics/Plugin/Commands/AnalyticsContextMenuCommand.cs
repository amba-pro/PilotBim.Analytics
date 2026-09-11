using System;
using System.ComponentModel.Composition;
using Ascon.Pilot.SDK;
using Ascon.Pilot.SDK.Menu;
using PilotBim.Analytics.Diagnostics;
using PilotBim.Analytics.Properties;

namespace PilotBim.Analytics.Plugin.Commands
{
    [Export(typeof(IMenu<ObjectsViewContext>))]
    public sealed class AnalyticsContextMenuCommand : IMenu<ObjectsViewContext>
    {
        private const string OverviewCommand = "PilotBim_Analytics_Overview_Context";
        private const string CatalogCommand = "PilotBim_Analytics_DataSources_Context";

        private readonly AnalyticsCommandService _service;

        [ImportingConstructor]
        public AnalyticsContextMenuCommand(AnalyticsCommandService service)
        {
            _service = service;
        }

        public void Build(IMenuBuilder builder, ObjectsViewContext context)
        {
            try
            {
                if (builder == null)
                    return;

                AnalyticsLogger.Info("context-menu-build", "context=" + (context != null));

                var overview = builder.AddItem(OverviewCommand, 0);
                if (overview != null)
                    overview.WithHeader(Resources.Menu_Overview);

                var catalog = builder.AddItem(CatalogCommand, 1);
                if (catalog != null)
                    catalog.WithHeader(Resources.Menu_Catalog);
            }
            catch (Exception ex)
            {
                AnalyticsLogger.Error("context-menu-build", ex);
            }
        }

        public void OnMenuItemClick(string name, ObjectsViewContext context)
        {
            try
            {
                if (name == OverviewCommand)
                {
                    AnalyticsLogger.Info("context-menu-click", "overview");
                    _service.OpenAnalytics();
                    return;
                }

                if (name == CatalogCommand)
                {
                    AnalyticsLogger.Info("context-menu-click", "catalog");
                    _service.OpenCatalog();
                }
            }
            catch (Exception ex)
            {
                AnalyticsLogger.Error("context-menu-click", ex);
            }
        }
    }
}
