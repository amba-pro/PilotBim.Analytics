using System;
using System.IO;
using System.Text;
using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.Diagnostics
{
    internal static class AnalyticsLogger
    {
        private static readonly object Sync = new object();

        internal static string LogDirectory
        {
            get
            {
                var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(root, "PilotBim.Analytics", "Logs");
            }
        }

        internal static void Info(string area, string message)
        {
            Write("INFO", area, message, null);
        }

        internal static void Warning(string area, string message)
        {
            Write("WARNING", area, message, null);
        }

        internal static void Error(string area, Exception exception)
        {
            Write("ERROR", area, exception != null ? exception.Message : null, exception);
        }

        internal static void Error(string area, string message, Exception exception = null)
        {
            Write("ERROR", area, message, exception);
        }

        internal static void Blocked(string area, string message)
        {
            Write("BLOCKED", area, message, null);
        }

        internal static DiagnosticEntry CreateEntry(string level, string area, string message)
        {
            return new DiagnosticEntry
            {
                Timestamp = DateTime.Now,
                Level = level,
                Area = area,
                Message = Truncate(message, 500)
            };
        }

        private static void Write(string level, string area, string message, Exception exception)
        {
            try
            {
                Directory.CreateDirectory(LogDirectory);
                var path = Path.Combine(LogDirectory, "analytics.log");
                var sb = new StringBuilder();
                sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                sb.Append(" [").Append(level).Append("] ");
                sb.Append("area=").Append(area ?? "-");
                if (!string.IsNullOrEmpty(message))
                    sb.Append(" message=").Append(Truncate(message, 800));
                if (exception != null)
                {
                    sb.AppendLine();
                    sb.Append("exception=").Append(exception.GetType().FullName);
                    sb.Append(" :: ").Append(Truncate(exception.Message, 400));
                }
                sb.AppendLine();

                lock (Sync)
                {
                    File.AppendAllText(path, sb.ToString(), Encoding.UTF8);
                }
            }
            catch
            {
                // Logging must never throw into Pilot-BIM.
            }
        }

        private static string Truncate(string value, int max)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= max)
                return value;
            return value.Substring(0, max) + "...";
        }
    }
}
