using System.Collections.ObjectModel;
using System.Globalization;
using ToleranceTool.Core.Scales;
using ToleranceTool.Wpf.Mvvm;

namespace ToleranceTool.Wpf.Scales
{
    public sealed class ScaleParamVm : ObservableObject
    {
        private string _name = string.Empty;
        private string _value = "0";

        public string Name { get => _name; set => Set(ref _name, value); }

        public string Value { get => _value; set => Set(ref _value, value); }
    }

    /// <summary>Editable view of one <see cref="ScaleType"/>.</summary>
    public sealed class ScaleTypeVm : ObservableObject
    {
        private string _name;
        private string _forward;
        private string _inverse;

        public ScaleTypeVm(ScaleType source)
        {
            _name = source.Name;
            _forward = source.Forward;
            _inverse = source.Inverse;
            Parameters = new ObservableCollection<ScaleParamVm>();
            foreach (var p in source.Parameters)
            {
                Parameters.Add(new ScaleParamVm { Name = p.Key, Value = p.Value.ToString("R", CultureInfo.InvariantCulture) });
            }
        }

        public string Name { get => _name; set => Set(ref _name, value); }

        public string Forward { get => _forward; set => Set(ref _forward, value); }

        public string Inverse { get => _inverse; set => Set(ref _inverse, value); }

        public ObservableCollection<ScaleParamVm> Parameters { get; }

        public ScaleType ToScaleType()
        {
            var scaleType = new ScaleType
            {
                Name = Name?.Trim() ?? string.Empty,
                Forward = Forward?.Trim() ?? string.Empty,
                Inverse = Inverse?.Trim() ?? string.Empty,
            };

            foreach (ScaleParamVm p in Parameters)
            {
                if (!string.IsNullOrWhiteSpace(p.Name) &&
                    double.TryParse(p.Value?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                {
                    scaleType.Parameters[p.Name.Trim()] = value;
                }
            }

            return scaleType;
        }
    }
}
