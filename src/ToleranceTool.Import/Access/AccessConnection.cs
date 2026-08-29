using System;
using System.IO;

namespace ToleranceTool.Import.Access
{
    /// <summary>
    /// Builds OLE DB connection strings for Access databases. The ACE provider
    /// (Microsoft Access Database Engine redistributable) must be installed on the
    /// machine; this class does not touch the database itself.
    /// </summary>
    public static class AccessConnection
    {
        public const string AceProvider = "Microsoft.ACE.OLEDB.12.0";
        public const string JetProvider = "Microsoft.Jet.OLEDB.4.0";

        /// <summary>
        /// A connection string for <paramref name="databasePath"/>. Already-formed
        /// connection strings (containing "Provider=") are returned unchanged.
        /// </summary>
        public static string ForDatabase(string databasePath, string? password = null)
        {
            if (string.IsNullOrWhiteSpace(databasePath))
            {
                throw new ArgumentException("A database path is required.", nameof(databasePath));
            }

            if (databasePath.IndexOf("Provider=", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return databasePath;
            }

            string extension = Path.GetExtension(databasePath).ToLowerInvariant();
            string provider = extension == ".mdb" ? JetProvider : AceProvider;

            string connectionString = $"Provider={provider};Data Source={databasePath};";
            if (!string.IsNullOrEmpty(password))
            {
                connectionString += provider == JetProvider
                    ? $"Jet OLEDB:Database Password={password};"
                    : $"Jet OLEDB:Database Password={password};";
            }

            return connectionString;
        }

        public static bool LooksLikeConnectionString(string value) =>
            !string.IsNullOrWhiteSpace(value) && value.IndexOf("Provider=", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
