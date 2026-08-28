using System.Linq.Expressions;
using UltimateSettings.Internal;
using UltimateSettings.Sources;

namespace UltimateSettings;

internal sealed class SettingsManager<TSettings> : ISettingsManager<TSettings>
    where TSettings : SettingsBase, new()
{
    private readonly IReadOnlyDictionary<string, ISettingsSource> _sources;
    private readonly IReadOnlyList<string> _defaultOrder;

    internal SettingsManager(IReadOnlyDictionary<string, ISettingsSource> sources, IReadOnlyList<string> defaultOrder)
    {
        _sources = sources;
        _defaultOrder = defaultOrder;
        Current = SettingsResolver<TSettings>.Resolve(_sources, _defaultOrder);
    }

    public TSettings Current { get; private set; }

    public TSettings Load()
    {
        Current = SettingsResolver<TSettings>.Resolve(_sources, _defaultOrder);
        return Current;
    }

    public void Save<TValue>(Expression<Func<TSettings, TValue>> property, TValue value, string sourceId)
    {
        var propertyInfo = PropertyAccessor.GetProperty(property);

        if (!_sources.TryGetValue(sourceId, out var source))
        {
            throw new ArgumentException($"No source registered with id '{sourceId}'.", nameof(sourceId));
        }

        if (!source.CanWrite)
        {
            throw new InvalidOperationException($"Source '{sourceId}' is write-protected.");
        }

        source.Write(propertyInfo.Name, value);
        Load();
    }
}
