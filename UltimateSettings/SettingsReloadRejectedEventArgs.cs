namespace UltimateSettings;

/// <summary>
/// Event arguments raised when a settings reload fails validation or encounters an unhandled exception.
/// </summary>
public sealed class SettingsReloadRejectedEventArgs : EventArgs
{
    public SettingsReloadRejectedEventArgs(string? error, Exception? exception = null)
    {
        Error = error;
        Exception = exception;
    }

    public string? Error { get; }

    public Exception? Exception { get; }
}
