using System.Runtime.Versioning;
using System.Runtime.InteropServices;
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

    public IDisposable Watch(string keyPath, bool includeSubkeys, Action changed)
    {
        ArgumentNullException.ThrowIfNull(changed);

        var (hive, subPath) = SplitPath(keyPath);
        var registryKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default).OpenSubKey(subPath)
            ?? throw new InvalidOperationException($"Unable to open registry key '{keyPath}' for watching.");

        return new RegistryWatch(registryKey, includeSubkeys, changed);
    }

    private sealed class RegistryWatch : IDisposable
    {
        private const uint ErrorSuccess = 0;
        private const uint NotifyChangeName = 0x00000001;
        private const uint NotifyChangeAttributes = 0x00000002;
        private const uint NotifyChangeLastSet = 0x00000004;
        private const uint NotifyChangeSecurity = 0x00000008;

        private readonly RegistryKey _registryKey;
        private readonly bool _includeSubkeys;
        private readonly Action _changed;
        private readonly AutoResetEvent _changeEvent = new(false);
        private readonly CancellationTokenSource _cancellation = new();
        private readonly Task _watchTask;
        private int _disposed;

        public RegistryWatch(RegistryKey registryKey, bool includeSubkeys, Action changed)
        {
            _registryKey = registryKey;
            _includeSubkeys = includeSubkeys;
            _changed = changed;
            _watchTask = Task.Run(WatchLoop);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _cancellation.Cancel();
            _changeEvent.Set();
            _watchTask.GetAwaiter().GetResult();
            _cancellation.Dispose();
            _changeEvent.Dispose();
            _registryKey.Dispose();
        }

        private void WatchLoop()
        {
            var waitHandles = new WaitHandle[] { _changeEvent, _cancellation.Token.WaitHandle };
            while (!_cancellation.IsCancellationRequested)
            {
                var result = RegNotifyChangeKeyValue(
                    _registryKey.Handle.DangerousGetHandle(),
                    _includeSubkeys,
                    NotifyChangeName | NotifyChangeAttributes | NotifyChangeLastSet | NotifyChangeSecurity,
                    _changeEvent.SafeWaitHandle.DangerousGetHandle(),
                    true);

                if (result != ErrorSuccess)
                {
                    throw new InvalidOperationException(
                        $"Unable to watch registry key. RegNotifyChangeKeyValue returned error code {result}.");
                }

                if (WaitHandle.WaitAny(waitHandles) == 1)
                {
                    return;
                }

                if (!_cancellation.IsCancellationRequested)
                {
                    _changed();
                }
            }
        }

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint RegNotifyChangeKeyValue(
            IntPtr keyHandle,
            [MarshalAs(UnmanagedType.Bool)] bool watchSubtree,
            uint notifyFilter,
            IntPtr eventHandle,
            [MarshalAs(UnmanagedType.Bool)] bool asynchronous);
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
