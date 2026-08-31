using System.Runtime.Versioning;
using Microsoft.Win32;

namespace UltimateSettings.Sources;

/// <summary>
/// An <see cref="IRegistryBackend"/> that reads and writes the real Windows registry. Only functional on
/// Windows; construct <see cref="RegistrySource"/> with an <see cref="InMemoryRegistryBackend"/> instead
/// to develop or test its behavior on any platform.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsRegistryBackend : IRegistryBackend
{
    public bool SubKeyExists(string keyPath)
    {
        var (hive, subPath) = SplitPath(keyPath);
        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
        using var subKey = baseKey.OpenSubKey(subPath);
        return subKey is not null;
    }

    public void EnsureSubKey(string keyPath)
    {
        var (hive, subPath) = SplitPath(keyPath);
        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
        using var subKey = baseKey.CreateSubKey(subPath);
    }

    public bool TryGetValue(string keyPath, string valueName, out object? value)
    {
        var (hive, subPath) = SplitPath(keyPath);
        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
        using var subKey = baseKey.OpenSubKey(subPath);

        if (subKey is null)
        {
            value = null;
            return false;
        }

        value = subKey.GetValue(valueName);
        return value is not null;
    }

    public void SetValue(string keyPath, string valueName, object? value)
    {
        var (hive, subPath) = SplitPath(keyPath);
        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
        using var subKey = baseKey.CreateSubKey(subPath)
            ?? throw new InvalidOperationException($"Unable to create or open registry key '{keyPath}'.");

        if (value is null)
        {
            subKey.DeleteValue(valueName, throwOnMissingValue: false);
        }
        else
        {
            subKey.SetValue(valueName, value);
        }
    }

    private static (RegistryHive Hive, string SubPath) SplitPath(string keyPath)
    {
        var separatorIndex = keyPath.IndexOf('\\');
        var hiveName = separatorIndex < 0 ? keyPath : keyPath[..separatorIndex];
        var subPath = separatorIndex < 0 ? string.Empty : keyPath[(separatorIndex + 1)..];

        var hive = hiveName.ToUpperInvariant() switch
        {
            "HKEY_CURRENT_USER" or "HKCU" => RegistryHive.CurrentUser,
            "HKEY_LOCAL_MACHINE" or "HKLM" => RegistryHive.LocalMachine,
            "HKEY_CLASSES_ROOT" or "HKCR" => RegistryHive.ClassesRoot,
            "HKEY_USERS" or "HKU" => RegistryHive.Users,
            "HKEY_CURRENT_CONFIG" or "HKCC" => RegistryHive.CurrentConfig,
            _ => throw new ArgumentException($"Unknown registry hive '{hiveName}' in path '{keyPath}'.", nameof(keyPath))
        };

        return (hive, subPath);
    }
}
