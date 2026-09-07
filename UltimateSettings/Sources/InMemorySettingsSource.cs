namespace UltimateSettings.Sources;

/// <summary>
/// An in-memory <see cref="ISettingsSource"/> used to validate core resolution and write logic before
/// implementing real sources such as JSON files or the registry.
/// </summary>
public sealed class InMemorySettingsSource : IObservableSettingsSource
{
    private readonly Dictionary<string, object?> _values = new();

    public event EventHandler? SourceChanged;

    public InMemorySettingsSource(string id, bool canWrite = true, bool canRead = true)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Source id is required.", nameof(id));
        }

        Id = id;
        CanWrite = canWrite;
        CanRead = canRead;
    }

    public string Id { get; }

    public bool CanRead { get; }

    public bool CanWrite { get; }

    public bool TryRead(string key, out object? value)
    {
        return TryRead(key, typeof(object), out value);
    }

    public bool TryRead(string key, Type targetType, out object? value)
    {
        if (!CanRead)
        {
            value = null;
            return false;
        }

        return _values.TryGetValue(key, out value);
    }

    public void Write(string key, object? value)
    {
        WriteMany(new Dictionary<string, object?> { [key] = value });
    }

    public void WriteMany(IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (!CanWrite)
        {
            throw new InvalidOperationException($"Source '{Id}' is write-protected.");
        }

        foreach (var entry in values)
        {
            _values[entry.Key] = entry.Value;
        }
    }

    /// <summary>
    /// Populates a value directly, bypassing the write-protection check, to simulate data that already
    /// exists in the underlying storage before the settings manager starts.
    /// </summary>
    public void Seed(string key, object? value)
    {
        _values[key] = value;
    }

    /// <summary>
    /// Triggers the <see cref="SourceChanged"/> event to simulate an external change.
    /// </summary>
    public void TriggerSourceChanged()
    {
        SourceChanged?.Invoke(this, EventArgs.Empty);
    }
}
