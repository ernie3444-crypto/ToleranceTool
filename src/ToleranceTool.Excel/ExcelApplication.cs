using ExcelDna.Integration;

namespace ToleranceTool.Excel
{
    /// <summary>
    /// Thin accessor for the running Excel Application COM object. Kept here so the
    /// rest of the codebase never touches ExcelDna directly.
    /// </summary>
    public static class ExcelApplication
    {
        /// <summary>The Excel Application object, late-bound.</summary>
        public static dynamic Current => ExcelDnaUtil.Application;

        /// <summary>The active worksheet — the datasheet the tool operates on.</summary>
        public static dynamic ActiveSheet => Current.ActiveSheet;

        public static string ActiveSheetName
        {
            get
            {
                dynamic? sheet = Current.ActiveSheet;
                return sheet == null ? string.Empty : (string)sheet.Name;
            }
        }
    }
}
