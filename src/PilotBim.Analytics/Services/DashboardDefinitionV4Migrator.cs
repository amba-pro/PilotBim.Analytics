using System.Collections.Generic;
using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.Services
{
    /// <summary>
    /// In-memory V3 (and earlier via V3) → V4. Adds empty DashboardFilters. Does not write files.
    /// </summary>
    internal static class DashboardDefinitionV4Migrator
    {
        public static DashboardDefinition ToCurrent(DashboardDefinition source)
        {
            if (source == null)
            {
                return new DashboardDefinition
                {
                    SchemaVersion = DashboardPersistenceV2.CurrentSchemaVersion,
                    Id = DashboardPersistenceV2.DefaultDashboardId,
                    Title = DashboardPersistenceV2.DefaultTitle,
                    Widgets = new List<DashboardWidgetDefinition>(),
                    DashboardFilters = new List<DashboardLevelFilterDefinition>()
                };
            }

            if (source.SchemaVersion == DashboardPersistenceV2.SchemaVersion)
                source = DashboardDefinitionV3Migrator.FromV2(source);
            else if (source.SchemaVersion != DashboardPersistenceV2.SchemaVersionV3
                && source.SchemaVersion != DashboardPersistenceV2.CurrentSchemaVersion)
            {
                source = DashboardDefinitionCopy.Clone(source) ?? source;
            }

            var clone = DashboardDefinitionCopy.Clone(source) ?? new DashboardDefinition
            {
                Widgets = new List<DashboardWidgetDefinition>()
            };

            clone.SchemaVersion = DashboardPersistenceV2.CurrentSchemaVersion;
            if (clone.Widgets == null)
                clone.Widgets = new List<DashboardWidgetDefinition>();
            if (clone.DashboardFilters == null)
                clone.DashboardFilters = new List<DashboardLevelFilterDefinition>();
            return clone;
        }
    }
}
