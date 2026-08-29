using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Input;
using Microsoft.Win32;
using ToleranceTool.Configuration;
using ToleranceTool.Configuration.Tolerances;
using ToleranceTool.Core.Scales;
using ToleranceTool.Core.Signals;
using ToleranceTool.Core.Tolerances;
using ToleranceTool.Wpf.Mvvm;

namespace ToleranceTool.Wpf.Tolerances
{
    public sealed class TermDisplayVm
    {
        public TermDisplayVm(ToleranceTerm term)
        {
            Term = term;
            Text = ToleranceTermText.Describe(term);
        }

        public ToleranceTerm Term { get; }
        public string Text { get; }
    }

    public sealed class ToleranceDefVm : ObservableObject
    {
        public ToleranceDefVm(ToleranceDefinition definition) => Definition = definition;

        public ToleranceDefinition Definition { get; }

        public string SignalType => Definition.SignalType;
        public string ModuleType => Definition.ModuleType;
        public string Band => ToleranceTermText.DescribeDefinition(Definition);

        public void RaiseBand()
        {
            Raise(nameof(Band));
        }
    }

    public sealed class ToleranceEditorViewModel : ObservableObject
    {
        private readonly ScaleCurveLibrary _curves = ScaleCurveLibrary.CreateDefault();
        private readonly ToleranceEngine _engine;

        private ToleranceLibrary _library = new ToleranceLibrary();
        private string _path;

        private ToleranceDefVm? _selected;
        private TermDisplayVm? _selectedTerm;
        private string _previewText = "Select a definition to preview its band.";
        private string _status = string.Empty;

        private string _rawLow = "4", _rawHigh = "20", _euLow = "0", _euHigh = "100";
        private string _euLowSi = "0", _euHighSi = "100", _expected = "50";
        private string _scaleType = ScaleTypeNames.Linear;
        private ConversionSense _sense = ConversionSense.Direct;
        private UnitSystem _unitSystem = UnitSystem.English;

        public ToleranceEditorViewModel(string? path = null)
        {
            _engine = new ToleranceEngine(_curves);
            _path = path ?? ConfigurationPaths.ToleranceLibraryFile;

            AddDefinitionCommand = new RelayCommand(() => { });   // wired in the window
            DeleteDefinitionCommand = new RelayCommand(DeleteDefinition, () => Selected != null);
            RemoveTermCommand = new RelayCommand(RemoveTerm, () => SelectedTerm != null);
            LoadCommand = new RelayCommand(LoadFromDialog);
            SaveCommand = new RelayCommand(() => Save(_path));
            SaveAsCommand = new RelayCommand(SaveAs);
            RecalculateCommand = new RelayCommand(RefreshPreview);

            if (File.Exists(_path))
            {
                Load(_path);
            }
        }

        public ObservableCollection<ToleranceDefVm> Definitions { get; } = new ObservableCollection<ToleranceDefVm>();
        public ObservableCollection<TermDisplayVm> Terms { get; } = new ObservableCollection<TermDisplayVm>();
        public ObservableCollection<string> Validation { get; } = new ObservableCollection<string>();

        public ToleranceDefVm? Selected
        {
            get => _selected;
            set { if (Set(ref _selected, value)) { RefreshTerms(); RefreshPreview(); } }
        }

        public TermDisplayVm? SelectedTerm { get => _selectedTerm; set => Set(ref _selectedTerm, value); }

        public string PreviewText { get => _previewText; private set => Set(ref _previewText, value); }
        public string Status { get => _status; private set => Set(ref _status, value); }

        public string[] ScaleTypes { get; } = { ScaleTypeNames.Linear, ScaleTypeNames.SquareRoot, ScaleTypeNames.Logarithmic };
        public ConversionSense[] Senses { get; } = { ConversionSense.Direct, ConversionSense.Reverse };
        public UnitSystem[] UnitSystems { get; } = { UnitSystem.English, UnitSystem.Si };

        public string RawLow { get => _rawLow; set { if (Set(ref _rawLow, value)) RefreshPreview(); } }
        public string RawHigh { get => _rawHigh; set { if (Set(ref _rawHigh, value)) RefreshPreview(); } }
        public string EuLow { get => _euLow; set { if (Set(ref _euLow, value)) RefreshPreview(); } }
        public string EuHigh { get => _euHigh; set { if (Set(ref _euHigh, value)) RefreshPreview(); } }
        public string EuLowSi { get => _euLowSi; set { if (Set(ref _euLowSi, value)) RefreshPreview(); } }
        public string EuHighSi { get => _euHighSi; set { if (Set(ref _euHighSi, value)) RefreshPreview(); } }
        public string Expected { get => _expected; set { if (Set(ref _expected, value)) RefreshPreview(); } }
        public string ScaleType { get => _scaleType; set { if (Set(ref _scaleType, value)) RefreshPreview(); } }
        public ConversionSense Sense { get => _sense; set { if (Set(ref _sense, value)) RefreshPreview(); } }
        public UnitSystem UnitSystem { get => _unitSystem; set { if (Set(ref _unitSystem, value)) RefreshPreview(); } }

