using System;
using System.Windows;
using ToleranceTool.Excel.Datasheet;

namespace ToleranceTool.Wpf.Datasheet
{
    public partial class DatasheetMappingWindow : Window
    {
        public DatasheetMappingWindow(IDatasheet sheet, string? mappingXml = null, Action<string>? persist = null)
        {
            InitializeComponent();
            Title = $"Datasheet Mapping — {sheet.Name}";
            DataContext = new DatasheetMappingViewModel(sheet, mappingXml, persist);
        }
    }
}
