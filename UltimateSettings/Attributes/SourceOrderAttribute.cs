namespace UltimateSettings.Attributes;

/// <summary>
/// Declares the source precedence used to resolve a setting. Placed on a class it sets the
/// default for every property without its own attribute; placed on a property it overrides the class default.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
public sealed class SourceOrderAttribute : Attribute
{
    public SourceOrderAttribute(params string[] sourceIds)
    {
        SourceIds = sourceIds;
    }

    public IReadOnlyList<string> SourceIds { get; }
}
