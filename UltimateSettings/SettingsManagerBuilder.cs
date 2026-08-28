using UltimateSettings.Internal;
using UltimateSettings.Sources;

namespace UltimateSettings;

/// <summary>
/// Registers setting sources and default precedence for a settings type, validating the configuration
/// before building a usable <see cref="ISettingsManager{TSettings}"/>.
/// </summary>
public sealed class SettingsManagerBuilder<TSettings>
    where TSettings : SettingsBase, new()
{
    private readonly Dictionary<string, ISettingsSource> _sources = new(StringComparer.Ordinal);
    private IReadOnlyList<string> _defaultOrder = Array.Empty<string>();

    public SettingsManagerBuilder<TSettings> AddSource(string id, ISettingsSource source)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Source id is required.", nameof(id));
        }

        ArgumentNullException.ThrowIfNull(source);

        if (!_sources.TryAdd(id, source))
        {
            throw new InvalidOperationException($"A source with id '{id}' is already registered.");
        }

        return this;
    }

    public SettingsManagerBuilder<TSettings> WithDefaultOrder(params string[] sourceIds)
    {
        _defaultOrder = sourceIds;
        return this;
    }

    public ISettingsManager<TSettings> Build()
    {
        var referencedIds = SettingsTypeMetadata<TSettings>.AllReferencedSourceIds;

        foreach (var id in referencedIds)
        {
            if (!_sources.ContainsKey(id))
            {
                throw new InvalidOperationException(
                    $"'{typeof(TSettings).Name}' references source id '{id}' via SourceOrder/SourceOrderIf, but no such source is registered.");
            }
        }

        foreach (var id in _defaultOrder)
        {
            if (!_sources.ContainsKey(id))
            {
                throw new InvalidOperationException($"Default order references unknown source id '{id}'.");
            }
        }

        return new SettingsManager<TSettings>(_sources, _defaultOrder);
    }
}
