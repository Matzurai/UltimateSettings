namespace UltimateSettings.Attributes;

/// <summary>
/// Declares an alternate source precedence used when the named boolean condition property currently
/// resolves to true. Multiple instances stack and are evaluated in declaration order; the first true
/// condition wins, otherwise the property's or class's <see cref="SourceOrderAttribute"/> applies.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public sealed class SourceOrderIfAttribute : Attribute
{
    public SourceOrderIfAttribute(string conditionProperty, params string[] sourceIds)
    {
        ConditionProperty = conditionProperty;
        SourceIds = sourceIds;
    }

    public string ConditionProperty { get; }

    public IReadOnlyList<string> SourceIds { get; }
}
