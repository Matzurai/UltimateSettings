namespace UltimateSettings.Sources;

/// <summary>
/// A named location that can supply and optionally persist setting values, such as a JSON file or the registry.
/// </summary>
public interface ISettingsSource
{
    string Id { get; }

    bool CanRead { get; }

    bool CanWrite { get; }

    bool TryRead(string key, out object? value);

    void Write(string key, object? value);
}
