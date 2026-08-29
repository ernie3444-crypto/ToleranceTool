using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using ToleranceTool.Configuration;
using ToleranceTool.Configuration.Tolerances;
using ToleranceTool.Core.Signals;
using ToleranceTool.Core.Tolerances;
using ToleranceTool.Wpf.Mvvm;

namespace ToleranceTool.Wpf.Tolerances
{
    public partial class ToleranceTermWindow : Window
    {
        private readonly TermVm _vm;

        public ToleranceTermWindow(ToleranceTerm? existing)
        {
            InitializeComponent();
            _vm = new TermVm(existing);
            DataContext = _vm;
        }

        public ToleranceTerm? Result { get; private set; }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (!_vm.TryBuild(out ToleranceTerm term, out string error))
            {
                MessageBox.Show(this, error, "Tolerance term", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Result = term;
            DialogResult = true;
        }
    }

    public sealed class TermVm : ObservableObject
    {
        private ToleranceTermKind _kind = ToleranceTermKind.Percent;
        private string _value = "0.003";
        private PercentBasis _basis = PercentBasis.RawSpan;
        private ToleranceSpace _space = ToleranceSpace.Raw;
        private string _unit = string.Empty;
        private UnitSystem _unitSystem = UnitSystem.English;
        private string _expression = string.Empty;

        public TermVm(ToleranceTerm? existing)
        {
            if (existing != null)
            {
                _kind = existing.Kind;
                _value = existing.Value.ToString("R", CultureInfo.InvariantCulture);
                _basis = existing.PercentBasis;
                _space = existing.Space;
                _unit = existing.Unit;
                _unitSystem = existing.UnitSystem;
                _expression = existing.ExpressionBody;
            }
        }

        public ToleranceTermKind[] Kinds { get; } = Enum.GetValues(typeof(ToleranceTermKind)).Cast<ToleranceTermKind>().ToArray();
        public PercentBasis[] BasisOptions { get; } = Enum.GetValues(typeof(PercentBasis)).Cast<PercentBasis>().ToArray();
        public ToleranceSpace[] SpaceOptions { get; } = Enum.GetValues(typeof(ToleranceSpace)).Cast<ToleranceSpace>().ToArray();
        public UnitSystem[] UnitSystems { get; } = { UnitSystem.English, UnitSystem.Si };

        public ToleranceTermKind Kind { get => _kind; set { if (Set(ref _kind, value)) RaiseVisibility(); } }
        public string Value { get => _value; set => Set(ref _value, value); }
        public PercentBasis Basis { get => _basis; set => Set(ref _basis, value); }
        public ToleranceSpace Space { get => _space; set => Set(ref _space, value); }
        public string Unit { get => _unit; set => Set(ref _unit, value); }
        public UnitSystem UnitSystem { get => _unitSystem; set => Set(ref _unitSystem, value); }
        public string Expression { get => _expression; set => Set(ref _expression, value); }

        public bool ShowValue => _kind != ToleranceTermKind.Expression;
        public bool ShowBasis => _kind == ToleranceTermKind.Percent;
        public bool ShowSpace => _kind == ToleranceTermKind.Percent || _kind == ToleranceTermKind.Expression;
        public bool ShowUnit => _kind == ToleranceTermKind.AbsoluteEu || _kind == ToleranceTermKind.AbsoluteRaw;
        public bool ShowUnitSystem => _kind == ToleranceTermKind.AbsoluteEu;
        public bool ShowExpression => _kind == ToleranceTermKind.Expression;

        public string HintText => _kind == ToleranceTermKind.Expression
            ? "Variables: " + string.Join(", ", ToleranceExpressionVariables.All)
            : _kind == ToleranceTermKind.Percent
                ? "Value is a fraction: 0.3% is 0.003."
                : string.Empty;

        private void RaiseVisibility()
        {
            Raise(nameof(ShowValue));
            Raise(nameof(ShowBasis));
            Raise(nameof(ShowSpace));
            Raise(nameof(ShowUnit));
            Raise(nameof(ShowUnitSystem));
            Raise(nameof(ShowExpression));
            Raise(nameof(HintText));
        }

        public bool TryBuild(out ToleranceTerm term, out string error)
        {
            term = new ToleranceTerm { Kind = _kind };
            error = string.Empty;

            if (_kind != ToleranceTermKind.Expression)
            {
                if (!double.TryParse(_value?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                {
                    error = "Value must be a number.";
                    return false;
                }

                term.Value = value;
            }

            switch (_kind)
            {
                case ToleranceTermKind.Percent:
                    term.PercentBasis = _basis;
                    term.Space = _space;
                    break;
                case ToleranceTermKind.AbsoluteEu:
                    term.Unit = _unit?.Trim() ?? string.Empty;
                    term.UnitSystem = _unitSystem;
                    break;
                case ToleranceTermKind.AbsoluteRaw:
                    term.Unit = _unit?.Trim() ?? string.Empty;
                    break;
                case ToleranceTermKind.Expression:
                    term.ExpressionBody = _expression?.Trim() ?? string.Empty;
                    term.Space = _space;
                    break;
            }

            var errors = ToleranceLibraryValidator.ValidateTerm(term)
                .Where(i => i.Severity == ConfigSeverity.Error)
                .Select(i => i.Message)
                .ToList();
            if (errors.Count > 0)
            {
                error = string.Join(Environment.NewLine, errors);
                return false;
            }

            return true;
        }
    }
}
