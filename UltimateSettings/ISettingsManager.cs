namespace UltimateSettings;

/// <summary>
/// Resolves a strongly typed settings instance from registered sources and applies targeted writes.
/// </summary>
public interface ISettingsManager<TSettings> : IDisposable
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
    /// Applies one or more changes to a specific registered source and refreshes <see cref="Current"/> once.
    /// </summary>
    void Edit(string sourceId, Action<ISettingsEditor<TSettings>> edit);

    /// <summary>
    /// Event raised when settings are successfully reloaded and <see cref="Current"/> is updated.
    /// </summary>
    event EventHandler<SettingsChangedEventArgs<TSettings>>? SettingsChanged;

    /// <summary>
    /// Event raised when a settings reload candidate fails validation or encounters an exception.
    /// </summary>
    event EventHandler<SettingsReloadRejectedEventArgs>? SettingsReloadRejected;
}
