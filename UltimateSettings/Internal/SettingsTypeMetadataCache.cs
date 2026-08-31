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
}
