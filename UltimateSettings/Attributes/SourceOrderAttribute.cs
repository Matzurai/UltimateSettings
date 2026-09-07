namespace UltimateSettings.Attributes;

/// <summary>
/// Declares the source precedence used to resolve a setting. Placed on a class it sets the
/// default for every property without its own attribute; placed on a property it overrides the class default.
/// If the property has a <see cref="SourceOrderIfAttribute"/>, that takes precedence over this attribute (if its condition is met).
/// If the property itself is a SettingsBase subclass, any <see cref="SourceOrderAttribute"/> on that class is overridden by the attribute on the property.
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
