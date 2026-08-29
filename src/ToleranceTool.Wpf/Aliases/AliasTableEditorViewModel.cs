using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Microsoft.Win32;
using ToleranceTool.Configuration;
using ToleranceTool.Configuration.Aliases;
using ToleranceTool.Wpf.Mvvm;

namespace ToleranceTool.Wpf.Aliases
{
    public sealed class AliasEntryRowVm : ObservableObject
    {
        private string _systemId = string.Empty;
        private string _targetKind = "SensorName";
        private string _target = string.Empty;
        private string _match = "exact";

        public string SystemId { get => _systemId; set => Set(ref _systemId, value); }
        public string TargetKind { get => _targetKind; set => Set(ref _targetKind, value); }
        public string Target { get => _target; set => Set(ref _target, value); }
        public string Match { get => _match; set => Set(ref _match, value); }

        public static AliasEntryRowVm From(AliasEntry e) => new AliasEntryRowVm
        {
            SystemId = e.SystemId,
            TargetKind = e.UniversalId != null ? "UniversalId" : "SensorName",
            Target = e.UniversalId ?? e.SensorName ?? string.Empty,
            Match = e.Match.ToString().ToLowerInvariant(),
        };
    }

    public sealed class AliasTableVm : ObservableObject
    {
        private string _name;
        private string _priority;

        public AliasTableVm(AliasTable table)
        {
            _name = table.Name;
            _priority = table.Priority.ToString(CultureInfo.InvariantCulture);
            Entries = new ObservableCollection<AliasEntryRowVm>(table.Entries.Select(AliasEntryRowVm.From));
        }

        public string Name { get => _name; set => Set(ref _name, value); }
        public string Priority { get => _priority; set => Set(ref _priority, value); }
        public ObservableCollection<AliasEntryRowVm> Entries { get; }

        public string Display => $"{Name}  (priority {Priority}, {Entries.Count})";

        public AliasTable ToModel()
        {
            var table = new AliasTable
            {
                Name = Name?.Trim() ?? string.Empty,
                Priority = int.TryParse(Priority?.Trim(), out int p) ? p : 0,
            };

            foreach (AliasEntryRowVm row in Entries)
            {
                string systemId = row.SystemId?.Trim() ?? string.Empty;
                string target = row.Target?.Trim() ?? string.Empty;
                if (systemId.Length == 0 || target.Length == 0)
                {
                    continue;
                }

                bool universal = string.Equals(row.TargetKind, "UniversalId", StringComparison.OrdinalIgnoreCase);
                Enum.TryParse(row.Match ?? "exact", true, out AliasMatch match);
                table.Entries.Add(new AliasEntry
                {
                    SystemId = systemId,
                    SensorName = universal ? null : target,
                    UniversalId = universal ? target : null,
                    Match = match,
                });
            }

            return table;
        }
    }

    public sealed class AliasTableEditorViewModel : ObservableObject
    {
        private string _path;
        private AliasTableVm? _selected;
        private string _status = string.Empty;

        public AliasTableEditorViewModel(string? path = null)
        {
            _path = path ?? ConfigurationPaths.AliasTablesFile;

            AddCommand = new RelayCommand(Add);
            DeleteCommand = new RelayCommand(Delete, () => Selected != null);
            LoadCommand = new RelayCommand(LoadFromDialog);
            SaveCommand = new RelayCommand(() => Save(_path));
            SaveAsCommand = new RelayCommand(SaveAs);

            if (File.Exists(_path))
            {
                Load(_path);
            }
            else
            {
                _status = "No alias tables yet. Add a table to start.";
            }
        }

        public ObservableCollection<AliasTableVm> Tables { get; } = new ObservableCollection<AliasTableVm>();

        public AliasTableVm? Selected { get => _selected; set => Set(ref _selected, value); }

        public string Status { get => _status; private set => Set(ref _status, value); }

        public string[] TargetKinds { get; } = { "SensorName", "UniversalId" };
        public string[] MatchModes { get; } = { "exact", "contains", "regex" };

        public ICommand AddCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand LoadCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand SaveAsCommand { get; }

        private void Load(string path)
        {
            ConfigLoadResult<AliasTableSet> result = AliasTablesXml.Load(path);
            Tables.Clear();
            foreach (AliasTable table in result.Value.Tables)
            {
                Tables.Add(new AliasTableVm(table));
            }

            _path = path;
            Selected = Tables.FirstOrDefault();
            Status = result.Issues.Count == 0
                ? $"Loaded {Tables.Count} table(s) — {path}"
                : $"Loaded with {result.Issues.Count} issue(s) — {result.Issues[0].Message}";
        }

        private void LoadFromDialog()
        {
            var dialog = new OpenFileDialog { Filter = "Alias tables (*.xml)|*.xml|All files (*.*)|*.*" };
            if (dialog.ShowDialog() == true)
            {
                Load(dialog.FileName);
            }
        }

        private void Save(string path)
        {
            var set = new AliasTableSet();
            foreach (AliasTableVm vm in Tables)
            {
                set.Add(vm.ToModel());
            }

            try
            {
                AliasTablesXml.Save(set, path);
                _path = path;
                Status = $"Saved {set.Tables.Count} table(s) — {path}";
            }
            catch (Exception ex)
            {
                Status = "Save failed: " + ex.Message;
            }
        }

        private void SaveAs()
        {
            var dialog = new SaveFileDialog { Filter = "Alias tables (*.xml)|*.xml", FileName = Path.GetFileName(_path) };
            if (dialog.ShowDialog() == true)
            {
                Save(dialog.FileName);
            }
        }

        private void Add()
        {
            var vm = new AliasTableVm(new AliasTable { Name = "New table", Priority = (Tables.Count + 1) * 10 });
            Tables.Add(vm);
            Selected = vm;
        }

        private void Delete()
        {
            if (Selected == null)
            {
                return;
            }

            int index = Tables.IndexOf(Selected);
            Tables.Remove(Selected);
            Selected = Tables.Count == 0 ? null : Tables[Math.Min(index, Tables.Count - 1)];
        }
    }
}
