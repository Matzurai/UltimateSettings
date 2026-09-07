using System.Linq.Expressions;
using System.Reflection;

namespace UltimateSettings.Internal;

internal sealed class SettingsEditor<TSettings> : ISettingsEditor<TSettings>
    where TSettings : SettingsBase, new()
{
    private readonly Dictionary<PropertyInfo, object?> _changes = new();

    public IReadOnlyList<SettingsChange> Changes =>
        _changes.Select(change => new SettingsChange(change.Key, change.Value)).ToArray();

    public void Set<TValue>(Expression<Func<TSettings, TValue>> property, TValue value)
    {
        ArgumentNullException.ThrowIfNull(property);

        var propertyInfo = PropertyAccessor.GetProperty(property);
        _changes[propertyInfo] = value;
    }
}

internal sealed record SettingsChange(PropertyInfo Property, object? Value);