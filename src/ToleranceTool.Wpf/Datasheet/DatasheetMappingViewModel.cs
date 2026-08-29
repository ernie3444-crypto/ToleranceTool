using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Microsoft.Win32;
using ToleranceTool.Configuration;
using ToleranceTool.Configuration.Aliases;
using ToleranceTool.Configuration.Datasheet;
using ToleranceTool.Configuration.Tolerances;
using ToleranceTool.Core.Precision;
using ToleranceTool.Core.Scales;
using ToleranceTool.Core.Signals;
using ToleranceTool.Excel.Datasheet;
using ToleranceTool.Import;
using ToleranceTool.Wpf.Mvvm;

namespace ToleranceTool.Wpf.Datasheet
{
    public sealed class HeaderMapRowVm : ObservableObject
    {
        private string? _selected;
        private readonly Action _onChanged;

        public HeaderMapRowVm(DatasheetParameter param, Action onChanged)
        {
            Param = param;
            _onChanged = onChanged;
        }

        public DatasheetParameter Param { get; }

        public string Label => Param.ToString();

        public string? Selected
        {
            get => _selected;
            set { if (Set(ref _selected, value)) _onChanged(); }
        }
    }

    public sealed class ReviewRowVm : ObservableObject
    {
        private string _override = string.Empty;
        private string _step = string.Empty;
        private string _signalText = string.Empty;
        private bool _resolved;
        private bool _autoMatched;
        private bool _excluded;
        private readonly Action<ReviewRowVm> _onOverrideChanged;
        private bool _quiet;

        public ReviewRowVm(Action<ReviewRowVm> onOverrideChanged) => _onOverrideChanged = onOverrideChanged;

        public int RowNumber { get; set; }
        public string SystemId { get; set; } = string.Empty;

        public string Step { get => _step; set => Set(ref _step, value); }
        public string SignalText { get => _signalText; set => Set(ref _signalText, value); }
        public bool Resolved { get => _resolved; set => Set(ref _resolved, value); }
        public bool AutoMatched { get => _autoMatched; set => Set(ref _autoMatched, value); }
        public bool Excluded { get => _excluded; set => Set(ref _excluded, value); }

        public string Override
        {
            get => _override;
            set { if (Set(ref _override, value) && !_quiet) _onOverrideChanged(this); }
        }

        /// <summary>Set the override without triggering the per-row recompute (used while (re)building the list).</summary>
        public void SetOverrideQuiet(string value)
        {
            _quiet = true;
            try { Override = value ?? string.Empty; }
            finally { _quiet = false; }
        }
    }

    public sealed class DatasheetMappingViewModel : ObservableObject
    {
        private readonly IDatasheet _sheet;
        private readonly Action<string>? _persist;

        private DatasheetMapping _mapping = new DatasheetMapping();
        private List<SignalConfig> _signals = new List<SignalConfig>();
        private ToleranceLibrary _tolerances = new ToleranceLibrary();
        private AliasTableSet _aliases = AliasTableSet.Empty();
        private readonly ScaleCurveLibrary _curves;

        private DatasheetOrientation _orientation = DatasheetOrientation.RowPerCase;
        private string _headerRow = "1";
        private string? _unitColumn;
        private UnitSystem _defaultUnitSystem = UnitSystem.English;
        private PrecisionMode _precisionMode = PrecisionMode.MatchExpected;
        private string _digits = "3";
        private RoundingMode _rounding = RoundingMode.HalfToEven;
        private string _multiplier = "1";
        private string _report = string.Empty;
        private string _status = string.Empty;
        private bool _refreshing;

        /// <summary>System ID → override (a Universal ID or the "(skip)" marker). The single source of truth,
        /// seeded from the saved mapping and edited via the review grid.</summary>
        private readonly Dictionary<string, string> _overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public DatasheetMappingViewModel(IDatasheet sheet, string? mappingXml = null, Action<string>? persist = null)
        {
            _sheet = sheet;
            _persist = persist;
            _curves = LoadCurves();

            SheetName = sheet.Name;

            Headers = new ObservableCollection<HeaderMapRowVm>(
                Enum.GetValues(typeof(DatasheetParameter)).Cast<DatasheetParameter>().Select(p => new HeaderMapRowVm(p, RefreshReview)));

            SaveMappingCommand = new RelayCommand(SaveMapping);
            RefreshReviewCommand = new RelayCommand(RefreshReview);
            LoadSignalSetCommand = new RelayCommand(LoadSignalSet);
            CheckCommand = new RelayCommand(() => Run(DatasheetRunMode.Check));
            ApplyCommand = new RelayCommand(() => Run(DatasheetRunMode.Apply));
            PassFailCommand = new RelayCommand(RunPassFail);

            LoadConfig();
            if (!string.IsNullOrWhiteSpace(mappingXml))
            {
                _mapping = DatasheetMappingXml.FromXml(mappingXml!).Value;
            }

            foreach (KeyValuePair<string, string> kv in _mapping.ResolutionOverrides)
            {
                _overrides[kv.Key] = kv.Value;
            }

            PopulateHeaders();
            ApplyMappingToControls();
            RefreshReview();
        }

