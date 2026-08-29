using System.Windows;

namespace ToleranceTool.Wpf.Tolerances
{
    public partial class AddDefinitionWindow : Window
    {
        public AddDefinitionWindow()
        {
            InitializeComponent();
        }

        public string SignalTypeName => SignalTypeBox.Text.Trim();

        public string ModuleTypeName => ModuleTypeBox.Text.Trim();

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (SignalTypeName.Length == 0 || ModuleTypeName.Length == 0)
            {
                MessageBox.Show(this, "Both fields are required.", "New tolerance", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
        }
    }
}
