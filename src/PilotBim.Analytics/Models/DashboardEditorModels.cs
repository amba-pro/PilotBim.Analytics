using System;
using System.Collections.Generic;
using System.Linq;
using PilotBim.Analytics.Properties;

namespace PilotBim.Analytics.Models
{
    /// <summary>Machine TypeId + display title. Display is never query identity.</summary>
    internal sealed class DashboardObjectTypeOption
    {
        public DashboardObjectTypeOption(int typeId, string displayName, bool isUnavailable)
        {
            TypeId = typeId;
            DisplayName = displayName ?? string.Empty;
            IsUnavailable = isUnavailable;
        }

        public int TypeId { get; private set; }
        public string DisplayName { get; private set; }
        public bool IsUnavailable { get; private set; }

        public string Label
        {
            get
            {
                if (IsUnavailable)
                    return string.Format(Resources.QueryEditor_UnavailableType, TypeId);
                return string.IsNullOrWhiteSpace(DisplayName)
                    ? TypeId.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : DisplayName;
            }
        }

        public static IReadOnlyList<DashboardObjectTypeOption> FromInventory(IEnumerable<TypeInventoryRecord> types)
        {
            var list = new List<DashboardObjectTypeOption>();
            if (types == null)
                return list;
            foreach (var type in types)
            {
                if (type == null)
                    continue;
                var display = !string.IsNullOrWhiteSpace(type.Title)
                    ? type.Title
                    : (!string.IsNullOrWhiteSpace(type.Name) ? type.Name : type.TypeId.ToString(System.Globalization.CultureInfo.InvariantCulture));
                list.Add(new DashboardObjectTypeOption(type.TypeId, display, false));
            }

            return list
                .OrderBy(t => t.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(t => t.TypeId)
                .ToList();
        }
    }

    internal sealed class DashboardFieldOption
    {
        public DashboardFieldOption(string fieldId, string displayName, DashboardFieldType fieldType, bool isNone, bool isUnavailable)
        {
            FieldId = fieldId;
            DisplayName = displayName ?? string.Empty;
            FieldType = fieldType;
            IsNone = isNone;
            IsUnavailable = isUnavailable;
        }

        public string FieldId { get; private set; }
        public string DisplayName { get; private set; }
        public DashboardFieldType FieldType { get; private set; }
        public bool IsNone { get; private set; }
        public bool IsUnavailable { get; private set; }

        public string Label
        {
            get
            {
                if (IsNone)
                    return Resources.QueryEditor_GroupByNone;
                if (IsUnavailable)
                    return string.Format(Resources.QueryEditor_UnavailableField, FieldId);
                return DisplayName;
            }
        }

        public static DashboardFieldOption None()
        {
            return new DashboardFieldOption(null, Resources.QueryEditor_GroupByNone, DashboardFieldType.Unknown, true, false);
        }

        public static DashboardFieldOption Unavailable(string fieldId)
        {
            return new DashboardFieldOption(fieldId, fieldId, DashboardFieldType.Unknown, false, true);
        }

        public static DashboardFieldOption FromDescriptor(DashboardFieldDescriptor descriptor)
        {
            if (descriptor == null)
                return null;
            return new DashboardFieldOption(descriptor.Id, descriptor.DisplayName, descriptor.FieldType, false, false);
        }
    }

    internal sealed class DashboardEditorChoice
    {
        public DashboardEditorChoice(string id, string title)
        {
            Id = id;
            Title = title ?? string.Empty;
        }

        public string Id { get; private set; }
        public string Title { get; private set; }
    }
}
