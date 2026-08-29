using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Microsoft.Win32;
using ToleranceTool.Configuration;
using ToleranceTool.Configuration.SignalTypes;
using ToleranceTool.Core.Signals;
using ToleranceTool.Import;
using ToleranceTool.Import.Access;
using ToleranceTool.Wpf.Mvvm;

namespace ToleranceTool.Wpf.Import
{
    public sealed class PreviewRowVm
    {
        public string UniversalId { get; set; } = string.Empty;
        public string SensorName { get; set; } = string.Empty;
        public string Conversion { get; set; } = string.Empty;
        public string Scale { get; set; } = string.Empty;
        public string Signal { get; set; } = string.Empty;
        public string Module { get; set; } = string.Empty;
        public string RawLo { get; set; } = string.Empty;
        public string RawHi { get; set; } = string.Empty;
        public string EuLo { get; set; } = string.Empty;
        public string EuHi { get; set; } = string.Empty;
        public string EuLoSi { get; set; } = string.Empty;
        public string EuHiSi { get; set; } = string.Empty;
        public bool Complete { get; set; }
        public string Missing { get; set; } = string.Empty;
    }

    public sealed class SignalImportViewModel : ObservableObject
    {
        private SourceVm? _selected;
        private string _status = string.Empty;
        private bool _hideIncomplete;
        private ResolvedSignalSet _result = new ResolvedSignalSet(Array.Empty<ResolvedSignal>());

        public SignalImportViewModel()
        {
            AddFileCommand = new RelayCommand(AddFile);
            AddAccessCommand = new RelayCommand(AddAccess);
            RemoveCommand = new RelayCommand(Remove, () => Selected != null);
            BuildPreviewCommand = new RelayCommand(BuildPreview);
            SaveForDatasheetCommand = new RelayCommand(SaveForDatasheet);
            ExportCommand = new RelayCommand(Export);

            LoadSavedSources();
        }

        public ObservableCollection<SourceVm> Sources { get; } = new ObservableCollection<SourceVm>();

        public SourceVm? Selected { get => _selected; set => Set(ref _selected, value); }

        public ObservableCollection<PreviewRowVm> PreviewRows { get; } = new ObservableCollection<PreviewRowVm>();

        public ObservableCollection<string> Issues { get; } = new ObservableCollection<string>();

        public string Status { get => _status; private set => Set(ref _status, value); }

        public bool HideIncomplete
        {
            get => _hideIncomplete;
            set { if (Set(ref _hideIncomplete, value) && PreviewRows.Count > 0) FillPreview(); }
        }

        public SignalDataOrientation[] Layouts { get; } =
        {
            SignalDataOrientation.RowPerSignal,
            SignalDataOrientation.ColumnPerSignal,
            SignalDataOrientation.ParameterPerRow,
        };

        public ICommand AddFileCommand { get; }
        public ICommand AddAccessCommand { get; }
        public ICommand RemoveCommand { get; }
        public ICommand BuildPreviewCommand { get; }
        public ICommand SaveForDatasheetCommand { get; }
        public ICommand ExportCommand { get; }

        // --- sources -----------------------------------------------------

        private void LoadSavedSources()
        {
            try
            {
                foreach (ImportSourceDefinition definition in ImportSourceDefinitionsXml.Load(ConfigurationPaths.ImportSourcesFile))
                {
                    Sources.Add(new SourceVm(definition));
                }

                Selected = Sources.FirstOrDefault();
                if (Sources.Count > 0)
                {
                    Status = $"Loaded {Sources.Count} saved source(s). Build the preview to check them.";
                }
            }
            catch
            {
                // start empty
            }
        }

        public void SaveSources()
        {
            try
            {
                Directory.CreateDirectory(ConfigurationPaths.RootFolder);
                ImportSourceDefinitionsXml.Save(Sources.Select(s => s.ToDefinition()), ConfigurationPaths.ImportSourcesFile);
            }
            catch
            {
                // non-fatal
            }
        }

        private void AddFile()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Signal source (*.csv;*.tsv;*.xlsx;*.xls)|*.csv;*.tsv;*.xlsx;*.xls|All files (*.*)|*.*",
                Multiselect = true,
            };
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            foreach (string path in dialog.FileNames)
            {
                string ext = Path.GetExtension(path).ToLowerInvariant();
                SignalSourceKind kind = ext == ".csv" || ext == ".tsv" ? SignalSourceKind.DelimitedText : SignalSourceKind.Workbook;

                var definition = new ImportSourceDefinition(Path.GetFileName(path), kind, path) { IsMaster = Sources.Count == 0 };
                foreach (SignalField field in SignalField.All)
                {
                    definition.Fields.Add(new FieldBinding(field.Name, string.Empty, field.RequiredByDefault));
                }

                Sources.Add(new SourceVm(definition));
            }

