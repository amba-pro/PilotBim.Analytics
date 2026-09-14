using System;
using System.Collections.Generic;

namespace PilotBim.Analytics.Models
{
    internal enum DashboardFilterOperator
    {
        Equals = 0,
        NotEquals = 1,
        IsEmpty = 2,
        IsNotEmpty = 3
    }

    /// <summary>
    /// Typed filter criterion. Display text is never stored. Persistence-ready primitives only.
    /// </summary>
    internal sealed class DashboardFilterValue
    {
        public DashboardFilterValue(DashboardFieldType kind, object value)
        {
            Kind = kind;
            Value = value;
        }

        public DashboardFieldType Kind { get; private set; }
        public object Value { get; private set; }

        public static DashboardFilterValue Text(string value)
        {
            return new DashboardFilterValue(DashboardFieldType.Text, value);
        }

        public static DashboardFilterValue Integer(long value)
        {
            return new DashboardFilterValue(DashboardFieldType.Integer, value);
        }

        public static DashboardFilterValue Number(double value)
        {
            return new DashboardFilterValue(DashboardFieldType.Number, value);
        }

        public static DashboardFilterValue Boolean(bool value)
        {
            return new DashboardFilterValue(DashboardFieldType.Boolean, value);
        }

        public static DashboardFilterValue Guid(Guid value)
        {
            return new DashboardFilterValue(DashboardFieldType.Guid, value);
        }

        public static DashboardFilterValue Identity(DashboardFieldType kind, string stableKey)
        {
            return new DashboardFilterValue(kind, stableKey);
        }
    }

    internal sealed class DashboardFilterDefinition
    {
        public DashboardFilterDefinition(string fieldId, DashboardFilterOperator op, DashboardFilterValue value)
        {
            FieldId = fieldId;
            Operator = op;
            Value = value;
        }

        public string FieldId { get; private set; }
        public DashboardFilterOperator Operator { get; private set; }
        public DashboardFilterValue Value { get; private set; }
    }
}