        public ICommand AddDefinitionCommand { get; set; }
        public ICommand DeleteDefinitionCommand { get; }
        public ICommand RemoveTermCommand { get; }
        public ICommand LoadCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand SaveAsCommand { get; }
        public ICommand RecalculateCommand { get; }

        // --- library --------------------------------------------------

        private void Load(string path)
        {
            ConfigLoadResult<ToleranceLibrary> result = ToleranceLibraryXml.Load(path);
            _library = result.Value;
            _path = path;

            Definitions.Clear();
            foreach (ToleranceDefinition definition in _library.Definitions)
            {
                Definitions.Add(new ToleranceDefVm(definition));
            }

            Selected = Definitions.FirstOrDefault();
            Status = result.Issues.Count == 0
                ? $"Loaded {_library.Count} definition(s) — {path}"
                : $"Loaded with {result.Issues.Count} issue(s) — {result.Issues[0].Message}";
            RefreshValidation();
        }

        private void LoadFromDialog()
        {
            var dialog = new OpenFileDialog { Filter = "Tolerance library (*.xml)|*.xml|All files (*.*)|*.*" };
            if (dialog.ShowDialog() == true)
            {
                Load(dialog.FileName);
            }
        }

        private void Save(string path)
        {
            try
            {
                ToleranceLibraryXml.Save(_library, path);
                _path = path;
                Status = $"Saved {_library.Count} definition(s) — {path}";
            }
            catch (Exception ex)
            {
                Status = "Save failed: " + ex.Message;
            }
        }

        private void SaveAs()
        {
            var dialog = new SaveFileDialog { Filter = "Tolerance library (*.xml)|*.xml", FileName = Path.GetFileName(_path) };
            if (dialog.ShowDialog() == true)
            {
                Save(dialog.FileName);
            }
        }

        public string? AddDefinition(string signalType, string moduleType)
        {
            var definition = new ToleranceDefinition { SignalType = signalType, ModuleType = moduleType };
            try
            {
                _library.Add(definition);
            }
            catch (InvalidOperationException ex)
            {
                return ex.Message;
            }

            var vm = new ToleranceDefVm(definition);
            Definitions.Add(vm);
            Selected = vm;
            RefreshValidation();
            return null;
        }

        private void DeleteDefinition()
        {
            if (Selected == null)
            {
                return;
            }

            _library.Remove(Selected.Definition);
            int index = Definitions.IndexOf(Selected);
            Definitions.Remove(Selected);
            Selected = Definitions.Count == 0 ? null : Definitions[Math.Min(index, Definitions.Count - 1)];
            RefreshValidation();
        }

        // --- terms ---------------------------------------------------

        public void AddTerm(ToleranceTerm term)
        {
            if (Selected == null)
            {
                return;
            }

            Selected.Definition.Terms.Add(term);
            AfterTermChange();
        }

        public void ReplaceSelectedTerm(ToleranceTerm term)
        {
            if (Selected == null || SelectedTerm == null)
            {
                return;
            }

            int index = Selected.Definition.Terms.IndexOf(SelectedTerm.Term);
            if (index >= 0)
            {
                Selected.Definition.Terms[index] = term;
                AfterTermChange();
            }
        }

        private void RemoveTerm()
        {
            if (Selected == null || SelectedTerm == null)
            {
                return;
            }

            Selected.Definition.Terms.Remove(SelectedTerm.Term);
            AfterTermChange();
        }

        private void AfterTermChange()
        {
            RefreshTerms();
            Selected?.RaiseBand();
            RefreshValidation();
            RefreshPreview();
        }

        private void RefreshTerms()
        {
            Terms.Clear();
            if (Selected != null)
            {
                foreach (ToleranceTerm term in Selected.Definition.Terms)
                {
                    Terms.Add(new TermDisplayVm(term));
                }
            }
        }

