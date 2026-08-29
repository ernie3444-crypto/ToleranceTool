using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ToleranceTool.Import
{
    public enum SignalFieldKind
    {
        Text = 0,
        Number = 1,
        ConversionSense = 2,
    }

    /// <summary>One field the import can populate on a <see cref="Core.Signals.SignalConfig"/>.</summary>
    public sealed class SignalField
    {
        private SignalField(string name, SignalFieldKind kind, bool requiredByDefault, bool masterOnly = false)
        {
            Name = name;
            Kind = kind;
            RequiredByDefault = requiredByDefault;
            MasterOnly = masterOnly;
        }

        public string Name { get; }

        public SignalFieldKind Kind { get; }

        /// <summary>Whether a fresh field binding starts with its "required" flag set.</summary>
        public bool RequiredByDefault { get; }

        /// <summary>True for fields only the master source carries (Sensor Name).</summary>
        public bool MasterOnly { get; }

        public const string UniversalId = "UniversalId";
        public const string SensorName = "SensorName";
        public const string ConversionSense = "ConversionSense";
        public const string ScaleType = "ScaleType";
        public const string SignalType = "SignalType";
        public const string ModuleType = "ModuleType";
        public const string RawLow = "RawLow";
        public const string RawHigh = "RawHigh";
        public const string EuLow = "EuLow";
        public const string EuHigh = "EuHigh";
        public const string EuLowSi = "EuLowSi";
        public const string EuHighSi = "EuHighSi";

        /// <summary>The mappable fields, excluding <see cref="UniversalId"/> which every source binds as its key.</summary>
        public static readonly IReadOnlyList<SignalField> All = new ReadOnlyCollection<SignalField>(new[]
        {
            new SignalField(SensorName, SignalFieldKind.Text, requiredByDefault: true, masterOnly: true),
            new SignalField(ConversionSense, SignalFieldKind.ConversionSense, requiredByDefault: true),
            new SignalField(ScaleType, SignalFieldKind.Text, requiredByDefault: true),
            new SignalField(SignalType, SignalFieldKind.Text, requiredByDefault: true),
            new SignalField(ModuleType, SignalFieldKind.Text, requiredByDefault: true),
            new SignalField(RawLow, SignalFieldKind.Number, requiredByDefault: false),
            new SignalField(RawHigh, SignalFieldKind.Number, requiredByDefault: false),
            new SignalField(EuLow, SignalFieldKind.Number, requiredByDefault: true),
            new SignalField(EuHigh, SignalFieldKind.Number, requiredByDefault: true),
            new SignalField(EuLowSi, SignalFieldKind.Number, requiredByDefault: true),
            new SignalField(EuHighSi, SignalFieldKind.Number, requiredByDefault: true),
        });

        public static SignalField? Find(string name)
        {
            foreach (SignalField field in All)
            {
                if (field.Name == name)
                {
                    return field;
                }
            }

            return null;
        }

        /// <summary>
        /// The SI counterpart of an English EU-range field, for the metric-value
        /// column of a parameter-per-row source. Null when the field has no SI pair.
        /// </summary>
        public static string? SiCounterpart(string fieldName)
        {
            switch (fieldName)
            {
                case EuLow: return EuLowSi;
                case EuHigh: return EuHighSi;
                default: return null;
            }
        }
    }
}
