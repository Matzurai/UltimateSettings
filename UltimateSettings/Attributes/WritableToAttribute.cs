namespace UltimateSettings.Attributes;

/// <summary>
/// Restricts a setting to the listed source ids when it is edited.
/// Without this attribute, every registered writable source is allowed.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class WritableToAttribute : Attribute
{
    public WritableToAttribute(params string[] sourceIds)
    {
        SourceIds = sourceIds;
    }

    public IReadOnlyList<string> SourceIds { get; }
}