        public string SheetName { get; }

        public ObservableCollection<string> AvailableHeaders { get; } = new ObservableCollection<string>();
        public ObservableCollection<HeaderMapRowVm> Headers { get; }
        public ObservableCollection<ReviewRowVm> ReviewRows { get; } = new ObservableCollection<ReviewRowVm>();
        public ObservableCollection<string> OverrideOptions { get; } = new ObservableCollection<string>();

        public DatasheetOrientation[] Orientations { get; } = { DatasheetOrientation.RowPerCase, DatasheetOrientation.ColumnPerCase };
        public UnitSystem[] UnitSystems { get; } = { UnitSystem.English, UnitSystem.Si };
        public PrecisionMode[] PrecisionModes { get; } = { PrecisionMode.MatchExpected, PrecisionMode.DecimalPlaces, PrecisionMode.SignificantFigures };
        public RoundingMode[] RoundingModes { get; } = { RoundingMode.HalfToEven, RoundingMode.HalfUp };
        public string[] Multipliers { get; } = { "1", "1.5", "2", "3", "4" };

        public DatasheetOrientation Orientation { get => _orientation; set { if (Set(ref _orientation, value)) { PopulateHeaders(); RefreshReview(); } } }
        public string HeaderRow { get => _headerRow; set { if (Set(ref _headerRow, value)) PopulateHeaders(); } }
        public string? UnitColumn { get => _unitColumn; set => Set(ref _unitColumn, value); }
        public UnitSystem DefaultUnitSystem { get => _defaultUnitSystem; set => Set(ref _defaultUnitSystem, value); }
        public PrecisionMode PrecisionMode { get => _precisionMode; set => Set(ref _precisionMode, value); }
        public string Digits { get => _digits; set => Set(ref _digits, value); }
        public RoundingMode Rounding { get => _rounding; set => Set(ref _rounding, value); }
        public string Multiplier { get => _multiplier; set => Set(ref _multiplier, value); }
        public string Report { get => _report; private set => Set(ref _report, value); }
        public string Status { get => _status; private set => Set(ref _status, value); }

        public ICommand SaveMappingCommand { get; }
        public ICommand RefreshReviewCommand { get; }
        public ICommand LoadSignalSetCommand { get; }
        public ICommand CheckCommand { get; }
        public ICommand ApplyCommand { get; }
        public ICommand PassFailCommand { get; }

        // --- config ------------------------------------------------------

        private ScaleCurveLibrary LoadCurves()
        {
            try
            {
                if (File.Exists(ConfigurationPaths.ScaleTypeLibraryFile))
                {
                    var result = Configuration.Scales.ScaleTypeLibraryXml.Load(ConfigurationPaths.ScaleTypeLibraryFile);
                    if (!result.HasErrors && result.Value.Count > 0)
                    {
                        return ScaleCurveLibrary.From(result.Value);
                    }
                }
            }
            catch
            {
                // fall through
            }

            return ScaleCurveLibrary.CreateDefault();
        }

        private void LoadConfig()
        {
            if (File.Exists(ConfigurationPaths.ToleranceLibraryFile))
            {
                _tolerances = ToleranceLibraryXml.Load(ConfigurationPaths.ToleranceLibraryFile).Value;
            }

            if (File.Exists(ConfigurationPaths.AliasTablesFile))
            {
                _aliases = AliasTablesXml.Load(ConfigurationPaths.AliasTablesFile).Value;
            }

            if (File.Exists(ConfigurationPaths.ResolvedSignalSetFile))
            {
                _signals = SignalConfigSetXml.Load(ConfigurationPaths.ResolvedSignalSetFile).Value;
            }

            Status = _signals.Count == 0
                ? "No signal set imported yet — use Signal Configuration, then 'Save for datasheet use'."
                : $"{_signals.Count} signal(s), {_tolerances.Count} tolerance(s), {_aliases.Tables.Count} alias table(s) loaded.";
        }

