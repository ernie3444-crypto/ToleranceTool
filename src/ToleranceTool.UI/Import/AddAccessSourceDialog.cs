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
        private readonly TextBox _name = new TextBox { Dock = DockStyle.Fill };
        private readonly TextBox _database = new TextBox { Dock = DockStyle.Fill };
        private readonly TextBox _query = new TextBox { Dock = DockStyle.Fill, Multiline = true, Height = 90, ScrollBars = ScrollBars.Vertical };
        private readonly TextBox _keyColumn = new TextBox { Dock = DockStyle.Fill, Text = "UniversalId" };
        private readonly CheckBox _isMaster = new CheckBox { Text = "This source is the master", AutoSize = true };

        public AddAccessSourceDialog()
        {
            Text = "Add Access source";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(460, 300);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                AutoSize = true,
                Padding = new Padding(14, 12, 14, 4),
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            int r = 0;
            void Row(string label, Control control)
            {
                layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 7, 12, 3) }, 0, r);
                control.Margin = new Padding(3, 4, 3, 4);
                layout.Controls.Add(control, 1, r);
                r++;
            }

            var databaseRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true, Margin = new Padding(0) };
            databaseRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            databaseRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var browse = new Button { Text = "Browse…", AutoSize = true, Margin = new Padding(4, 3, 0, 3) };
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
            databaseRow.Controls.Add(_database, 0, 0);
            databaseRow.Controls.Add(browse, 1, 0);

            Row("Name", _name);
            Row("Database", databaseRow);
            Row("Query (SQL)", _query);
            Row("Universal ID column", _keyColumn);
            Row(string.Empty, _isMaster);

            var ok = new Button { Text = "OK", Width = 84, Margin = new Padding(4) };
            var cancel = new Button { Text = "Cancel", Width = 84, DialogResult = DialogResult.Cancel, Margin = new Padding(4) };
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

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 48, Padding = new Padding(10, 8, 10, 8) };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);

            Controls.Add(layout);
            Controls.Add(buttons);
            AcceptButton = ok;
            CancelButton = cancel;
            AutoScaleMode = AutoScaleMode.None;
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
