using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Microsoft.Win32;
using ToleranceTool.Configuration;
using ToleranceTool.Configuration.SignalTypes;
using ToleranceTool.Core.Signals;
using ToleranceTool.Wpf.Mvvm;

namespace ToleranceTool.Wpf.SignalTypes
{
    public sealed class SignalTypeRowVm : ObservableObject
    {
        private string _name = string.Empty;
        private string _rawLow = "0";
        private string _rawHigh = "0";
        private string _unit = string.Empty;

        public string Name { get => _name; set => Set(ref _name, value); }
        public string RawLow { get => _rawLow; set => Set(ref _rawLow, value); }
        public string RawHigh { get => _rawHigh; set => Set(ref _rawHigh, value); }
        public string Unit { get => _unit; set => Set(ref _unit, value); }

        public static SignalTypeRowVm From(SignalTypeSpec s) => new SignalTypeRowVm
        {
            Name = s.Name,
            RawLow = s.RawLow.ToString("R", CultureInfo.InvariantCulture),
            RawHigh = s.RawHigh.ToString("R", CultureInfo.InvariantCulture),
            Unit = s.Unit,
        };
    }

    public sealed class SignalTypeEditorViewModel : ObservableObject
    {
        private string _path;
        private string _status = string.Empty;

        public SignalTypeEditorViewModel(string? path = null)
        {
            _path = path ?? ConfigurationPaths.SignalTypeRegistryFile;

            LoadCommand = new RelayCommand(LoadFromDialog);
            SaveCommand = new RelayCommand(() => Save(_path));
            SaveAsCommand = new RelayCommand(SaveAs);

            if (File.Exists(_path))
            {
                Load(_path);
            }
            else
            {
                _status = "No signal-type registry yet. Add rows and Save.";
            }
        }

        public ObservableCollection<SignalTypeRowVm> Rows { get; } = new ObservableCollection<SignalTypeRowVm>();

        public string Status { get => _status; private set => Set(ref _status, value); }

        public ICommand LoadCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand SaveAsCommand { get; }

        private void Load(string path)
        {
            ConfigLoadResult<SignalTypeRegistry> result = SignalTypeRegistryXml.Load(path);
            Rows.Clear();
            foreach (SignalTypeSpec spec in result.Value.Specs)
            {
                Rows.Add(SignalTypeRowVm.From(spec));
            }

            _path = path;
            Status = result.Issues.Count == 0
                ? $"Loaded {Rows.Count} signal type(s) — {path}"
                : $"Loaded with {result.Issues.Count} issue(s) — {result.Issues[0].Message}";
        }

        private void LoadFromDialog()
        {
            var dialog = new OpenFileDialog { Filter = "Signal types (*.xml)|*.xml|All files (*.*)|*.*" };
            if (dialog.ShowDialog() == true)
            {
                Load(dialog.FileName);
            }
        }

        private void Save(string path)
        {
            var registry = new SignalTypeRegistry();
            foreach (SignalTypeRowVm row in Rows)
            {
                string name = row.Name?.Trim() ?? string.Empty;
                if (name.Length == 0)
                {
                    continue;
                }

                if (!double.TryParse(row.RawLow?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double low) ||
                    !double.TryParse(row.RawHigh?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double high))
                {
                    Status = $"\"{name}\": raw low/high must be numbers.";
                    return;
                }

                try
                {
                    registry.Add(new SignalTypeSpec { Name = name, RawLow = low, RawHigh = high, Unit = row.Unit?.Trim() ?? string.Empty });
                }
                catch (InvalidOperationException ex)
                {
                    Status = ex.Message;
                    return;
                }
            }

            try
            {
                SignalTypeRegistryXml.Save(registry, path);
                _path = path;
                Status = $"Saved {registry.Count} signal type(s) — {path}";
            }
            catch (Exception ex)
            {
                Status = "Save failed: " + ex.Message;
            }
        }

        private void SaveAs()
        {
            var dialog = new SaveFileDialog { Filter = "Signal types (*.xml)|*.xml", FileName = Path.GetFileName(_path) };
            if (dialog.ShowDialog() == true)
            {
                Save(dialog.FileName);
            }
        }
    }
}
