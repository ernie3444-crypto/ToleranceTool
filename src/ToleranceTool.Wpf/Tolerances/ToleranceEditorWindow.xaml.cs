using System.Windows;

namespace ToleranceTool.Wpf.Tolerances
{
    public partial class ToleranceEditorWindow : Window
    {
        private readonly ToleranceEditorViewModel _vm;

        public ToleranceEditorWindow(string? path = null)
        {
            InitializeComponent();
            _vm = new ToleranceEditorViewModel(path);
            DataContext = _vm;
        }

        private void AddDefinition_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AddDefinitionWindow { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                string? error = _vm.AddDefinition(dialog.SignalTypeName, dialog.ModuleTypeName);
                if (error != null)
                {
                    MessageBox.Show(this, error, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void AddTerm_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.Selected == null)
            {
                return;
            }

            var dialog = new ToleranceTermWindow(null) { Owner = this };
            if (dialog.ShowDialog() == true && dialog.Result != null)
            {
                _vm.AddTerm(dialog.Result);
            }
        }

        private void EditTerm_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.Selected == null || _vm.SelectedTerm == null)
            {
                return;
            }

            var dialog = new ToleranceTermWindow(_vm.SelectedTerm.Term) { Owner = this };
            if (dialog.ShowDialog() == true && dialog.Result != null)
            {
                _vm.ReplaceSelectedTerm(dialog.Result);
            }
        }
    }
}
