using System.Collections.Generic;
using System.Linq;

namespace ToleranceTool.Configuration
{
    public enum ConfigSeverity
    {
        Warning = 0,
        Error = 1,
    }

    /// <summary>One problem found while loading or validating a configuration artifact.</summary>
    public sealed class ConfigIssue
    {
        public ConfigIssue(ConfigSeverity severity, string message, string? scope = null)
        {
            Severity = severity;
            Message = message;
            Scope = scope;
        }

        public ConfigSeverity Severity { get; }

        public string Message { get; }

        /// <summary>Optional locator, e.g. the tolerance key <c>"4-20mA / AI-871"</c>.</summary>
        public string? Scope { get; }

        public override string ToString() =>
            Scope == null ? $"{Severity}: {Message}" : $"{Severity} [{Scope}]: {Message}";

        public static ConfigIssue Error(string message, string? scope = null) =>
            new ConfigIssue(ConfigSeverity.Error, message, scope);

        public static ConfigIssue Warning(string message, string? scope = null) =>
            new ConfigIssue(ConfigSeverity.Warning, message, scope);
    }

    /// <summary>The outcome of a load: the value (possibly partial) and everything that went wrong.</summary>
    public sealed class ConfigLoadResult<T>
    {
        public ConfigLoadResult(T value, IReadOnlyList<ConfigIssue> issues)
        {
            Value = value;
            Issues = issues;
        }

        public T Value { get; }

        public IReadOnlyList<ConfigIssue> Issues { get; }

        public bool HasErrors => Issues.Any(i => i.Severity == ConfigSeverity.Error);

        public IEnumerable<ConfigIssue> Errors => Issues.Where(i => i.Severity == ConfigSeverity.Error);

        public IEnumerable<ConfigIssue> Warnings => Issues.Where(i => i.Severity == ConfigSeverity.Warning);
    }
}
