using System;
using System.IO;
using System.Linq;

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

        /// <summary>The joined signal set from the last import — the datasheet run reads this.</summary>
        public static string ResolvedSignalSetFile => Path.Combine(RootFolder, "last-signal-set.xml");

        /// <summary>The import wizard's source definitions + field mappings (stopgap for the in-workbook store).</summary>
        public static string ImportSourcesFile => Path.Combine(RootFolder, "import-sources.xml");

        /// <summary>Folder holding one datasheet-mapping file per worksheet (stopgap for the in-workbook store).</summary>
        public static string SheetsFolder => Path.Combine(RootFolder, "sheets");

        public static string SheetMappingFile(string sheetName)
        {
            string safe = string.Concat((sheetName ?? string.Empty)
                .Select(c => Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 ? '_' : c));
            return Path.Combine(SheetsFolder, safe + ".xml");
        }

        /// <summary>Creates the root folder if it does not yet exist.</summary>
        public static void EnsureRootFolder()
        {
            Directory.CreateDirectory(RootFolder);
        }
    }
}
