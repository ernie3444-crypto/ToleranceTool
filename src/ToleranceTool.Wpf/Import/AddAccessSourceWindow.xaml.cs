using System.IO;
using System.Windows;
using Microsoft.Win32;
using ToleranceTool.Import;
using ToleranceTool.Import.Access;

namespace ToleranceTool.Wpf.Import
{
    public partial class AddAccessSourceWindow : Window
    {
        public AddAccessSourceWindow()
        {
            InitializeComponent();
        }

        public ImportSourceDefinition? Result { get; private set; }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Filter = "Access database (*.accdb;*.mdb)|*.accdb;*.mdb|All files (*.*)|*.*" };
            if (dialog.ShowDialog() == true)
            {
                DatabaseBox.Text = dialog.FileName;
                if (NameBox.Text.Length == 0)
                {
                    NameBox.Text = Path.GetFileNameWithoutExtension(dialog.FileName);
                }
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            string database = DatabaseBox.Text.Trim();
            string query = QueryBox.Text.Trim();
            if (database.Length == 0 || query.Length == 0)
            {
                MessageBox.Show(this, "A database and a query are required.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string location = AccessConnection.LooksLikeConnectionString(database) ? database : AccessConnection.ForDatabase(database);
            var definition = new ImportSourceDefinition(
                NameBox.Text.Trim().Length > 0 ? NameBox.Text.Trim() : Path.GetFileName(database),
                SignalSourceKind.Access,
                location)
            {
                Query = query,
                UniversalIdLocator = KeyBox.Text.Trim(),
                IsMaster = MasterBox.IsChecked == true,
            };

            foreach (SignalField field in SignalField.All)
            {
                definition.Fields.Add(new FieldBinding(field.Name, field.Name, field.RequiredByDefault));
            }

            Result = definition;
            DialogResult = true;
        }
    }
}
