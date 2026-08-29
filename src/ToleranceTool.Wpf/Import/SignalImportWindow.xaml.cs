using System.ComponentModel;
using System.Windows;

namespace ToleranceTool.Wpf.Import
{
    public partial class SignalImportWindow : Window
    {
        private readonly SignalImportViewModel _vm;

        public SignalImportWindow()
        {
            InitializeComponent();
            _vm = new SignalImportViewModel();
            DataContext = _vm;
        }

        private void Window_Closing(object sender, CancelEventArgs e) => _vm.SaveSources();
    }
}