        private void LoadSignalSet()
        {
            var dialog = new OpenFileDialog { Filter = "Signal set (*.xml)|*.xml|All files (*.*)|*.*" };
            if (dialog.ShowDialog() == true)
            {
                _signals = SignalConfigSetXml.Load(dialog.FileName).Value;
                Status = $"Loaded {_signals.Count} signal(s) from {dialog.FileName}";
                RefreshReview();
            }
        }

        // --- mapping <-> controls -------------------------------------

        private int HeaderRowIndex() =>
            int.TryParse(_headerRow?.Trim(), out int oneBased) && oneBased >= 1 ? oneBased - 1 : 0;

        private IDatasheet Effective() =>
            _orientation == DatasheetOrientation.ColumnPerCase ? new TransposedDatasheet(_sheet) : _sheet;

        private string?[] SafeRow(int index)
        {
            try
            {
                return Effective().Row(index);
            }
            catch
            {
                return Array.Empty<string?>();
            }
        }

        private void PopulateHeaders()
        {
            string?[] headers = SafeRow(HeaderRowIndex());
            var options = headers
                .Select(h => h?.Trim())
                .Where(h => !string.IsNullOrEmpty(h))
                .Cast<string>()
                .Distinct()
                .ToList();

            AvailableHeaders.Clear();
            AvailableHeaders.Add(string.Empty);
            foreach (string option in options)
            {
                AvailableHeaders.Add(option);
            }
        }

