using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using ToleranceTool.Configuration;
using ToleranceTool.Configuration.Scales;
using ToleranceTool.Core.Scales;
using ToleranceTool.Wpf.Mvvm;

namespace ToleranceTool.Wpf.Scales
{
    public sealed class ScaleTypeEditorViewModel : ObservableObject
    {
        private const int PlotSamples = 120;

        private string _path;
        private ScaleTypeVm? _selected;
        private string _status = string.Empty;
        private string _validationText = string.Empty;
        private bool _isValid;
        private PointCollection _forwardPoints = new PointCollection();
        private PointCollection _inversePoints = new PointCollection();
        private readonly PointCollection _referencePoints = new PointCollection { new System.Windows.Point(0, 0), new System.Windows.Point(1, 1) };

        public ScaleTypeEditorViewModel(string? path = null)
        {
            _path = path ?? ConfigurationPaths.ScaleTypeLibraryFile;

            AddCommand = new RelayCommand(Add);
            DeleteCommand = new RelayCommand(Delete, () => Selected != null);
            LoadCommand = new RelayCommand(LoadFromDialog);
            SaveCommand = new RelayCommand(() => Save(_path));
            SaveAsCommand = new RelayCommand(SaveAs);
            ValidateCommand = new RelayCommand(Validate, () => Selected != null);

            if (File.Exists(_path))
            {
                Load(_path);
            }
            else
            {
                Seed();
            }
        }

        public ObservableCollection<ScaleTypeVm> ScaleTypes { get; } = new ObservableCollection<ScaleTypeVm>();

        public ScaleTypeVm? Selected
        {
            get => _selected;
            set
            {
                if (_selected != null)
                {
                    _selected.PropertyChanged -= OnSelectedChanged;
                    _selected.Parameters.CollectionChanged -= OnParametersChanged;
                }

                if (Set(ref _selected, value))
                {
                    if (_selected != null)
                    {
                        _selected.PropertyChanged += OnSelectedChanged;
                        _selected.Parameters.CollectionChanged += OnParametersChanged;
                    }

                    Validate();
                }
            }
        }

        public string Status { get => _status; private set => Set(ref _status, value); }

        public string ValidationText { get => _validationText; private set => Set(ref _validationText, value); }

        public bool IsValid { get => _isValid; private set => Set(ref _isValid, value); }

        public PointCollection ForwardPoints { get => _forwardPoints; private set => Set(ref _forwardPoints, value); }

        public PointCollection InversePoints { get => _inversePoints; private set => Set(ref _inversePoints, value); }

        public PointCollection ReferencePoints => _referencePoints;

        public ICommand AddCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand LoadCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand SaveAsCommand { get; }
        public ICommand ValidateCommand { get; }

        // --- data ---------------------------------------------------------

        private void Seed()
        {
            ScaleTypes.Clear();
            ScaleTypes.Add(new ScaleTypeVm(new ScaleType { Name = "Linear", Forward = "x", Inverse = "x" }));
            ScaleTypes.Add(new ScaleTypeVm(new ScaleType { Name = "SquareRoot", Forward = "Pow(x, 2)", Inverse = "Sqrt(x)" }));

            var log = new ScaleType
            {
                Name = "Logarithmic",
                Forward = "(Pow(10, x * decades) - 1) / (Pow(10, decades) - 1)",
                Inverse = "Log10(x * (Pow(10, decades) - 1) + 1) / decades",
            };
            log.Parameters["decades"] = 2;
            ScaleTypes.Add(new ScaleTypeVm(log));

            Selected = ScaleTypes.FirstOrDefault();
            Status = "Seeded with the built-in curves. Save to create the library file.";
        }

