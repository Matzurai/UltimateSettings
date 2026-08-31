using System.Text.Json;
using System.Text.Json.Nodes;

namespace UltimateSettings.Sources;

/// <summary>
/// A JSON file-backed <see cref="ISettingsSource"/> that uses <see cref="System.Text.Json"/> for persistence
/// and optionally watches for file changes using a <see cref="FileSystemWatcher"/>.
/// </summary>
public sealed class JsonFileSource : IObservableSettingsSource, IDisposable
{
    private static readonly JsonSerializerOptions DefaultJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly object _lock = new();
    private readonly JsonSerializerOptions _options;
    private readonly FileSystemWatcher? _watcher;
    private Dictionary<string, JsonElement>? _cache;
    private bool _isDisposed;

    public event EventHandler? SourceChanged;

    public JsonFileSource(string id, string filePath, bool canWrite = true, bool watchForChanges = false, JsonSerializerOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Source id is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path is required.", nameof(filePath));
        }

        Id = id;
        FilePath = Path.GetFullPath(filePath);
        CanWrite = canWrite;
        CanRead = true;
        _options = options ?? DefaultJsonOptions;

        if (watchForChanges)
        {
            var directory = Path.GetDirectoryName(FilePath);
            var fileName = Path.GetFileName(FilePath);

            if (!string.IsNullOrEmpty(directory) && !string.IsNullOrEmpty(fileName))
            {
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                _watcher = new FileSystemWatcher(directory, fileName)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size
                };

                _watcher.Changed += OnFileEvent;
                _watcher.Created += OnFileEvent;
                _watcher.Renamed += OnFileEvent;
                _watcher.EnableRaisingEvents = true;
            }
        }
    }

    public string Id { get; }

    public string FilePath { get; }

    public bool CanRead { get; }

    public bool CanWrite { get; }

    public bool TryRead(string key, out object? value)
    {
        return TryRead(key, typeof(object), out value);
    }

    public bool TryRead(string key, Type targetType, out object? value)
    {
        var cache = EnsureCacheLoaded();

        if (!cache.TryGetValue(key, out var element))
        {
            value = null;
            return false;
        }

        if (targetType == typeof(object) || targetType == typeof(JsonElement))
        {
            value = element;
            return true;
        }

        try
        {
            value = JsonSerializer.Deserialize(element, targetType, _options);
            return true;
        }
        catch
        {
            value = null;
            return false;
        }
    }

    public bool TryNavigate(object container, string propertyName, out object? child)
    {
        if (container is JsonElement jsonElement
            && jsonElement.ValueKind == JsonValueKind.Object
            && jsonElement.TryGetProperty(propertyName, out var childElement))
        {
            child = childElement;
            return true;
        }

        child = null;
        return false;
    }

    public bool TryNavigateLeaf(object container, string propertyName, Type targetType, out object? value)
    {
        if (container is not JsonElement jsonElement
            || jsonElement.ValueKind != JsonValueKind.Object
            || !jsonElement.TryGetProperty(propertyName, out var childElement))
        {
            value = null;
            return false;
        }

        if (targetType == typeof(object) || targetType == typeof(JsonElement))
        {
            value = childElement;
            return true;
        }

        try
        {
            value = JsonSerializer.Deserialize(childElement, targetType, _options);
            return true;
        }
        catch
        {
            value = null;
            return false;
        }
    }

    public void Write(string key, object? value)
    {
        if (!CanWrite)
        {
            throw new InvalidOperationException($"Source '{Id}' is write-protected.");
        }

        lock (_lock)
        {
            JsonObject root;

            if (File.Exists(FilePath))
            {
                try
                {
                    var existingJson = File.ReadAllText(FilePath);
                    root = JsonNode.Parse(existingJson)?.AsObject() ?? new JsonObject();
                }
                catch
                {
                    root = new JsonObject();
                }
            }
            else
            {
                root = new JsonObject();
            }

            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var jsonNode = JsonSerializer.SerializeToNode(value, _options);
            root[key] = jsonNode;

            var updatedJson = root.ToJsonString(_options);

            // Temporarily pause watcher to prevent re-triggering reload on self-write.
            if (_watcher is not null)
            {
                _watcher.EnableRaisingEvents = false;
            }

            try
            {
                File.WriteAllText(FilePath, updatedJson);
            }
            finally
            {
                if (_watcher is not null && !_isDisposed)
                {
                    _watcher.EnableRaisingEvents = true;
                }
            }

            _cache = null;
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Changed -= OnFileEvent;
            _watcher.Created -= OnFileEvent;
            _watcher.Renamed -= OnFileEvent;
            _watcher.Dispose();
        }
    }

    private Dictionary<string, JsonElement> EnsureCacheLoaded()
    {
        lock (_lock)
        {
            if (_cache is not null)
            {
                return _cache;
            }

            var dictionary = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

            if (File.Exists(FilePath))
            {
                try
                {
                    var json = File.ReadAllText(FilePath);
                    using var document = JsonDocument.Parse(json);

                    if (document.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var property in document.RootElement.EnumerateObject())
                        {
                            dictionary[property.Name] = property.Value.Clone();
                        }
                    }
                }
                catch
                {
                    // Ignore corrupted or empty file reads gracefully.
                }
            }

            _cache = dictionary;
            return _cache;
        }
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        lock (_lock)
        {
            _cache = null;
        }

        SourceChanged?.Invoke(this, EventArgs.Empty);
    }
}
