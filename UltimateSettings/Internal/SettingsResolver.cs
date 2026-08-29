using UltimateSettings.Sources;

namespace UltimateSettings.Internal;

/// <summary>
/// Builds a resolved settings instance by evaluating each property's precedence, including conditional
/// overrides, and reading the first available source in that order.
/// </summary>
internal static class SettingsResolver<TSettings>
    where TSettings : SettingsBase, new()
{
    public static TSettings Resolve(IReadOnlyDictionary<string, ISettingsSource> sources, IReadOnlyList<string> defaultOrder)
    {
        var instance = new TSettings();

        foreach (var property in SettingsTypeMetadata<TSettings>.ResolutionOrder)
        {
            var order = ResolveEffectiveOrder(property, instance, defaultOrder);

            foreach (var sourceId in order)
            {
                if (!sources.TryGetValue(sourceId, out var source) || !source.CanRead)
                {
                    continue;
                }

                if (source.TryRead(property.Property.Name, out var value))
                {
                    property.Property.SetValue(instance, value);
                    break;
                }
            }
        }

        return instance;
    }

    // Precedence: matching conditional order > property-level SourceOrder > manager's explicit default order > class-level SourceOrder.
    private static IReadOnlyList<string> ResolveEffectiveOrder(PropertyMetadata property, TSettings instance, IReadOnlyList<string> defaultOrder)
    {
        foreach (var conditionalOrder in property.ConditionalOrders)
        {
            var conditionValue = conditionalOrder.ConditionProperty.GetValue(instance) as bool?;
            if (conditionValue == true)
            {
                return conditionalOrder.SourceIds;
            }
        }

        if (property.PropertyOrder is not null)
        {
            return property.PropertyOrder;
        }

        if (defaultOrder.Count > 0)
        {
            return defaultOrder;
        }

        return property.ClassOrder ?? defaultOrder;
    }
}