        private void Load(string path)
        {
            ConfigLoadResult<List<ScaleType>> result = ScaleTypeLibraryXml.Load(path);
            ScaleTypes.Clear();
            foreach (ScaleType scaleType in result.Value)
            {
                ScaleTypes.Add(new ScaleTypeVm(scaleType));
            }

            _path = path;
            Selected = ScaleTypes.FirstOrDefault();
            Status = result.Issues.Count == 0
                ? $"Loaded {ScaleTypes.Count} scale type(s) — {path}"
                : $"Loaded with {result.Issues.Count} issue(s) — {result.Issues[0].Message}";
        }

        private void LoadFromDialog()
        {
            var dialog = new OpenFileDialog { Filter = "Scale types (*.xml)|*.xml|All files (*.*)|*.*" };
            if (dialog.ShowDialog() == true)
            {
                Load(dialog.FileName);
            }
        }

        private void Save(string path)
        {
            try
            {
                ScaleTypeLibraryXml.Save(ScaleTypes.Select(vm => vm.ToScaleType()), path);
                _path = path;
                Status = $"Saved {ScaleTypes.Count} scale type(s) — {path}";
            }
            catch (Exception ex)
            {
                Status = "Save failed: " + ex.Message;
            }
        }

        private void SaveAs()
        {
            var dialog = new SaveFileDialog { Filter = "Scale types (*.xml)|*.xml", FileName = Path.GetFileName(_path) };
            if (dialog.ShowDialog() == true)
            {
                Save(dialog.FileName);
            }
        }

        private void Add()
        {
            var vm = new ScaleTypeVm(new ScaleType { Name = "NewCurve", Forward = "x", Inverse = "x" });
            ScaleTypes.Add(vm);
            Selected = vm;
        }

        private void Delete()
        {
            if (Selected == null)
            {
                return;
            }

            int index = ScaleTypes.IndexOf(Selected);
            ScaleTypes.Remove(Selected);
            Selected = ScaleTypes.Count == 0 ? null : ScaleTypes[Math.Min(index, ScaleTypes.Count - 1)];
        }

        // --- validation + plot ------------------------------------------

        private void OnSelectedChanged(object? sender, PropertyChangedEventArgs e) => Validate();

        private void OnParametersChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (ScaleParamVm p in e.NewItems.OfType<ScaleParamVm>())
                {
                    p.PropertyChanged += OnSelectedChanged;
                }
            }

            Validate();
        }

        private void Validate()
        {
            if (Selected == null)
            {
                ValidationText = "Select a scale type.";
                IsValid = false;
                ForwardPoints = new PointCollection();
                InversePoints = new PointCollection();
                return;
            }

            ScaleType scaleType = Selected.ToScaleType();
            IReadOnlyList<ConfigIssue> issues = ScaleTypeLibraryXml.ValidateCurve(scaleType);

            IsValid = issues.Count == 0;
            ValidationText = issues.Count == 0
                ? $"\"{scaleType.Name}\" satisfies the contract: Forward(0)=0, Forward(1)=1, monotonic."
                : string.Join(Environment.NewLine, issues.Select(i => "• " + i.Message));

            ScaleCurve? curve = SafeCurve(scaleType);
            ForwardPoints = Sample(curve, forward: true);
            InversePoints = Sample(curve, forward: false);
        }

        private static PointCollection Sample(ScaleCurve? curve, bool forward)
        {
            var points = new PointCollection();
            if (curve == null)
            {
                return points;
            }

            for (int i = 0; i <= PlotSamples; i++)
            {
                double x = (double)i / PlotSamples;
                double y;
                try
                {
                    y = forward ? curve.Forward(x) : curve.Inverse(x);
                }
                catch
                {
                    continue;
                }

                if (!double.IsNaN(y) && !double.IsInfinity(y) && y >= -0.25 && y <= 1.25)
                {
                    points.Add(new System.Windows.Point(x, y));
                }
            }

            return points;
        }

        private static ScaleCurve? SafeCurve(ScaleType scaleType)
        {
            try
            {
                return new ScaleCurve(scaleType);
            }
            catch
            {
                return null;
            }
        }
    }
}