            Selected = Sources.Last();
        }

        private void AddAccess()
        {
            var window = new AddAccessSourceWindow { Owner = System.Windows.Application.Current?.MainWindow };
            if (window.ShowDialog() == true && window.Result != null)
            {
                if (window.Result.IsMaster || Sources.Count == 0)
                {
                    foreach (SourceVm s in Sources)
                    {
                        s.IsMaster = false;
                    }

                    window.Result.IsMaster = true;
                }

                Sources.Add(new SourceVm(window.Result));
                Selected = Sources.Last();
            }
        }

        private void Remove()
        {
            if (Selected == null)
            {
                return;
            }

            int index = Sources.IndexOf(Selected);
            Sources.Remove(Selected);
            Selected = Sources.Count == 0 ? null : Sources[Math.Min(index, Sources.Count - 1)];
        }

        // --- preview ---------------------------------------------------

        private void BuildPreview()
        {
            Issues.Clear();
            PreviewRows.Clear();

            if (Sources.Count == 0)
            {
                Issues.Add("Add at least one source.");
                return;
            }

            var builder = new SignalSetBuilder();

            SignalTypeRegistry registry = File.Exists(ConfigurationPaths.SignalTypeRegistryFile)
                ? SignalTypeRegistryXml.Load(ConfigurationPaths.SignalTypeRegistryFile).Value
                : new SignalTypeRegistry();

            if (registry.Count > 0)
            {
                builder.WithRegistry(registry);
            }
            else if (!Sources.Any(s => s.Fields.Any(f => f.Field == SignalField.RawLow && f.Locator.Length > 0)))
            {
                Issues.Add("No signal-type registry — raw ranges will be blank, so raw-space tolerances cannot be calculated. " +
                           "Add your signal types in Setup → Signal Types, or map Raw Low / Raw High here.");
            }

            foreach (SourceVm source in Sources)
            {
                ImportSourceDefinition effective = source.ToEffectiveDefinition();
                try
                {
                    ISignalSource signalSource = effective.Kind == SignalSourceKind.Access
                        ? new AccessSignalSource(effective)
                        : FileSignalSource.Open(effective);
                    builder.Add(signalSource, effective);
                }
                catch (Exception ex)
                {
                    Issues.Add($"{source.Name}: {ex.Message}");
                    return;
                }
            }

            ConfigLoadResult<ResolvedSignalSet> result = builder.Build();
            _result = result.Value;

            foreach (ConfigIssue issue in result.Issues)
            {
                Issues.Add(issue.ToString());
            }

            foreach (FieldGap gap in _result.AllGaps)
            {
                Issues.Add("Dropped — " + gap);
            }

            int complete = _result.Complete.Count();
            int dropped = _result.Count - complete;
            Status = dropped == 0
                ? $"{complete} signal(s), all complete."
                : $"{complete} complete, {dropped} dropped (missing a required field — see Issues).";
            if (Issues.Count == 0)
            {
                Issues.Add(Status);
            }

            FillPreview();
            SaveSources();
        }

        private void FillPreview()
        {
            PreviewRows.Clear();
            var rows = HideIncomplete ? _result.Complete : _result.Signals.AsEnumerable();
            foreach (ResolvedSignal signal in rows.OrderBy(s => s.IsComplete ? 0 : 1))
            {
                SignalConfig c = signal.Config;
                PreviewRows.Add(new PreviewRowVm
                {
                    UniversalId = c.UniversalId,
                    SensorName = c.SensorName,
                    Conversion = c.ConversionSense.ToString(),
                    Scale = c.ScaleType,
                    Signal = c.SignalType,
                    Module = c.ModuleType,
                    RawLo = Num(c.RawLow),
                    RawHi = Num(c.RawHigh),
                    EuLo = Num(c.EuLow),
                    EuHi = Num(c.EuHigh),
                    EuLoSi = Num(c.EuLowSi),
                    EuHiSi = Num(c.EuHighSi),
                    Complete = signal.IsComplete,
                    Missing = signal.IsComplete ? string.Empty : string.Join(", ", signal.Gaps.Select(g => g.Field)),
                });
            }
        }

        private bool HasComplete()
        {
            if (_result.Complete.Any())
            {
                return true;
            }

            Issues.Add("Build the preview first (and make sure at least one signal is complete).");
            return false;
        }

        private void SaveForDatasheet()
        {
            if (!HasComplete())
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(ConfigurationPaths.RootFolder);
                SignalConfigSetXml.Save(_result.Complete.Select(s => s.Config), ConfigurationPaths.ResolvedSignalSetFile);
                SaveSources();
                Status = $"Saved {_result.Complete.Count()} signal(s) for datasheet use — Apply / Check will use these.";
            }
            catch (Exception ex)
            {
                Status = "Save failed: " + ex.Message;
            }
        }

        private void Export()
        {
            if (!HasComplete())
            {
                return;
            }

            var dialog = new SaveFileDialog { Filter = "Signal set (*.xml)|*.xml", FileName = "signal-set.xml" };
            if (dialog.ShowDialog() == true)
            {
                SignalConfigSetXml.Save(_result.Complete.Select(s => s.Config), dialog.FileName);
                Status = $"Exported {_result.Complete.Count()} signal(s) to {dialog.FileName}";
            }
        }

        private static string Num(double value) => value == 0 ? "—" : value.ToString("0.######");
    }
}
