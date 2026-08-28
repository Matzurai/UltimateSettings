using System.Reflection;
using UltimateSettings.Attributes;

namespace UltimateSettings.Internal;

/// <summary>
/// Reflects over a settings type once and caches its precedence rules, validating condition types and
/// detecting <see cref="SourceOrderIfAttribute"/> dependency cycles at registration time.
/// </summary>
internal static class SettingsTypeMetadata<TSettings>
    where TSettings : SettingsBase, new()
{
    private static readonly Lazy<IReadOnlyList<PropertyMetadata>> LazyResolutionOrder = new(Build);
    private static readonly Lazy<IReadOnlyCollection<string>> LazyAllReferencedSourceIds =
        new(() => CollectReferencedSourceIds(LazyResolutionOrder.Value));

    /// <summary>
    /// The order in which properties should be resolved, respecting <see cref="SourceOrderIfAttribute"/> dependencies.
    /// This is independent of the precedence order of the sources.
    /// </summary>
    public static IReadOnlyList<PropertyMetadata> ResolutionOrder => LazyResolutionOrder.Value;

    public static IReadOnlyCollection<string> AllReferencedSourceIds => LazyAllReferencedSourceIds.Value;

    private static IReadOnlyList<PropertyMetadata> Build()
    {
        var type = typeof(TSettings);
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
                BaseOrder = propertyOrder ?? classOrder,
                ConditionalOrders = conditionalOrders
            };
            dependencies[property] = propertyDependencies;
        }

        return TopologicalSort(dependencies, metadataByProperty);
    }

    private static List<PropertyMetadata> TopologicalSort(
        Dictionary<PropertyInfo, List<PropertyInfo>> dependencies,
        Dictionary<PropertyInfo, PropertyMetadata> metadataByProperty)
    {
        var result = new List<PropertyMetadata>();
        var isResolved = new Dictionary<PropertyInfo, bool>();

        foreach (var property in dependencies.Keys)
        {
            Visit(property, dependencies, metadataByProperty, isResolved, result);
        }

        return result;
    }

    private static void Visit(
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
                    $"Cyclic SourceOrderIf dependency detected involving '{typeof(TSettings).Name}.{property.Name}'.");
            }

            return;
        }

        isResolved[property] = false; // mark as visited but not done - only if we can resolve all dependencies will we mark it as done

        foreach (var dependency in dependencies[property])
        {
            Visit(dependency, dependencies, metadataByProperty, isResolved, result);
        }

        isResolved[property] = true;
        result.Add(metadataByProperty[property]);
    }

    private static IReadOnlyCollection<string> CollectReferencedSourceIds(IReadOnlyList<PropertyMetadata> properties)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var property in properties)
        {
            if (property.BaseOrder is not null)
            {
                foreach (var id in property.BaseOrder)
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
        }

        return ids;
    }
}
