using System.Text;

namespace UltimateSettings;

/// <summary>
/// Explains how one settings property was resolved.
/// </summary>
public sealed class ResolutionInfo
{
    public required string PropertyPath { get; init; }
    public object? ResolvedValue { get; init; }
    public string? WinningSourceId { get; init; }
    public required IReadOnlyList<string> AppliedSourceOrder { get; init; }
    public string? AppliedConditionProperty { get; init; }
    public bool? AppliedConditionValue { get; init; }
    public required IReadOnlyList<ConditionEvaluationInfo> ConditionEvaluations { get; init; }
    public required IReadOnlyList<SourceValueInfo> Sources { get; init; }
}

/// <summary>
/// Describes one conditional precedence rule evaluated during resolution.
/// </summary>
public sealed class ConditionEvaluationInfo
{
    public required string ConditionProperty { get; init; }
    public bool? Value { get; init; }
    public required IReadOnlyList<string> SourceOrder { get; init; }
    public bool Applied { get; init; }
}

/// <summary>
/// Describes a source considered while resolving one property.
/// </summary>
public sealed class SourceValueInfo
{
    public required string SourceId { get; init; }
    public bool WasConsidered { get; init; }
    public bool CanRead { get; init; }
    public bool HasValue { get; init; }
    public object? Value { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// Formats resolution metadata as a readable, deterministic text dump.
/// </summary>
public static class ResolutionDebugDump
{
    public static string Format(IReadOnlyDictionary<string, ResolutionInfo> resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);

        var builder = new StringBuilder();
        foreach (var entry in resolution.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            var info = entry.Value;
            builder.AppendLine($"Property: {info.PropertyPath}");
            builder.AppendLine($"  Resolved value: {FormatValue(info.ResolvedValue)}");
            builder.AppendLine($"  Winning source: {info.WinningSourceId ?? "<schema default>"}");
            builder.AppendLine($"  Applied order: {string.Join(", ", info.AppliedSourceOrder)}");

            if (info.ConditionEvaluations.Count > 0)
            {
                builder.AppendLine("  Conditions:");
                foreach (var condition in info.ConditionEvaluations)
                {
                    builder.AppendLine(
                        $"    {condition.ConditionProperty} = {condition.Value?.ToString() ?? "null"}; " +
                        $"order [{string.Join(", ", condition.SourceOrder)}]; applied: {condition.Applied}");
                }
            }

            builder.AppendLine("  Sources:");
            foreach (var source in info.Sources)
            {
                var state = source.Error is not null
                    ? $"error: {source.Error}"
                    : source.HasValue
                        ? $"value: {FormatValue(source.Value)}"
                        : "no value";
                builder.AppendLine(
                    $"    {source.SourceId}; considered: {source.WasConsidered}; can read: {source.CanRead}; {state}");
            }
        }

        return builder.ToString();
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => "null",
            string text => $"\"{text}\"",
            IEnumerable<string> strings => $"[{string.Join(", ", strings.Select(text => $"\"{text}\""))}]",
            _ => value.ToString() ?? "null"
        };
    }
}