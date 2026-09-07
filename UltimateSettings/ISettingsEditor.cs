using System.Linq.Expressions;

namespace UltimateSettings;

/// <summary>
/// Collects typed changes for one settings edit operation.
/// </summary>
public interface ISettingsEditor<TSettings>
    where TSettings : SettingsBase, new()
{
    /// <summary>
    /// Adds or replaces the value for a settings property in the current edit.
    /// </summary>
    void Set<TValue>(Expression<Func<TSettings, TValue>> property, TValue value);
}