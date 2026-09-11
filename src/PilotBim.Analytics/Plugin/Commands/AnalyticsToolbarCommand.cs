using System;
using System.ComponentModel.Composition;
using Ascon.Pilot.SDK;
using Ascon.Pilot.SDK.Toolbar;
using PilotBim.Analytics.Diagnostics;
using PilotBim.Analytics.Properties;

namespace PilotBim.Analytics.Plugin.Commands
{
    [Export(typeof(IToolbar<ObjectsViewContext>))]
    public sealed class AnalyticsToolbarCommand : IToolbar<ObjectsViewContext>
    {
        private const string OverviewButton = "PilotBim_Analytics_Toolbar_Overview";
        private const string CatalogButton = "PilotBim_Analytics_Toolbar_Catalog";

        private readonly AnalyticsCommandService _service;

        [ImportingConstructor]
        public AnalyticsToolbarCommand(AnalyticsCommandService service)
        {
            _service = service;
        }

        public void Build(IToolbarBuilder builder, ObjectsViewContext context)
        {
            try
            {
                if (builder == null)
                    return;

                AnalyticsLogger.Info("toolbar-build", "context=" + (context != null));

                var overview = builder.AddButtonItem(OverviewButton, 0);
                if (overview != null)
                    overview.WithHeader(Resources.Toolbar_Analytics);

                var catalog = builder.AddButtonItem(CatalogButton, 1);
                if (catalog != null)
                    catalog.WithHeader(Resources.Toolbar_Catalog);
            }
            catch (Exception ex)
            {
                AnalyticsLogger.Error("toolbar-build", ex);
            }
        }

        public void OnToolbarItemClick(string name, ObjectsViewContext context)
        {
            try
            {
                if (name == OverviewButton)
                {
                    AnalyticsLogger.Info("toolbar-click", "overview");
                    _service.OpenAnalytics();
                    return;
                }

                if (name == CatalogButton)
                {
                    AnalyticsLogger.Info("toolbar-click", "catalog");
                    _service.OpenCatalog();
                }
            }
            catch (Exception ex)
            {
                AnalyticsLogger.Error("toolbar-click", ex);
            }
        }
    }
}
