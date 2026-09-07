using System.Linq.Expressions;
namespace UltimateSettings.Internal;

internal sealed class SettingsEditor<TSettings> : ISettingsEditor<TSettings>
    where TSettings : SettingsBase, new()
{
    private readonly Dictionary<string, object?> _changes = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, object?> Changes => _changes;

    public void Set<TValue>(Expression<Func<TSettings, TValue>> property, TValue value)
    {
        ArgumentNullException.ThrowIfNull(property);

        var propertyInfo = PropertyAccessor.GetProperty(property);
        _changes[propertyInfo.Name] = value;
    }
}