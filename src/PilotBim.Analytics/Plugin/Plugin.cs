using System;
using System.ComponentModel.Composition;
using System.Reflection;
using Ascon.Pilot.SDK;
using PilotBim.Analytics.Diagnostics;

namespace PilotBim.Analytics
{
    [Export(typeof(IDataPlugin))]
    public sealed class Extension : IDataPlugin
    {
        [ImportingConstructor]
        public Extension()
        {
            try
            {
                var asm = typeof(Extension).Assembly;
                var ver = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                    ?? asm.GetName().Version?.ToString()
                    ?? "?";
                AnalyticsLogger.Info("plugin load",
                    "PilotBim.Analytics loaded v" + ver + "; exports=IDataPlugin,IMenu,IToolbar");
            }
            catch
            {
            }
        }
    }
}
