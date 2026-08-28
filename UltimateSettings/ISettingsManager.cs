using System.Linq.Expressions;

namespace UltimateSettings;

/// <summary>
/// Resolves a strongly typed settings instance from registered sources and applies targeted writes.
/// </summary>
public interface ISettingsManager<TSettings>
    where TSettings : SettingsBase, new()
{
    /// <summary>
    /// The most recently resolved settings snapshot.
    /// </summary>
    TSettings Current { get; }

    /// <summary>
    /// Re-resolves settings from all registered sources and updates <see cref="Current"/>.
    /// </summary>
    TSettings Load();

    /// <summary>
    /// Writes a value to a specific registered source and refreshes <see cref="Current"/>.
    /// </summary>
    void Save<TValue>(Expression<Func<TSettings, TValue>> property, TValue value, string sourceId);
}
