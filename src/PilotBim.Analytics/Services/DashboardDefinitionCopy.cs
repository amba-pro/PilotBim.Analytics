using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.Services
{
    /// <summary>
    /// Deep-copies a V2 definition so mutations can be attempted without touching the live object.
    /// </summary>
    internal static class DashboardDefinitionCopy
    {
        public static DashboardDefinition Clone(DashboardDefinition source)
        {
            if (source == null)
                return null;

            var serializer = new DataContractJsonSerializer(
                typeof(DashboardDefinition),
                new DataContractJsonSerializerSettings
                {
                    UseSimpleDictionaryFormat = true,
                    EmitTypeInformation = EmitTypeInformation.Never
                });

            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, source);
                stream.Position = 0;
                return serializer.ReadObject(stream) as DashboardDefinition;
            }
        }
    }
}
