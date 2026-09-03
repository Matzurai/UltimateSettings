using System.Collections.Concurrent;
using System.Reflection;
using UltimateSettings.Attributes;

namespace UltimateSettings.Internal;

/// <summary>
/// Non-generic core that reflects over a settings type (root or nested) once and caches its precedence
/// rules, validating condition types and detecting <see cref="SourceOrderIfAttribute"/> dependency cycles
/// at registration time. Shared by <see cref="SettingsTypeMetadata{TSettings}"/> and by
/// <see cref="SettingsResolver{TSettings}"/> when recursing into nested settings objects.
/// </summary>
internal static class SettingsTypeMetadataCache
{
    private static readonly ConcurrentDictionary<Type, IReadOnlyList<PropertyMetadata>> ResolutionOrderCache = new();
    private static readonly ConcurrentDictionary<Type, IReadOnlyCollection<string>> ReferencedSourceIdsCache = new();
    private static readonly ConcurrentDictionary<Type, IReadOnlyList<string>> ArrayMergeWarningsCache = new();

    public static IReadOnlyList<PropertyMetadata> GetResolutionOrder(Type type)
    {
        return ResolutionOrderCache.GetOrAdd(type, Build);
    }

    public static IReadOnlyCollection<string> GetAllReferencedSourceIds(Type type)
    {
        return ReferencedSourceIdsCache.GetOrAdd(type, t =>
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            CollectReferencedSourceIds(t, ids, new HashSet<Type>());
            return ids;
        });
    }

    /// <summary>
    /// Finds properties, anywhere in the settings object graph, that hold a collection whose element type
    /// derives from <see cref="SettingsBase"/> and that are not marked with
    /// <see cref="SuppressArrayMergeWarningAttribute"/>. Such collections are replaced wholesale by the
    /// highest-precedence source that has a value, rather than merged element-wise.
    /// </summary>
    public static IReadOnlyList<string> GetArrayMergeWarnings(Type type)
    {
        return ArrayMergeWarningsCache.GetOrAdd(type, t =>
        {
            var warnings = new List<string>();
            CollectArrayMergeWarnings(t, t.Name, warnings, new HashSet<Type>());
            return warnings;
        });
    }

    /// <summary>
    /// Whether a property type is itself a settings object whose properties should be resolved recursively,
    /// rather than read as a single opaque value from a source.
    /// </summary>
    public static bool IsNestedSettingsType(Type type)
    {
        return typeof(SettingsBase).IsAssignableFrom(type) && type != typeof(SettingsBase);
    }

    private static IReadOnlyList<PropertyMetadata> Build(Type type)
    {
        var classOrder = type.GetCustomAttribute<SourceOrderAttribute>()?.SourceIds;
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite)
            .ToArray();

        var metadataByProperty = new Dictionary<PropertyInfo, PropertyMetadata>();
        var dependencies = new Dictionary<PropertyInfo, List<PropertyInfo>>();

        foreach (var property in properties)
        {
            var isInitOnly = property.SetMethod?.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(System.Runtime.CompilerServices.IsExternalInit));
            if(isInitOnly != true)
            {
                throw new InvalidOperationException(
                    $"'{type.Name}.{property.Name}' is not an init-only property. SettingsBase-derived types must declare properties with an init accessor to prevent accidental writes to the in-memory snapshot.");
            }
            var propertyOrder = property.GetCustomAttribute<SourceOrderAttribute>()?.SourceIds;
            var conditionalOrders = new List<ConditionalOrder>();
            var propertyDependencies = new List<PropertyInfo>();

            foreach (var conditionAttribute in property.GetCustomAttributes<SourceOrderIfAttribute>())
            {
                var conditionProperty = type.GetProperty(conditionAttribute.ConditionProperty)
                    ?? throw new InvalidOperationException(
                        $"'{type.Name}.{property.Name}' references unknown condition property '{conditionAttribute.ConditionProperty}'.");

                if (conditionProperty.PropertyType != typeof(bool) && conditionProperty.PropertyType != typeof(bool?))
                {
                    throw new InvalidOperationException(
                        $"'{type.Name}.{conditionProperty.Name}' is used as a SourceOrderIf condition but is not a bool or bool?.");
                }

                conditionalOrders.Add(new ConditionalOrder
                {
                    ConditionProperty = conditionProperty,
                    SourceIds = conditionAttribute.SourceIds
                });
                propertyDependencies.Add(conditionProperty);
            }

            metadataByProperty[property] = new PropertyMetadata
            {
                Property = property,
                PropertyOrder = propertyOrder,
                ClassOrder = classOrder,
                ConditionalOrders = conditionalOrders
            };
            dependencies[property] = propertyDependencies;
        }

        return TopologicalSort(type, dependencies, metadataByProperty);
    }

    private static List<PropertyMetadata> TopologicalSort(
        Type type,
        Dictionary<PropertyInfo, List<PropertyInfo>> dependencies,
        Dictionary<PropertyInfo, PropertyMetadata> metadataByProperty)
    {
        var result = new List<PropertyMetadata>();
        var isResolved = new Dictionary<PropertyInfo, bool>();

        foreach (var property in dependencies.Keys)
        {
            Visit(type, property, dependencies, metadataByProperty, isResolved, result);
        }

        return result;
    }

    private static void Visit(
        Type type,
        PropertyInfo property,
        Dictionary<PropertyInfo, List<PropertyInfo>> dependencies,
        Dictionary<PropertyInfo, PropertyMetadata> metadataByProperty,
        Dictionary<PropertyInfo, bool> isResolved,
        List<PropertyMetadata> result)
    {
        if (isResolved.TryGetValue(property, out var done)) //if already visited, but not done, we have a cycle
        {
            if (!done)
            {
                throw new InvalidOperationException(
                    $"Cyclic SourceOrderIf dependency detected involving '{type.Name}.{property.Name}'.");
            }

            return;
        }

        isResolved[property] = false; // mark as visited but not done - only if we can resolve all dependencies will we mark it as done

        foreach (var dependency in dependencies[property])
        {
            Visit(type, dependency, dependencies, metadataByProperty, isResolved, result);
        }

        isResolved[property] = true;
        result.Add(metadataByProperty[property]);
    }

    private static void CollectReferencedSourceIds(Type type, HashSet<string> ids, HashSet<Type> visiting)
    {
        if (!visiting.Add(type))
        {
            // Cycle in the settings object graph shape; already being processed higher up the call stack.
            return;
        }

        foreach (var property in GetResolutionOrder(type))
        {
            if (property.PropertyOrder is not null)
            {
                foreach (var id in property.PropertyOrder)
                {
                    ids.Add(id);
                }
            }

            if (property.ClassOrder is not null)
            {
                foreach (var id in property.ClassOrder)
                {
                    ids.Add(id);
                }
            }

            foreach (var conditionalOrder in property.ConditionalOrders)
            {
                foreach (var id in conditionalOrder.SourceIds)
                {
                    ids.Add(id);
                }
            }

            if (IsNestedSettingsType(property.Property.PropertyType))
            {
                CollectReferencedSourceIds(property.Property.PropertyType, ids, visiting);
            }
        }

        visiting.Remove(type);
    }

    private static void CollectArrayMergeWarnings(Type type, string pathPrefix, List<string> warnings, HashSet<Type> visiting)
    {
        if (!visiting.Add(type))
        {
            // Cycle in the settings object graph shape; already being processed higher up the call stack.
            return;
        }

        foreach (var property in GetResolutionOrder(type))
        {
            var propertyPath = $"{pathPrefix}.{property.Property.Name}";
            var elementType = GetCollectionElementType(property.Property.PropertyType);

            if (elementType is not null && IsNestedSettingsType(elementType)
                && property.Property.GetCustomAttribute<SuppressArrayMergeWarningAttribute>() is null)
            {
                warnings.Add(
                    $"'{propertyPath}' is a collection of '{elementType.Name}', which derives from SettingsBase. " +
                    "Collections are never merged element-wise across sources; the entire collection from the " +
                    "highest-precedence source that has a value is used as-is. Apply [SuppressArrayMergeWarning] " +
                    "on the property if this is intentional.");
            }

            if (IsNestedSettingsType(property.Property.PropertyType))
            {
                CollectArrayMergeWarnings(property.Property.PropertyType, propertyPath, warnings, visiting);
            }
        }

        visiting.Remove(type);
    }

    /// <summary>
    /// Returns the element type of an array or generic enumerable, excluding <see cref="string"/>, or
    /// <see langword="null"/> if the type is not such a collection.
    /// </summary>
    private static Type? GetCollectionElementType(Type type)
    {
        if (type == typeof(string))
        {
            return null;
        }

        if (type.IsArray)
        {
            return type.GetElementType();
        }

        foreach (var candidate in type.GetInterfaces().Prepend(type))
        {
            if (candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                return candidate.GetGenericArguments()[0];
            }
        }

        return null;
    }
}
