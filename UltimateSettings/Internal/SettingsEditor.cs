using System.Linq.Expressions;
using System.Reflection;
namespace UltimateSettings.Internal;

internal sealed class SettingsEditor<TSettings> : ISettingsEditor<TSettings>
    where TSettings : SettingsBase, new()
{
    private readonly Dictionary<string, object?> _changes = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, object?> Changes => _changes;

    internal IReadOnlyCollection<PropertyInfo> Properties => _properties;

    private readonly HashSet<PropertyInfo> _properties = new();

    public void Set<TValue>(Expression<Func<TSettings, TValue>> property, TValue value)
    {
        ArgumentNullException.ThrowIfNull(property);

        var propertyInfo = PropertyAccessor.GetProperty(property);
        _changes[propertyInfo.Name] = value;
        _properties.Add(propertyInfo);
    }
}