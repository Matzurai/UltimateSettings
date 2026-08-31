using System.Reflection;
using System.Text.Json;
using UltimateSettings.Sources;

namespace UltimateSettings.Internal;

/// <summary>
/// Builds a resolved settings instance by evaluating each property's precedence, including conditional
/// overrides, and reading the first available source in that order. Properties whose type is itself a
/// <see cref="SettingsBase"/> are resolved recursively, field by field, rather than read as a single
/// opaque value from one source. Precedence for a nested field is: matching conditional order, then the
/// innermost declared property-level SourceOrder along the path from root to leaf, then the manager's
/// explicit default order, then the innermost declared class-level SourceOrder along that same path.
/// </summary>
internal static class SettingsResolver<TSettings>
    where TSettings : SettingsBase, new()
{
    public static TSettings Resolve(IReadOnlyDictionary<string, ISettingsSource> sources, IReadOnlyList<string> defaultOrder)
    {
        var instance = new TSettings();

        ResolveObject(
            typeof(TSettings),
            instance,
            sources,
            defaultOrder,
            path: Array.Empty<PropertyInfo>(),
            inheritedClassOrders: Array.Empty<IReadOnlyList<string>?>(),
            inheritedPropertyOrders: Array.Empty<IReadOnlyList<string>?>());

        return instance;
    }

    private static void ResolveObject(
        Type type,
        object instance,
        IReadOnlyDictionary<string, ISettingsSource> sources,
        IReadOnlyList<string> defaultOrder,
        IReadOnlyList<PropertyInfo> path,
        IReadOnlyList<IReadOnlyList<string>?> inheritedClassOrders,
        IReadOnlyList<IReadOnlyList<string>?> inheritedPropertyOrders)
    {
        foreach (var property in SettingsTypeMetadataCache.GetResolutionOrder(type))
        {
            var fullPath = Append(path, property.Property);

            if (SettingsTypeMetadataCache.IsNestedSettingsType(property.Property.PropertyType))
            {
                var nestedInstance = Activator.CreateInstance(property.Property.PropertyType)
                    ?? throw new InvalidOperationException(
                        $"Unable to create an instance of nested settings type '{property.Property.PropertyType.Name}'. It must have a public parameterless constructor.");

                ResolveObject(
                    property.Property.PropertyType,
                    nestedInstance,
                    sources,
                    defaultOrder,
                    fullPath,
                    Append(inheritedClassOrders, property.ClassOrder),
                    Append(inheritedPropertyOrders, property.PropertyOrder));

                property.Property.SetValue(instance, nestedInstance);
                continue;
            }

            var order = ResolveEffectiveOrder(property, instance, defaultOrder, inheritedClassOrders, inheritedPropertyOrders);
            var targetType = property.Property.PropertyType;

            foreach (var sourceId in order)
            {
                if (!sources.TryGetValue(sourceId, out var source) || !source.CanRead)
                {
                    continue;
                }

                if (TryReadPath(source, fullPath, targetType, out var value))
                {
                    var coerced = CoerceValue(value, targetType);
                    property.Property.SetValue(instance, coerced);
                    break;
                }
            }
        }
    }

    private static IReadOnlyList<T> Append<T>(IReadOnlyList<T> list, T item)
    {
        var result = new T[list.Count + 1];
        for (var i = 0; i < list.Count; i++)
        {
            result[i] = list[i];
        }

        result[list.Count] = item;
        return result;
    }

    /// <summary>
    /// Reads the value for a (possibly nested) property path from a single source. For a top-level property
    /// this is a direct read. For a nested property, the top-level container is read as a raw blob from the
    /// source, then navigated field by field using JSON or reflection, since sources only store values keyed
    /// by the root property name.
    /// </summary>
    private static bool TryReadPath(ISettingsSource source, IReadOnlyList<PropertyInfo> path, Type targetType, out object? value)
    {
        if (path.Count == 1)
        {
            return source.TryRead(path[0].Name, targetType, out value);
        }

        if (!source.TryRead(path[0].Name, typeof(object), out var current) || current is null)
        {
            value = null;
            return false;
        }

        for (var i = 1; i < path.Count - 1; i++)
        {
            if (!TryNavigate(current, path[i].Name, out current) || current is null)
            {
                value = null;
                return false;
            }
        }

        return TryNavigateLeaf(current, path[^1].Name, targetType, out value);
    }

    private static bool TryNavigate(object current, string propertyName, out object? result)
    {
        if (current is JsonElement jsonElement)
        {
            if (jsonElement.ValueKind == JsonValueKind.Object && jsonElement.TryGetProperty(propertyName, out var child))
            {
                result = child;
                return true;
            }

            result = null;
            return false;
        }

        var propertyInfo = current.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (propertyInfo is null)
        {
            result = null;
            return false;
        }

        result = propertyInfo.GetValue(current);
        return result is not null;
    }

    private static bool TryNavigateLeaf(object current, string propertyName, Type targetType, out object? value)
    {
        if (current is JsonElement jsonElement)
        {
            if (jsonElement.ValueKind != JsonValueKind.Object || !jsonElement.TryGetProperty(propertyName, out var child))
            {
                value = null;
                return false;
            }

            if (targetType == typeof(object) || targetType == typeof(JsonElement))
            {
                value = child;
                return true;
            }

            try
            {
                value = JsonSerializer.Deserialize(child, targetType);
                return true;
            }
            catch
            {
                value = null;
                return false;
            }
        }

        var propertyInfo = current.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (propertyInfo is null)
        {
            value = null;
            return false;
        }

        value = propertyInfo.GetValue(current);
        return true;
    }

    private static object? CoerceValue(object? value, Type targetType)
    {
        if (value is null)
        {
            return null;
        }

        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (underlyingType.IsInstanceOfType(value))
        {
            return value;
        }

        if (value is System.Text.Json.JsonElement jsonElement)
        {
            return System.Text.Json.JsonSerializer.Deserialize(jsonElement, targetType);
        }

        if (underlyingType.IsEnum && value is string enumString)
        {
            return Enum.Parse(underlyingType, enumString, ignoreCase: true);
        }

        try
        {
            return Convert.ChangeType(value, underlyingType);
        }
        catch
        {
            return value;
        }
    }

    // Precedence: matching conditional order > innermost property-level SourceOrder (this property, then walking
    // outward along the nesting path) > manager's explicit default order > innermost class-level SourceOrder
    // (this property's declaring type, then walking outward along the nesting path).
    private static IReadOnlyList<string> ResolveEffectiveOrder(
        PropertyMetadata property,
        object parentInstance,
        IReadOnlyList<string> defaultOrder,
        IReadOnlyList<IReadOnlyList<string>?> inheritedClassOrders,
        IReadOnlyList<IReadOnlyList<string>?> inheritedPropertyOrders)
    {
        foreach (var conditionalOrder in property.ConditionalOrders)
        {
            var conditionValue = conditionalOrder.ConditionProperty.GetValue(parentInstance) as bool?;
            if (conditionValue == true)
            {
                return conditionalOrder.SourceIds;
            }
        }

        if (property.PropertyOrder is not null)
        {
            return property.PropertyOrder;
        }

        for (var i = inheritedPropertyOrders.Count - 1; i >= 0; i--)
        {
            if (inheritedPropertyOrders[i] is not null)
            {
                return inheritedPropertyOrders[i]!;
            }
        }

        if (defaultOrder.Count > 0)
        {
            return defaultOrder;
        }

        if (property.ClassOrder is not null)
        {
            return property.ClassOrder;
        }

        for (var i = inheritedClassOrders.Count - 1; i >= 0; i--)
        {
            if (inheritedClassOrders[i] is not null)
            {
                return inheritedClassOrders[i]!;
            }
        }

        return defaultOrder;
    }
}
