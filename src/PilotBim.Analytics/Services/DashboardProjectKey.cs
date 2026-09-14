using System;
using Ascon.Pilot.SDK;

namespace PilotBim.Analytics.Services
{
    /// <summary>
    /// Stable Pilot database identity for per-project dashboard files.
    /// Source: <see cref="IObjectsRepository.GetDatabaseId"/> (Guid).
    /// </summary>
    internal static class DashboardProjectKey
    {
        public static Guid FromRepository(IObjectsRepository repository)
        {
            if (repository == null)
                throw new ArgumentNullException("repository");
            return repository.GetDatabaseId();
        }

        public static string ToFolderName(Guid databaseId)
        {
            if (databaseId == Guid.Empty)
                throw new ArgumentException("database id is required.", "databaseId");
            return databaseId.ToString("D");
        }

        public static bool TryParse(string value, out Guid databaseId)
        {
            databaseId = Guid.Empty;
            if (string.IsNullOrWhiteSpace(value))
                return false;
            Guid parsed;
            if (!Guid.TryParse(value.Trim(), out parsed) || parsed == Guid.Empty)
                return false;
            databaseId = parsed;
            return true;
        }

        public static bool AreEqual(string left, Guid right)
        {
            Guid parsed;
            if (!TryParse(left, out parsed))
                return false;
            return parsed == right;
        }
    }
}
