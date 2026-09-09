namespace UltimateSettings.Sources;

/// <summary>
/// Minimal key/value storage abstraction used by <see cref="RegistrySource"/>. Lets the real Windows
/// registry (<see cref="WindowsRegistryBackend"/>) be swapped for an in-memory fake
/// (<see cref="InMemoryRegistryBackend"/>) so <see cref="RegistrySource"/> can be developed and tested on
/// any platform, including ones without a real registry.
/// </summary>
public interface IRegistryBackend
{
    /// <summary>Whether a subkey exists at the given path.</summary>
    bool SubKeyExists(string keyPath);

    /// <summary>Creates the subkey at the given path if it does not already exist.</summary>
    void EnsureSubKey(string keyPath);

    /// <summary>Reads a named value from the subkey at the given path.</summary>
    bool TryGetValue(string keyPath, string valueName, out object? value);

    /// <summary>Writes (or, if <paramref name="value"/> is <see langword="null"/>, deletes) a named value in the subkey at the given path.</summary>
    void SetValue(string keyPath, string valueName, object? value);

    /// <summary>
    /// Watches a registry key and its descendants for value, key, and security changes.
    /// The returned registration must be disposed to stop watching.
    /// </summary>
    IDisposable Watch(string keyPath, bool includeSubkeys, Action changed);
}
