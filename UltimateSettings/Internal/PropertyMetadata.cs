using System.Reflection;

namespace UltimateSettings.Internal;

/// <summary>
/// Resolution metadata for a single settings property: its base precedence and any conditional overrides.
/// </summary>
internal sealed class PropertyMetadata
{
    public required PropertyInfo Property { get; init; }

    /// <summary>
    /// Property-level or class-level source order. Null means the manager's default order applies.
    /// </summary>
    public IReadOnlyList<string>? BaseOrder { get; init; }

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
