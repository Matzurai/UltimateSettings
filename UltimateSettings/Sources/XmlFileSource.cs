using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace UltimateSettings.Sources;

/// <summary>
/// An XML file-backed <see cref="ISettingsSource"/> that stores each key as a child element of a root
/// "Settings" element, using <see cref="XmlSerializer"/> for persistence, and optionally watches for file
/// changes using a <see cref="FileSystemWatcher"/>.
/// </summary>
public sealed class XmlFileSource : IObservableSettingsSource, IDisposable
{
    private const string RootElementName = "Settings";

    private readonly object _lock = new();
    private readonly FileSystemWatcher? _watcher;
    private Dictionary<string, XElement>? _cache;
    private bool _isDisposed;

    public event EventHandler? SourceChanged;

    public XmlFileSource(string id, string filePath, bool canWrite = true, bool watchForChanges = false)
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

        if (targetType == typeof(object) || targetType == typeof(XElement))
        {
            value = element;
            return true;
        }

        try
        {
            var serializer = new XmlSerializer(targetType, new XmlRootAttribute(element.Name.LocalName));
            using var reader = element.CreateReader();
            value = serializer.Deserialize(reader);
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
        if (container is not XElement xElement)
        {
            child = null;
            return false;
        }

        var childElement = xElement.Elements().FirstOrDefault(e => string.Equals(e.Name.LocalName, propertyName, StringComparison.OrdinalIgnoreCase));
        child = childElement;
        return childElement is not null;
    }

    public bool TryNavigateLeaf(object container, string propertyName, Type targetType, out object? value)
    {
        if (container is not XElement xElement)
        {
            value = null;
            return false;
        }

        var childElement = xElement.Elements().FirstOrDefault(e => string.Equals(e.Name.LocalName, propertyName, StringComparison.OrdinalIgnoreCase));
        if (childElement is null)
        {
            value = null;
            return false;
        }

        if (targetType == typeof(object) || targetType == typeof(XElement))
        {
            value = childElement;
            return true;
        }

        try
        {
            var serializer = new XmlSerializer(targetType, new XmlRootAttribute(childElement.Name.LocalName));
            using var reader = childElement.CreateReader();
            value = serializer.Deserialize(reader);
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
            XElement root;

            if (File.Exists(FilePath))
            {
                try
                {
                    root = XElement.Load(FilePath);
                }
                catch
                {
                    root = new XElement(RootElementName);
                }
            }
            else
            {
                root = new XElement(RootElementName);
            }

            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            root.Elements(key).Remove();

            if (value is not null)
            {
                var serializer = new XmlSerializer(value.GetType(), new XmlRootAttribute(key));
                using var stringWriter = new StringWriter();
                using (var xmlWriter = XmlWriter.Create(stringWriter, new XmlWriterSettings { OmitXmlDeclaration = true }))
                {
                    serializer.Serialize(xmlWriter, value);
                }

                root.Add(XElement.Parse(stringWriter.ToString()));
            }

            // Temporarily pause watcher to prevent re-triggering reload on self-write.
            if (_watcher is not null)
            {
                _watcher.EnableRaisingEvents = false;
            }

            try
            {
                root.Save(FilePath);
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

    private Dictionary<string, XElement> EnsureCacheLoaded()
    {
        lock (_lock)
        {
            if (_cache is not null)
            {
                return _cache;
            }

            var dictionary = new Dictionary<string, XElement>(StringComparer.OrdinalIgnoreCase);

            if (File.Exists(FilePath))
            {
                try
                {
                    var root = XElement.Load(FilePath);
                    foreach (var element in root.Elements())
                    {
                        dictionary[element.Name.LocalName] = element;
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
