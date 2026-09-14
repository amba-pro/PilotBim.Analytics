using System;
using Ascon.Pilot.SDK;
using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.Services
{
    /// <summary>
    /// Narrow SDK → <see cref="DashboardObjectSource"/> mapping. No extra repository calls.
    /// </summary>
    internal static class DashboardObjectSourceAdapter
    {
        public static DashboardObjectSource FromDataObject(IDataObject obj, int expectedTypeId)
        {
            if (obj == null)
                return null;

            int typeId = expectedTypeId;
            try
            {
                if (obj.Type != null)
                    typeId = obj.Type.Id;
            }
            catch
            {
            }

            int? creatorId = null;
            try
            {
                if (obj.Creator != null)
                    creatorId = obj.Creator.Id;
            }
            catch
            {
            }

            string objectState = null;
            try
            {
                if (obj.ObjectStateInfo != null)
                    objectState = obj.ObjectStateInfo.State.ToString();
            }
            catch
            {
            }

            var source = new DashboardObjectSource
            {
                Id = obj.Id,
                TypeId = typeId,
                ParentId = obj.ParentId,
                CreatorId = creatorId,
                Created = obj.Created,
                ObjectState = objectState,
                Attributes = new System.Collections.Generic.List<DashboardAttributeSource>()
            };

            try
            {
                var type = obj.Type;
                if (type != null && type.Attributes != null)
                {
                    foreach (var attr in type.Attributes)
                    {
                        if (attr == null || string.IsNullOrWhiteSpace(attr.Name))
                            continue;
                        var item = new DashboardAttributeSource
                        {
                            Name = attr.Name,
                            ValueType = attr.Type.ToString(),
                            Present = false,
                            Value = null
                        };
                        try
                        {
                            if (obj.Attributes != null && obj.Attributes.ContainsKey(attr.Name))
                            {
                                item.Present = true;
                                item.Value = obj.Attributes[attr.Name];
                            }
                        }
                        catch
                        {
                            item.Present = true;
                            item.Value = null;
                        }
                        source.Attributes.Add(item);
                    }
                }
            }
            catch
            {
            }

            return source;
        }
    }
}
