using System;
using System.Windows;
using System.Windows.Interop;
using ToleranceTool.Excel.Datasheet;
using ToleranceTool.Wpf.Aliases;
using ToleranceTool.Wpf.Datasheet;
using ToleranceTool.Wpf.Import;
using ToleranceTool.Wpf.Scales;
using ToleranceTool.Wpf.SignalTypes;
using ToleranceTool.Wpf.Tolerances;

namespace ToleranceTool.Wpf
{
    /// <summary>Entry points the Excel-DNA add-in calls to open a WPF window (owned by Excel).</summary>
    public static class WpfDialogs
    {
        public static void ScaleTypeEditor(IntPtr excelWindow) => ShowModal(new ScaleTypeEditorWindow(), excelWindow);

        public static void SignalTypeEditor(IntPtr excelWindow) => ShowModal(new SignalTypeEditorWindow(), excelWindow);

        public static void AliasTableEditor(IntPtr excelWindow) => ShowModal(new AliasTableEditorWindow(), excelWindow);

        public static void ToleranceEditor(IntPtr excelWindow) => ShowModal(new ToleranceEditorWindow(), excelWindow);

        public static void SignalImport(IntPtr excelWindow) => ShowModal(new SignalImportWindow(), excelWindow);

        public static void DatasheetMapping(IntPtr excelWindow, IDatasheet sheet, string? mappingXml) =>
            ShowModal(new DatasheetMappingWindow(sheet, mappingXml), excelWindow);

        private static void ShowModal(Window window, IntPtr owner)
        {
            try
            {
                if (owner != IntPtr.Zero)
                {
                    new WindowInteropHelper(window).Owner = owner;
                }

                window.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Tolerance Tool", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
