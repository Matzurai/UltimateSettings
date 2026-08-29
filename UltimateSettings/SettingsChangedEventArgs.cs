namespace UltimateSettings;

/// <summary>
/// Event arguments raised when <see cref="ISettingsManager{TSettings}.Current"/> is updated.
/// </summary>
public sealed class SettingsChangedEventArgs<TSettings> : EventArgs
    where TSettings : SettingsBase, new()
{
    public SettingsChangedEventArgs(TSettings previous, TSettings current)
    {
        Previous = previous;
        Current = current;
    }

    public TSettings Previous { get; }

    public TSettings Current { get; }
}
