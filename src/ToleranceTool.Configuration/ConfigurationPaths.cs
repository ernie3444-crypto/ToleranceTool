using System;
using System.IO;

namespace ToleranceTool.Configuration
{
    /// <summary>
    /// Resolves where the shared configuration libraries live. Per-user, under
    /// %APPDATA%\ToleranceTool\ , each overridable per workbook.
    /// </summary>
    public static class ConfigurationPaths
    {
        public const string FolderName = "ToleranceTool";

        public static string RootFolder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), FolderName);

        public static string AliasTablesFile => Path.Combine(RootFolder, "alias-tables.xml");

        public static string SignalTypeRegistryFile => Path.Combine(RootFolder, "signal-types.xml");

        public static string ScaleTypeLibraryFile => Path.Combine(RootFolder, "scale-types.xml");

        public static string ToleranceLibraryFile => Path.Combine(RootFolder, "tolerances.xml");

        /// <summary>Creates the root folder if it does not yet exist.</summary>
        public static void EnsureRootFolder()
        {
            Directory.CreateDirectory(RootFolder);
        }
    }
}
