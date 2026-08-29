namespace UltimateSettings.Sources;

/// <summary>
/// A setting source that can notify listeners when its underlying storage has changed externally.
/// </summary>
public interface IObservableSettingsSource : ISettingsSource
{
    event EventHandler SourceChanged;
}
