using System;
using System.Windows;
using System.Windows.Interop;
using ToleranceTool.Wpf.Scales;

namespace ToleranceTool.Wpf
{
    /// <summary>Entry points the Excel-DNA add-in calls to open a WPF window (owned by Excel).</summary>
    public static class WpfDialogs
    {
        public static void ScaleTypeEditor(IntPtr excelWindow) =>
            ShowModal(new ScaleTypeEditorWindow(), excelWindow);

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
