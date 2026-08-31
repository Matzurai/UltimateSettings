using System.Reflection;
using System.Text.Json;
using UltimateSettings.Internal;

namespace UltimateSettings.Sources;

/// <summary>
/// A registry-backed <see cref="ISettingsSource"/> that stores each top-level property as a named value
/// under a base registry key, and stores nested settings objects as subkeys so they can be navigated and
/// resolved field by field. Storage access goes through an injectable <see cref="IRegistryBackend"/>, which
/// defaults to <see cref="WindowsRegistryBackend"/> but can be an <see cref="InMemoryRegistryBackend"/> to
/// develop or test against on any platform, including ones without a real registry.
/// </summary>
public sealed class RegistrySource : ISettingsSource
{
    private readonly IRegistryBackend _backend;

    public RegistrySource(string id, string keyPath, bool canWrite = true, IRegistryBackend? backend = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Source id is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(keyPath))
        {
            throw new ArgumentException("Registry key path is required.", nameof(keyPath));
        }

        Id = id;
        KeyPath = keyPath;
        CanWrite = canWrite;
        CanRead = true;
        _backend = backend ?? CreateDefaultBackend();
    }

    // Intentionally constructible on any OS; only throws if actually used off Windows, matching Microsoft.Win32.Registry's own behavior.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416", Justification = "Constructing WindowsRegistryBackend is harmless off Windows; only using it throws.")]
    private static IRegistryBackend CreateDefaultBackend()
    {
        return new WindowsRegistryBackend();
    }

    public string Id { get; }

    public string KeyPath { get; }

    public bool CanRead { get; }

    public bool CanWrite { get; }

    public bool TryRead(string key, out object? value)
    {
        return TryRead(key, typeof(object), out value);
    }

    public bool TryRead(string key, Type targetType, out object? value)
    {
        if (targetType == typeof(object))
        {
            var subKeyPath = CombinePath(KeyPath, key);
            if (_backend.SubKeyExists(subKeyPath))
            {
                value = new RegistryContainer(subKeyPath);
                return true;
            }
        }

        if (_backend.TryGetValue(KeyPath, key, out var raw))
        {
            return TryDecodeLeafValue(raw, targetType, out value);
        }

        value = null;
        return false;
    }

    public bool TryNavigate(object container, string propertyName, out object? child)
    {
        if (container is not RegistryContainer registryContainer)
        {
            child = null;
            return false;
        }

        var childPath = CombinePath(registryContainer.KeyPath, propertyName);
        if (_backend.SubKeyExists(childPath))
        {
            child = new RegistryContainer(childPath);
            return true;
        }

        child = null;
        return false;
    }

    public bool TryNavigateLeaf(object container, string propertyName, Type targetType, out object? value)
    {
        if (container is RegistryContainer registryContainer && _backend.TryGetValue(registryContainer.KeyPath, propertyName, out var raw))
        {
            return TryDecodeLeafValue(raw, targetType, out value);
        }

        value = null;
        return false;
    }

    public void Write(string key, object? value)
    {
        if (!CanWrite)
        {
            throw new InvalidOperationException($"Source '{Id}' is write-protected.");
        }

        if (value is not null && SettingsTypeMetadataCache.IsNestedSettingsType(value.GetType()))
        {
            WriteNestedObject(CombinePath(KeyPath, key), value);
            return;
        }

        _backend.SetValue(KeyPath, key, EncodeLeafValue(value));
    }

    private void WriteNestedObject(string subKeyPath, object nestedInstance)
    {
        _backend.EnsureSubKey(subKeyPath);

        var properties = nestedInstance.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead);

        foreach (var property in properties)
        {
            var propertyValue = property.GetValue(nestedInstance);

            if (propertyValue is not null && SettingsTypeMetadataCache.IsNestedSettingsType(propertyValue.GetType()))
            {
                WriteNestedObject(CombinePath(subKeyPath, property.Name), propertyValue);
            }
            else
            {
                _backend.SetValue(subKeyPath, property.Name, EncodeLeafValue(propertyValue));
            }
        }
    }

    private static string CombinePath(string basePath, string child)
    {
        return $"{basePath}\\{child}";
    }

    // The registry natively supports only a handful of value shapes; encode everything else as a JSON
    // string so it can round-trip through TryDecodeLeafValue.
    private static object? EncodeLeafValue(object? value)
    {
        return value switch
        {
            null => null,
            bool boolValue => boolValue ? 1 : 0,
            Enum enumValue => enumValue.ToString(),
            string or string[] or int or long or byte[] => value,
            _ => JsonSerializer.Serialize(value)
        };
    }

    private static bool TryDecodeLeafValue(object? rawValue, Type targetType, out object? value)
    {
        if (rawValue is null)
        {
            value = null;
            return false;
        }

        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (underlyingType.IsInstanceOfType(rawValue))
        {
            value = rawValue;
            return true;
        }

        if (underlyingType == typeof(bool) && rawValue is int intValue)
        {
            value = intValue != 0;
            return true;
        }

        if (underlyingType.IsEnum && rawValue is string enumString)
        {
            value = Enum.Parse(underlyingType, enumString, ignoreCase: true);
            return true;
        }

        if (rawValue is string jsonString)
        {
            try
            {
                value = JsonSerializer.Deserialize(jsonString, targetType);
                return true;
            }
            catch
            {
                value = null;
                return false;
            }
        }

        try
        {
            value = Convert.ChangeType(rawValue, underlyingType);
            return true;
        }
        catch
        {
            value = null;
            return false;
        }
    }

    /// <summary>Wraps the path of a subkey that represents a nested settings object, for navigation via <see cref="TryNavigate"/>/<see cref="TryNavigateLeaf"/>.</summary>
    private sealed class RegistryContainer
    {
        public RegistryContainer(string keyPath)
        {
            KeyPath = keyPath;
        }

        public string KeyPath { get; }
    }
}