        private void RefreshValidation()
        {
            Validation.Clear();
            foreach (string key in _library.DuplicateKeys())
            {
                Validation.Add($"Error [{key}]: defined more than once.");
            }

            foreach (ToleranceDefinition definition in _library.Definitions)
            {
                if (definition.Terms.Count == 0)
                {
                    Validation.Add($"Error [{ToleranceLibrary.KeyOf(definition)}]: no terms.");
                }
            }

            foreach (ConfigIssue issue in ToleranceLibraryValidator.Validate(_library))
            {
                Validation.Add(issue.ToString());
            }

            if (Validation.Count == 0)
            {
                Validation.Add("No problems found.");
            }
        }

        // --- preview -----------------------------------------------

        private void RefreshPreview()
        {
            try
            {
                if (Selected == null)
                {
                    PreviewText = _library.Count == 0
                        ? "No tolerances yet — add one, then this shows its resolved band."
                        : "Select a definition on the left to preview its band.";
                    return;
                }

                ToleranceDefinition definition = Selected.Definition;
                if (definition.Terms.Count == 0)
                {
                    PreviewText = $"\"{ToleranceLibrary.KeyOf(definition)}\" has no terms yet. Add a term to see the band.";
                    return;
                }

                if (!TryReadSignal(out SignalConfig signal, out string error))
                {
                    PreviewText = "Preview inputs: " + error;
                    return;
                }

                if (!double.TryParse(_expected?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double expected))
                {
                    PreviewText = "Preview inputs: the expected value must be a number.";
                    return;
                }

                ToleranceResult result = _engine.Calculate(expected, _unitSystem, signal, definition);
                PreviewText = Describe(result, expected, _unitSystem);
            }
            catch (Exception ex)
            {
                PreviewText = "Preview error: " + ex.Message;
            }
        }

        private bool TryReadSignal(out SignalConfig signal, out string error)
        {
            var s = new SignalConfig();
            signal = s;

            var fields = new (string Text, string Name, Action<double> Set)[]
            {
                (_rawLow, "raw low", v => s.RawLow = v),
                (_rawHigh, "raw high", v => s.RawHigh = v),
                (_euLow, "EU low", v => s.EuLow = v),
                (_euHigh, "EU high", v => s.EuHigh = v),
                (_euLowSi, "EU low (SI)", v => s.EuLowSi = v),
                (_euHighSi, "EU high (SI)", v => s.EuHighSi = v),
            };

            foreach ((string text, string name, Action<double> set) in fields)
            {
                if (!double.TryParse(text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                {
                    error = $"{name} must be a number.";
                    return false;
                }

                set(value);
            }

            if (Selected != null && !Selected.Definition.IsEuOnly && s.RawLow == s.RawHigh)
            {
                error = "raw low and raw high must differ — this band applies in raw units.";
                return false;
            }

            s.SignalType = Selected?.SignalType ?? string.Empty;
            s.ModuleType = Selected?.ModuleType ?? string.Empty;
            s.ScaleType = _scaleType;
            s.ConversionSense = _sense;
            error = string.Empty;
            return true;
        }

        private static string Describe(ToleranceResult result, double expected, UnitSystem unitSystem)
        {
            var sb = new StringBuilder();
            if (result.Outcome != ToleranceOutcome.Calculated)
            {
                sb.AppendLine($"Not calculated: {result.Outcome}");
                sb.AppendLine(result.Message);
                if (result.RawExpected != 0)
                {
                    sb.AppendLine($"rawExpected = {result.RawExpected:0.######}");
                }

                return sb.ToString();
            }

            sb.AppendLine(result.UsedEuFastPath ? "Path A — EU fast path" : "Path B — raw round-trip");
            sb.AppendLine();
            foreach (ResolvedTerm term in result.Terms)
            {
                string space = term.Space == ToleranceSpace.Eu ? "EU " : "raw";
                sb.AppendLine($"  {space}  {term.Magnitude,14:0.########}   {ToleranceTermText.Describe(term.Source)}");
            }

            sb.AppendLine();
            if (!result.UsedEuFastPath)
            {
                sb.AppendLine($"  rawExpected     {result.RawExpected,14:0.########}");
                sb.AppendLine($"  raw band ±      {result.RawTolerance,14:0.########}");
                sb.AppendLine($"  EU +            {result.EuPlus,14:0.########}");
                sb.AppendLine($"  EU -            {result.EuMinus,14:0.########}");
                if (result.Extrapolated)
                {
                    sb.AppendLine("  (extrapolated past the raw range)");
                }
            }

            sb.AppendLine();
            sb.AppendLine($"  tolerance ±  =  {result.Tolerance:0.########}  ({unitSystem} EU)");
            sb.AppendLine($"  {expected - result.Tolerance:0.######}  …  {expected + result.Tolerance:0.######}");
            return sb.ToString();
        }
    }
}
