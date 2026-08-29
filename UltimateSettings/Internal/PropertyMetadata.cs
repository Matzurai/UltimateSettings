using System.Reflection;

namespace UltimateSettings.Internal;

/// <summary>
/// Resolution metadata for a single settings property: its base precedence and any conditional overrides.
/// </summary>
internal sealed class PropertyMetadata
{
    public required PropertyInfo Property { get; init; }

    /// <summary>
    /// Property-level source order, from <see cref="Attributes.SourceOrderAttribute"/> on the property itself.
    /// Takes precedence over everything except a matching conditional order.
    /// </summary>
    public IReadOnlyList<string>? PropertyOrder { get; init; }

    /// <summary>
    /// Class-level source order, from <see cref="Attributes.SourceOrderAttribute"/> on the settings type.
    /// Used only as a last-resort fallback, below the manager's explicit default order.
    /// </summary>
    public IReadOnlyList<string>? ClassOrder { get; init; }

    public required IReadOnlyList<ConditionalOrder> ConditionalOrders { get; init; }
}

/// <summary>
/// A single <see cref="Attributes.SourceOrderIfAttribute"/> resolved against its condition property.
/// </summary>
internal sealed class ConditionalOrder
{
    public required PropertyInfo ConditionProperty { get; init; }

    public required IReadOnlyList<string> SourceIds { get; init; }
}
