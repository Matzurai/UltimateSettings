using System.Reflection;
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
    public static ResolvedSettings<TSettings> Resolve(
        IReadOnlyDictionary<string, ISettingsSource> sources,
        IReadOnlyList<string> defaultOrder)
    {
        var instance = new TSettings();
        var resolution = new Dictionary<string, ResolutionInfo>(StringComparer.Ordinal);

        ResolveObject(
            typeof(TSettings),
            instance,
            sources,
            defaultOrder,
            path: Array.Empty<PropertyInfo>(),
            inheritedClassOrders: Array.Empty<IReadOnlyList<string>?>(),
            inheritedPropertyOrders: Array.Empty<IReadOnlyList<string>?>(),
            resolution);

        return new ResolvedSettings<TSettings>(instance, resolution);
    }

    private static void ResolveObject(
        Type type,
        object instance,
        IReadOnlyDictionary<string, ISettingsSource> sources,
        IReadOnlyList<string> defaultOrder,
        IReadOnlyList<PropertyInfo> path,
        IReadOnlyList<IReadOnlyList<string>?> inheritedClassOrders,
        IReadOnlyList<IReadOnlyList<string>?> inheritedPropertyOrders,
        Dictionary<string, ResolutionInfo> resolution)
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
                    Append(inheritedPropertyOrders, property.PropertyOrder),
                    resolution);

                property.Property.SetValue(instance, nestedInstance);
                continue;
            }

            var effectiveOrder = ResolveEffectiveOrder(
                property,
                instance,
                defaultOrder,
                inheritedClassOrders,
                inheritedPropertyOrders);
            var order = effectiveOrder.SourceOrder;
            var targetType = property.Property.PropertyType;
            string? winningSourceId = null;
            var sourceValues = new List<SourceValueInfo>();

            foreach (var sourceId in order)
            {
                if (!sources.TryGetValue(sourceId, out var source))
                {
                    continue;
                }

                if (!source.CanRead)
                {
                    sourceValues.Add(new SourceValueInfo
                    {
                        SourceId = sourceId,
                        WasConsidered = true,
                        CanRead = false
                    });
                    continue;
                }

                try
                {
                    if (TryReadPath(source, fullPath, targetType, out var value))
                    {
                        var coerced = CoerceValue(value, targetType);
                        sourceValues.Add(new SourceValueInfo
                        {
                            SourceId = sourceId,
                            WasConsidered = true,
                            CanRead = true,
                            HasValue = true,
                            Value = value
                        });

                        if (winningSourceId is null)
                        {
                            winningSourceId = sourceId;
                            property.Property.SetValue(instance, coerced);
                        }
                    }
                    else
                    {
                        sourceValues.Add(new SourceValueInfo
                        {
                            SourceId = sourceId,
                            WasConsidered = true,
                            CanRead = true
                        });
                    }
                }
                catch (Exception exception)
                {
                    sourceValues.Add(new SourceValueInfo
                    {
                        SourceId = sourceId,
                        WasConsidered = true,
                        CanRead = true,
                        Error = exception.Message
                    });
                }
            }

            foreach (var sourceId in sources.Keys)
            {
                if (!order.Contains(sourceId, StringComparer.Ordinal))
                {
                    var source = sources[sourceId];
                    if (!source.CanRead)
                    {
                        sourceValues.Add(new SourceValueInfo
                        {
                            SourceId = sourceId,
                            WasConsidered = false,
                            CanRead = false
                        });
                        continue;
                    }

                    try
                    {
                        if (TryReadPath(source, fullPath, targetType, out var value))
                        {
                            sourceValues.Add(new SourceValueInfo
                            {
                                SourceId = sourceId,
                                WasConsidered = false,
                                CanRead = true,
                                HasValue = true,
                                Value = value
                            });
                        }
                        else
                        {
                            sourceValues.Add(new SourceValueInfo
                            {
                                SourceId = sourceId,
                                WasConsidered = false,
                                CanRead = true
                            });
                        }
                    }
                    catch (Exception exception)
                    {
                        sourceValues.Add(new SourceValueInfo
                        {
                            SourceId = sourceId,
                            WasConsidered = false,
                            CanRead = true,
                            Error = exception.Message
                        });
                    }
                }
            }

            resolution[string.Join('.', fullPath.Select(segment => segment.Name))] = new ResolutionInfo
            {
                PropertyPath = string.Join('.', fullPath.Select(segment => segment.Name)),
                ResolvedValue = property.Property.GetValue(instance),
                WinningSourceId = winningSourceId,
                AppliedSourceOrder = order,
                AppliedConditionProperty = effectiveOrder.AppliedConditionProperty,
                AppliedConditionValue = effectiveOrder.AppliedConditionValue,
                ConditionEvaluations = effectiveOrder.ConditionEvaluations,
                Sources = sourceValues
            };
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
    /// source, then navigated field by field via the source's own <see cref="ISettingsSource.TryNavigate"/>
    /// and <see cref="ISettingsSource.TryNavigateLeaf"/>, since sources only store values keyed by the root
    /// property name and each source knows the shape of its own raw representation.
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
            if (!source.TryNavigate(current, path[i].Name, out current) || current is null)
            {
                value = null;
                return false;
            }
        }

        return source.TryNavigateLeaf(current, path[^1].Name, targetType, out value);
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
    private static EffectiveOrder ResolveEffectiveOrder(
        PropertyMetadata property,
        object parentInstance,
        IReadOnlyList<string> defaultOrder,
        IReadOnlyList<IReadOnlyList<string>?> inheritedClassOrders,
        IReadOnlyList<IReadOnlyList<string>?> inheritedPropertyOrders)
    {
        var evaluations = new List<ConditionEvaluationInfo>();
        foreach (var conditionalOrder in property.ConditionalOrders)
        {
            var conditionValue = conditionalOrder.ConditionProperty.GetValue(parentInstance) as bool?;
            var applied = conditionValue == true;
            evaluations.Add(new ConditionEvaluationInfo
            {
                ConditionProperty = conditionalOrder.ConditionProperty.Name,
                Value = conditionValue,
                SourceOrder = conditionalOrder.SourceIds,
                Applied = applied
            });

            if (conditionValue == true)
            {
                return new EffectiveOrder(
                    conditionalOrder.SourceIds,
                    conditionalOrder.ConditionProperty.Name,
                    conditionValue,
                    evaluations);
            }
        }

        if (property.PropertyOrder is not null)
        {
            return new EffectiveOrder(property.PropertyOrder, null, null, evaluations);
        }

        for (var i = inheritedPropertyOrders.Count - 1; i >= 0; i--)
        {
            if (inheritedPropertyOrders[i] is not null)
            {
                return new EffectiveOrder(inheritedPropertyOrders[i]!, null, null, evaluations);
            }
        }

        if (defaultOrder.Count > 0)
        {
            return new EffectiveOrder(defaultOrder, null, null, evaluations);
        }

        if (property.ClassOrder is not null)
        {
            return new EffectiveOrder(property.ClassOrder, null, null, evaluations);
        }

        for (var i = inheritedClassOrders.Count - 1; i >= 0; i--)
        {
            if (inheritedClassOrders[i] is not null)
            {
                return new EffectiveOrder(inheritedClassOrders[i]!, null, null, evaluations);
            }
        }

        return new EffectiveOrder(defaultOrder, null, null, evaluations);
    }
}

internal sealed class ResolvedSettings<TSettings>
    where TSettings : SettingsBase, new()
{
    public ResolvedSettings(TSettings settings, IReadOnlyDictionary<string, ResolutionInfo> resolution)
    {
        Settings = settings;
        Resolution = resolution;
    }

    public TSettings Settings { get; }

    public IReadOnlyDictionary<string, ResolutionInfo> Resolution { get; }
}

internal sealed class EffectiveOrder
{
    public EffectiveOrder(
        IReadOnlyList<string> sourceOrder,
        string? appliedConditionProperty,
        bool? appliedConditionValue,
        IReadOnlyList<ConditionEvaluationInfo> conditionEvaluations)
    {
        SourceOrder = sourceOrder;
        AppliedConditionProperty = appliedConditionProperty;
        AppliedConditionValue = appliedConditionValue;
        ConditionEvaluations = conditionEvaluations;
    }

    public IReadOnlyList<string> SourceOrder { get; }

    public string? AppliedConditionProperty { get; }

    public bool? AppliedConditionValue { get; }

    public IReadOnlyList<ConditionEvaluationInfo> ConditionEvaluations { get; }
}
