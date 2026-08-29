using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ToleranceTool.Import;
using ToleranceTool.Wpf.Mvvm;

namespace ToleranceTool.Wpf.Import
{
    public sealed class FieldBindingRowVm : ObservableObject
    {
        private string _locator = string.Empty;
        private bool _required;

        public FieldBindingRowVm(string field, string locator, bool required)
        {
            Field = field;
            _locator = locator;
            _required = required;
        }

        public string Field { get; }

        public string Locator { get => _locator; set => Set(ref _locator, value); }

        public bool Required { get => _required; set => Set(ref _required, value); }
    }

    /// <summary>Editable view of one <see cref="ImportSourceDefinition"/>.</summary>
    public sealed class SourceVm : ObservableObject
    {
        private readonly Dictionary<string, FieldBindingRowVm> _all = new Dictionary<string, FieldBindingRowVm>();

        private bool _isMaster;
        private SignalDataOrientation _layout;
        private string _sheetName = string.Empty;
        private string _headerRow = "1";
        private string _universalId = "A";
        private string _parameterName = string.Empty;
        private string _parameterValue = string.Empty;
        private string _parameterMetric = string.Empty;

        public SourceVm(ImportSourceDefinition definition)
        {
            Name = definition.Name;
            Kind = definition.Kind;
            Location = definition.Location;
            Query = definition.Query;
            _isMaster = definition.IsMaster;
            _layout = definition.Orientation;
            _sheetName = definition.SheetName ?? string.Empty;
            _headerRow = definition.HeaderRowIndex.HasValue ? (definition.HeaderRowIndex.Value + 1).ToString() : string.Empty;
            _universalId = definition.UniversalIdLocator;
            _parameterName = definition.ParameterNameLocator ?? string.Empty;
            _parameterValue = definition.ParameterValueLocator ?? string.Empty;
            _parameterMetric = definition.ParameterMetricLocator ?? string.Empty;

            foreach (SignalField field in SignalField.All)
            {
                FieldBinding? binding = definition.Binding(field.Name);
                _all[field.Name] = new FieldBindingRowVm(field.Name, binding?.Locator ?? string.Empty, binding?.Required ?? field.RequiredByDefault);
            }

            Fields = new ObservableCollection<FieldBindingRowVm>();
            RebuildFields();
        }

        public string Name { get; }
        public SignalSourceKind Kind { get; }
        public string Location { get; }
        public string? Query { get; }

        public bool IsMaster { get => _isMaster; set { if (Set(ref _isMaster, value)) RebuildFields(); } }

        public SignalDataOrientation Layout
        {
            get => _layout;
            set
            {
                if (Set(ref _layout, value))
                {
                    Raise(nameof(IsEav));
                    Raise(nameof(LocatorHeader));
                    RebuildFields();
                }
            }
        }

        public bool IsEav => _layout == SignalDataOrientation.ParameterPerRow;

        public bool IsWorkbook => Kind == SignalSourceKind.Workbook;

        public bool IsFile => Kind != SignalSourceKind.Access;

        public string LocatorHeader => IsEav ? "Parameter name" : "Column / number";

        public string SheetName { get => _sheetName; set => Set(ref _sheetName, value); }
        public string HeaderRow { get => _headerRow; set => Set(ref _headerRow, value); }
        public string UniversalId { get => _universalId; set => Set(ref _universalId, value); }
        public string ParameterName { get => _parameterName; set => Set(ref _parameterName, value); }
        public string ParameterValue { get => _parameterValue; set => Set(ref _parameterValue, value); }
        public string ParameterMetric { get => _parameterMetric; set => Set(ref _parameterMetric, value); }

        public ObservableCollection<FieldBindingRowVm> Fields { get; }

        public string Display => _isMaster ? $"{Name}   [master]" : Name;

        private void RebuildFields()
        {
            Fields.Clear();
            foreach (SignalField field in SignalField.All)
            {
                if (field.MasterOnly && !_isMaster)
                {
                    continue;
                }

                if (IsEav && (field.Name == SignalField.EuLowSi || field.Name == SignalField.EuHighSi))
                {
                    continue;
                }

                Fields.Add(_all[field.Name]);
            }

            Raise(nameof(Display));
        }

        public ImportSourceDefinition ToDefinition()
        {
            var definition = new ImportSourceDefinition(Name, Kind, Location)
            {
                IsMaster = _isMaster,
                Orientation = _layout,
                SheetName = string.IsNullOrWhiteSpace(_sheetName) ? null : _sheetName.Trim(),
                HeaderRowIndex = int.TryParse(_headerRow?.Trim(), out int h) && h >= 1 ? h - 1 : (int?)null,
                UniversalIdLocator = _universalId?.Trim() ?? string.Empty,
                Query = Query,
                ParameterNameLocator = Blank(_parameterName),
                ParameterValueLocator = Blank(_parameterValue),
                ParameterMetricLocator = Blank(_parameterMetric),
            };

            foreach (FieldBindingRowVm row in _all.Values)
            {
                definition.Fields.Add(new FieldBinding(row.Field, row.Locator?.Trim() ?? string.Empty, row.Required));
            }

            return definition;
        }

        /// <summary>The definition with only the bound fields, for the preview build.</summary>
        public ImportSourceDefinition ToEffectiveDefinition()
        {
            ImportSourceDefinition full = ToDefinition();
            var effective = new ImportSourceDefinition(full.Name, full.Kind, full.Location)
            {
                IsMaster = full.IsMaster,
                Orientation = full.Orientation,
                SheetName = full.SheetName,
                HeaderRowIndex = full.HeaderRowIndex,
                UniversalIdLocator = full.UniversalIdLocator,
                Query = full.Query,
                ParameterNameLocator = full.ParameterNameLocator,
                ParameterValueLocator = full.ParameterValueLocator,
                ParameterMetricLocator = full.ParameterMetricLocator,
            };
            effective.Fields.AddRange(full.Fields.Where(b => !string.IsNullOrWhiteSpace(b.Locator)));
            return effective;
        }

        private static string? Blank(string? value)
        {
            string trimmed = value?.Trim() ?? string.Empty;
            return trimmed.Length == 0 ? null : trimmed;
        }
    }
}
