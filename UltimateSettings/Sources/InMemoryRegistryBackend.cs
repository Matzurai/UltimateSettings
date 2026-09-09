namespace UltimateSettings.Sources;

/// <summary>
/// An in-memory <see cref="IRegistryBackend"/> that simulates registry subkeys and values without touching
/// any real registry. Useful for developing and testing <see cref="RegistrySource"/> on any platform,
/// including non-Windows systems where the real registry is unavailable.
/// </summary>
public sealed class InMemoryRegistryBackend : IRegistryBackend
{
    private readonly Dictionary<string, Dictionary<string, object?>> _keys = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<Action>> _watchers = new(StringComparer.OrdinalIgnoreCase);

    public bool SubKeyExists(string keyPath)
    {
        return _keys.ContainsKey(keyPath);
    }

    public void EnsureSubKey(string keyPath)
    {
        if (!_keys.ContainsKey(keyPath))
        {
            _keys[keyPath] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public bool TryGetValue(string keyPath, string valueName, out object? value)
    {
        if (_keys.TryGetValue(keyPath, out var values) && values.TryGetValue(valueName, out value))
        {
            return true;
        }

        value = null;
        return false;
    }

    public void SetValue(string keyPath, string valueName, object? value)
    {
        EnsureSubKey(keyPath);
        var values = _keys[keyPath];

        if (value is null)
        {
            values.Remove(valueName);
        }
        else
        {
            values[valueName] = value;
        }
    }

    public IDisposable Watch(string keyPath, bool includeSubkeys, Action changed)
    {
        ArgumentNullException.ThrowIfNull(changed);

        if (!_watchers.TryGetValue(keyPath, out var callbacks))
        {
            callbacks = new List<Action>();
            _watchers[keyPath] = callbacks;
        }

        callbacks.Add(changed);
        return new WatchRegistration(() => callbacks.Remove(changed));
    }

    public void TriggerChange(string keyPath)
    {
        foreach (var entry in _watchers.ToArray())
        {
            if (!IsWatchedPath(entry.Key, keyPath))
            {
                continue;
            }

            foreach (var callback in entry.Value.ToArray())
            {
                callback();
            }
        }
    }

    private static bool IsWatchedPath(string watchedPath, string changedPath)
    {
        return string.Equals(watchedPath, changedPath, StringComparison.OrdinalIgnoreCase)
            || changedPath.StartsWith(watchedPath + "\\", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class WatchRegistration : IDisposable
    {
        private Action? _dispose;

        public WatchRegistration(Action dispose)
        {
            _dispose = dispose;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _dispose, null)?.Invoke();
        }
    }
}