        private void ApplyMappingToControls()
        {
            Orientation = _mapping.Orientation;
            HeaderRow = (_mapping.HeaderRowIndex + 1).ToString();
            foreach (HeaderMapRowVm row in Headers)
            {
                row.Selected = _mapping.Header(row.Param) ?? string.Empty;
            }

            UnitColumn = _mapping.UnitColumnHeader ?? string.Empty;
            DefaultUnitSystem = _mapping.DefaultUnitSystem;
            PrecisionMode = _mapping.Precision.Mode;
            Digits = _mapping.Precision.Digits.ToString();
            Rounding = _mapping.Precision.Rounding;
            Multiplier = _mapping.ToleranceMultiplier.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private DatasheetMapping ReadMapping()
        {
            var mapping = new DatasheetMapping
            {
                Orientation = _orientation,
                HeaderRowIndex = HeaderRowIndex(),
                DefaultUnitSystem = _defaultUnitSystem,
                UnitColumnHeader = string.IsNullOrWhiteSpace(_unitColumn) ? null : _unitColumn!.Trim(),
                Precision = new PrecisionPolicy
                {
                    Mode = _precisionMode,
                    Rounding = _rounding,
                    Digits = int.TryParse(_digits?.Trim(), out int d) ? d : 3,
                },
                ToleranceMultiplier = double.TryParse(_multiplier?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double m) && m > 0 ? m : 1.0,
            };

            foreach (HeaderMapRowVm row in Headers)
            {
                if (!string.IsNullOrWhiteSpace(row.Selected))
                {
                    mapping.Headers[row.Param] = row.Selected!.Trim();
                }
            }

            foreach (KeyValuePair<string, string> kv in _overrides)
            {
                if (kv.Key.Length > 0 && !string.IsNullOrEmpty(kv.Value))
                {
                    mapping.ResolutionOverrides[kv.Key] = kv.Value;
                }
            }

            return mapping;
        }

        private void SaveMapping()
        {
            _mapping = ReadMapping();
            string xml = DatasheetMappingXml.ToXml(_mapping);
            try
            {
                _persist?.Invoke(xml);
                Directory.CreateDirectory(ConfigurationPaths.SheetsFolder);
                File.WriteAllText(ConfigurationPaths.SheetMappingFile(_sheet.Name), xml);
                Status = $"Mapping saved for \"{_sheet.Name}\".";
            }
            catch (Exception ex)
            {
                Status = "Save failed: " + ex.Message;
            }
        }

        // --- review + run -------------------------------------------

        private SignalResolver BuildResolver(DatasheetMapping mapping) =>
            new SignalResolver(_signals, _aliases, mapping.ResolutionOverrides);

        private void RefreshReview()
        {
            if (_refreshing)
            {
                return;
            }

            _refreshing = true;
            try
            {
                RefreshReviewCore();
            }
            finally
            {
                _refreshing = false;
            }
        }

        private void RefreshReviewCore()
        {
            DatasheetMapping mapping = ReadMapping();
            ReviewRows.Clear();

            string? systemIdHeader = mapping.Header(DatasheetParameter.SystemId);
            if (string.IsNullOrWhiteSpace(systemIdHeader))
            {
                Status = "Map the System ID header to see the resolution review.";
                return;
            }

            IDatasheet sheet = Effective();
            int headerRow = mapping.HeaderRowIndex;
            string?[] headers = SafeRow(headerRow);
            int systemIdColumn = Array.FindIndex(headers, h => string.Equals(h?.Trim(), systemIdHeader!.Trim(), StringComparison.OrdinalIgnoreCase));
            if (systemIdColumn < 0)
            {
                Status = $"No column has the header \"{systemIdHeader}\".";
                return;
            }

            SignalResolver resolver = BuildResolver(mapping);

            OverrideOptions.Clear();
            OverrideOptions.Add(string.Empty);
            OverrideOptions.Add(SignalResolver.ExcludeMarker);
            foreach (string id in _signals.Select(s => s.UniversalId).Where(x => x.Length > 0).Distinct().OrderBy(x => x))
            {
                OverrideOptions.Add(id);
            }

            int last = mapping.LastDataRowIndex ?? sheet.LastRowIndex;
            for (int row = headerRow + 1; row <= last; row++)
            {
                string? systemId = sheet.GetText(row, systemIdColumn)?.Trim();
                if (string.IsNullOrEmpty(systemId))
                {
                    continue;
                }

                var vm = new ReviewRowVm(OnOverrideChanged)
                {
                    RowNumber = row + 1,
                    SystemId = systemId!,
                };

                if (_overrides.TryGetValue(systemId!, out string existing))
                {
                    vm.SetOverrideQuiet(existing);
                }

                ApplyResolution(vm, resolver);
                ReviewRows.Add(vm);
            }

            Status = $"{ReviewRows.Count} data row(s) reviewed.";
        }

        /// <summary>Recompute one row's resolution in place — no list churn, so a grid edit stays put.</summary>
        private void OnOverrideChanged(ReviewRowVm row)
        {
            if (row.SystemId.Length == 0)
            {
                return;
            }

            if (string.IsNullOrEmpty(row.Override))
            {
                _overrides.Remove(row.SystemId);
            }
            else
            {
                _overrides[row.SystemId] = row.Override;
            }

            ApplyResolution(row, BuildResolver(ReadMapping()));
        }

        private static void ApplyResolution(ReviewRowVm row, SignalResolver resolver)
        {
            SignalResolution resolution = resolver.Resolve(row.SystemId);
            row.Step = resolution.Step.ToString();
            row.SignalText = resolution.Step switch
            {
                ResolutionStep.Excluded => "— excluded —",
                ResolutionStep.Ambiguous => "ambiguous: " + string.Join(", ", resolution.Candidates),
                _ => resolution.IsResolved
                    ? $"{resolution.Signal!.SensorName}  ({resolution.Signal.SignalType} / {resolution.Signal.ModuleType})"
                    : "unresolved",
            };
            row.Resolved = resolution.IsResolved;
            row.AutoMatched = resolution.Step == ResolutionStep.AutoMatch;
            row.Excluded = resolution.Step == ResolutionStep.Excluded;
        }

        private void Run(DatasheetRunMode mode)
        {
            DatasheetMapping mapping = ReadMapping();
            _mapping = mapping;

            var runner = new DatasheetRunner(BuildResolver(mapping), _tolerances, _curves);
            DatasheetRunResult result = runner.Run(_sheet, mapping, mode);

            Report = FormatReport(result);
            Status = result.Summary();
            RefreshReview();
        }

        private void RunPassFail()
        {
            DatasheetMapping mapping = ReadMapping();
            var runner = new DatasheetRunner(BuildResolver(mapping), _tolerances, _curves);
            DatasheetRunResult result = runner.RunPassFail(_sheet, mapping);
            Report = FormatReport(result);
            Status = result.Summary();
        }

        private static string FormatReport(DatasheetRunResult result)
        {
            var lines = new List<string> { result.Summary() };
            foreach (string warning in result.Warnings)
            {
                lines.Add("  ! " + warning);
            }

            lines.Add(string.Empty);
            if (!result.DidRun)
            {
                return string.Join(Environment.NewLine, lines);
            }

            bool multi = result.TestPointsPerRow > 1;
            foreach (RowOutcome row in result.Rows)
            {
                string calc = row.Calculated.HasValue ? row.Calculated.Value.ToString("0.######") : "—";
                string where = multi ? $"row {row.RowIndex + 1}.{row.TestPoint}" : $"row {row.RowIndex + 1}";
                lines.Add($"  {where,-9} {row.SystemId,-22} {row.Status,-14} {calc,12}   {row.Note}");
            }

            return string.Join(Environment.NewLine, lines);
        }
    }
}
