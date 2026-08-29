using System;
using System.Drawing;
using System.Windows.Forms;
using ToleranceTool.Import;
using ToleranceTool.Import.Access;

namespace ToleranceTool.UI.Import
{
    /// <summary>Collects an Access database path + query for a new signal source (architecture doc §7, Method B).</summary>
    public sealed class AddAccessSourceDialog : Form
    {
        private readonly TextBox _name = new TextBox { Width = 320 };
        private readonly TextBox _database = new TextBox { Width = 320 };
        private readonly TextBox _query = new TextBox { Width = 320, Multiline = true, Height = 90, ScrollBars = ScrollBars.Vertical };
        private readonly TextBox _keyColumn = new TextBox { Width = 320, Text = "UniversalId" };
        private readonly CheckBox _isMaster = new CheckBox { Text = "This source is the master", AutoSize = true };

        public AddAccessSourceDialog()
        {
            Text = "Add Access source";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(430, 320);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(12) };
            void Row(string label, Control control)
            {
                layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 8, 3) });
                layout.Controls.Add(control);
            }

            var databaseRow = new FlowLayoutPanel { AutoSize = true };
            databaseRow.Controls.Add(_database);
            var browse = new Button { Text = "…", Width = 30 };
            browse.Click += (s, e) =>
            {
                using (var open = new OpenFileDialog { Filter = "Access database (*.accdb;*.mdb)|*.accdb;*.mdb|All files (*.*)|*.*" })
                {
                    if (open.ShowDialog(this) == DialogResult.OK)
                    {
                        _database.Text = open.FileName;
                        if (_name.Text.Length == 0)
                        {
                            _name.Text = System.IO.Path.GetFileNameWithoutExtension(open.FileName);
                        }
                    }
                }
            };
            databaseRow.Controls.Add(browse);

            Row("Name", _name);
            Row("Database", databaseRow);
            Row("Query (SQL)", _query);
            Row("Universal ID column", _keyColumn);
            Row(string.Empty, _isMaster);

            var ok = new Button { Text = "OK", Width = 80 };
            var cancel = new Button { Text = "Cancel", Width = 80, DialogResult = DialogResult.Cancel };
            ok.Click += (s, e) =>
            {
                if (_database.Text.Trim().Length == 0 || _query.Text.Trim().Length == 0)
                {
                    MessageBox.Show(this, "A database and a query are required.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                DialogResult = DialogResult.OK;
                Close();
            };

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 44, Padding = new Padding(8) };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);

            Controls.Add(layout);
            Controls.Add(buttons);
            AcceptButton = ok;
            CancelButton = cancel;
        }

        public ImportSourceDefinition BuildDefinition()
        {
            string location = _database.Text.Trim();
            var definition = new ImportSourceDefinition(
                _name.Text.Trim().Length > 0 ? _name.Text.Trim() : System.IO.Path.GetFileName(location),
                SignalSourceKind.Access,
                AccessConnection.LooksLikeConnectionString(location) ? location : AccessConnection.ForDatabase(location))
            {
                Query = _query.Text.Trim(),
                UniversalIdLocator = _keyColumn.Text.Trim(),
                IsMaster = _isMaster.Checked,
            };

            foreach (SignalField field in SignalField.All)
            {
                definition.Fields.Add(new FieldBinding(field.Name, field.Name, field.RequiredByDefault));
            }

            return definition;
        }
    }
}
