using System;
using PilotBim.Analytics.Services;

namespace PilotBim.Analytics.ViewModels
{
    /// <summary>
    /// Composition for Dashboard V2. Built at the Analytics window / inventory root.
    /// Not a singleton. Not MEF.
    /// </summary>
    internal sealed class AnalyticsDashboardRuntimeOptions
    {
        public Guid ProjectKey { get; set; }
        public DashboardDefinitionStore DefinitionStore { get; set; }
        public DashboardLayoutStore LayoutStore { get; set; }
        public IDashboardTypeDatasetProvider TypeDatasetProvider { get; set; }
        public Action<Action> PostToUi { get; set; }
    }
}
