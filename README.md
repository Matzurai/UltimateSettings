# UltimateSettings

A reusable, strongly typed settings system for .NET that reads from multiple configurable sources with deterministic precedence, source-level write capabilities, and per-setting policy control.

## Features

✨ **Strongly Typed Settings** - Define your settings as plain C# classes with full type safety and compile-time checking.

🎯 **Multi-Source Resolution** - Read settings from multiple sources (files, registry, custom backends) with predictable precedence rules.

🔒 **Policy Control** - Define which settings can be overridden at different levels (user, machine, group policy, etc.).

🔄 **Hot Reload Support** - Optionally watch for external changes and automatically reload settings with validation.

✅ **Validation** - Apply custom validation logic to ensure settings remain in a valid state after changes.

📝 **Flexible Source Ordering** - Set a global default source order, override per-setting, or use conditional rules based on other settings.

🔌 **Extensible** - Implement custom settings sources without changing core library code.

📦 **Built-in Sources** - JSON files, XML files, Windows Registry, and in-memory storage included.

## Getting Started

### Basic Setup

Define your settings class by inheriting from `SettingsBase`:

```csharp
using UltimateSettings;
using UltimateSettings.Attributes;

public class MyScopes
{
    public const string userScope = "user";
    public const string machineScope = "machine";
    public const string domainScope = "GroupPolicy";
}

[SourceOrder("User", "Machine")]  // default precedence for this class.
public class AppSettings : SettingsBase
{
    public int FontSize { get; set; } = 12;
    
    public string Theme { get; set; } = "Light";
    
    [SourceOrder("Machine", "GroupPolicy")]  // Override: Machine sources take priority
    public string[] AllowedEncryptionMethods { get; set; } = Array.Empty<string>();
}
```

### Create and Use a Settings Manager

```csharp
using UltimateSettings;
using UltimateSettings.Sources;

// Create settings sources
var userSource = new JsonFileSource("User", "appsettings.user.json");
var machineSource = new JsonFileSource("Machine", "appsettings.machine.json");

// Build the settings manager
var manager = new SettingsManagerBuilder<AppSettings>()
    .AddSource("User", userSource)
    .AddSource("Machine", machineSource)
    //.WithDefaultOrder("User", "Machine") // You can override the classes default SourceOrder here. Usefull, if you need a different order in different scenarios. 
    .Build();

// Read the resolved settings
var settings = manager.Current;
Console.WriteLine($"Font Size: {settings.FontSize}");

// Watch for changes
manager.SettingsChanged += (sender, args) => 
{
    Console.WriteLine("Settings reloaded!");
};

// Manually reload
var newSettings = manager.Load();

// Write a setting to a specific source
manager.Edit("User", edit => edit.Set(s => s.FontSize, 14));
```

## Configuration Attributes

### `SourceOrder`

Defines the precedence order for a setting. Can be applied at class or property level.

```csharp
// Class-level: applies to all properties without explicit property-level order
[SourceOrder("User", "Machine", "GroupPolicy")]
public class AppSettings : SettingsBase
{
    // Property-level: overrides class-level order for this property
    [SourceOrder("Machine", "GroupPolicy")]
    public string[] LockdownSettings { get; set; } = Array.Empty<string>();
}
```

### `SourceOrderIf`

Conditionally override the source order based on another setting's value.

```csharp
[SourceOrder("Machine", "User")]
public class AppSettings : SettingsBase
{
    public bool AllowUserOverrides { get; set; }
    
    // If AllowUserOverrides is true, User source wins; otherwise Machine wins
    [SourceOrderIf(nameof(AllowUserOverrides), "User", "Machine")]
    public string ProxyAddress { get; set; } = string.Empty;
}
```

### `SuppressArrayMergeWarning`

Suppresses build warnings about array merging behavior in nested settings objects.
Nested settings will get resolved in most cases (dictionary, or named properties), but arrays would cause undefined behavior. Thus, arrays will be completely overwritten by the winning source.

```csharp
public class AppSettings : SettingsBase
{
    [SuppressArrayMergeWarning]
    public string[] Items { get; set; } = Array.Empty<string>();
}
```

### `WritableTo`

Configure, to which sources the setting can be written to. When trying to write to any other source, InvalidOperationException is thrown. Defaults to all available sources.

```csharp
public class AppSettings : SettingsBase
{
    [WritableTo("User", "Machine")]
    public string Item { get; set; } = "";
}
```

### `Readonly`

Marks the setting as readonly.

```csharp
public class AppSettings : SettingsBase
{
    [Readonly]
    public string Item { get; set; } = "";
}
```





## Built-in Sources

### JSON File Source

Read/write settings from JSON files:

```csharp
var source = new JsonFileSource("User", "config.json");
```

### XML File Source

Read/write settings from XML files:

```csharp
var source = new XmlFileSource("Machine", "settings.xml");
```

### Windows Registry Source

Read/write settings from the Windows Registry (Windows only):

```csharp
var source = new RegistrySource("Registry", RegistryHive.LocalMachine, 
    @"Software\MyCompany\MyApp");
```

### In-Memory Source

Useful for testing or temporary overrides:

```csharp
var source = new InMemorySettingsSource("Test");
source.Seed("FontSize", 16);
```

## Advanced Usage

### Custom Settings Sources

Implement `ISettingsSource` to create a custom source:

```csharp
using System.Reflection;

public class CustomSource : ISettingsSource
{
    private readonly Dictionary<string, object?> _store = new();
    
    public string Id { get; }
    public bool CanRead { get; }
    public bool CanWrite { get; }
    
    public CustomSource(string id, bool canRead = true, bool canWrite = true)
    {
        Id = id;
        CanRead = canRead;
        CanWrite = canWrite;
    }
    
    /// <summary>
    /// Try to read a value without type information.
    /// </summary>
    public bool TryRead(string key, out object? value)
    {
        return _store.TryGetValue(key, out value);
    }
    
    /// <summary>
    /// Try to read a value and coerce it to the target type.
    /// </summary>
    public bool TryRead(string key, Type targetType, out object? value)
    {
        if (!_store.TryGetValue(key, out var storedValue))
        {
            value = null;
            return false;
        }
        
        try
        {
            // Simple type coercion; real implementations may need more sophisticated handling
            value = storedValue != null && targetType.IsAssignableFrom(storedValue.GetType())
                ? storedValue
                : Convert.ChangeType(storedValue, targetType);
            return true;
        }
        catch
        {
            value = null;
            return false;
        }
    }
    
    /// <summary>
    /// Write all values from one edit to this source.
    /// </summary>
    public void WriteMany(IReadOnlyDictionary<string, object?> values)
    {
        if (!CanWrite)
        {
            throw new InvalidOperationException($"Source '{Id}' does not support write operations.");
        }

        foreach (var entry in values)
        {
            _store[entry.Key] = entry.Value;
        }
    }
    
    /// <summary>
    /// Optional: Navigate one level into nested objects.
    /// The interface provides a default reflection-based implementation;
    /// override this if your source uses a custom object representation (e.g., JSON documents, XML nodes).
    /// </summary>
    public bool TryNavigate(object container, string propertyName, out object? child)
    {
        var propertyInfo = container.GetType().GetProperty(propertyName, 
            BindingFlags.Public | BindingFlags.Instance);
        if (propertyInfo is null)
        {
            child = null;
            return false;
        }

        child = propertyInfo.GetValue(container);
        return child is not null;
    }
    
    /// <summary>
    /// Optional: Navigate to a leaf property and coerce to target type.
    /// The interface provides a default reflection-based implementation;
    /// override this if your source uses a custom object representation.
    /// </summary>
    public bool TryNavigateLeaf(object container, string propertyName, Type targetType, out object? value)
    {
        var propertyInfo = container.GetType().GetProperty(propertyName,
            BindingFlags.Public | BindingFlags.Instance);
        if (propertyInfo is null)
        {
            value = null;
            return false;
        }

        value = propertyInfo.GetValue(container);
        return true;
    }
    
    // Helper method to seed test data
    public void Seed(string key, object? value)
    {
        _store[key] = value;
    }
}

// Register and use
var manager = new SettingsManagerBuilder<AppSettings>()
    .AddSource("Custom", new CustomSource("Custom"))
    .WithDefaultOrder("Custom")
    .Build();
```

**Key Methods:**

- `TryRead(string key, out object? value)` - Read without type information. Return `true` if found, `false` otherwise.
- `TryRead(string key, Type targetType, out object? value)` - Read and coerce to the target type.
- `Write(string key, object? value)` - Persist a value. Should throw if `CanWrite` is `false`.
- `TryNavigate(object container, string propertyName, out object? child)` - Navigate one level into nested objects. Optional to override; default implementation uses reflection.
- `TryNavigateLeaf(object container, string propertyName, Type targetType, out object? value)` - Navigate to a leaf property and coerce. Optional to override; default implementation uses reflection.

### Observable/Watchable Sources

Make a source watchable for changes by implementing `IObservableSettingsSource`:

```csharp
public class WatchableSource : IObservableSettingsSource
{
    public event EventHandler? SourceChanged;
    
    // ... other ISettingsSource implementation
    
    private void OnFileChanged()
    {
        SourceChanged?.Invoke(this, EventArgs.Empty);
    }
}

// Enable watching
var manager = new SettingsManagerBuilder<AppSettings>()
    .AddSource("Watched", new WatchableSource(), watchForChanges: true)
    .Build();
```

### Settings Validation

Validate settings after they are loaded:

```csharp
var isValid = (AppSettings settings, out string? error) =>
{
    if (settings.FontSize < 8 || settings.FontSize > 72)
    {
        error = "FontSize must be between 8 and 72";
        return false;
    }
    
    if (string.IsNullOrEmpty(settings.Theme))
    {
        error = "Theme cannot be empty";
        return false;
    }
    
    error = null;
    return true;
};

var manager = new SettingsManagerBuilder<AppSettings>()
    .AddSource("User", userSource)
    .AddSource("Machine", machineSource)
    .WithDefaultOrder("User", "Machine")
    .WithValidator(isValid)
    .Build();
```

### Handling Reload Failures

When validation fails during a reload, the `SettingsReloadRejected` event is raised:

```csharp
manager.SettingsChanged += (sender, args) => 
{
    Console.WriteLine($"Settings changed. Previous FontSize: {args.Previous.FontSize}, " +
                      $"New FontSize: {args.Current.FontSize}");
};

manager.SettingsReloadRejected += (sender, args) =>
{
    Console.WriteLine($"Reload rejected: {args.Error}");
};
```

## Resolution Process

The settings manager resolves values using the following process:

1. **Determine Source Order**: Use property-level order if specified, otherwise use class-level order, otherwise use default order.
2. **Conditional Order Evaluation**: If `SourceOrderIf` is present, evaluate the condition and use the conditional order if applicable.
3. **Iterate Through Sources**: For each source in the resolved order, attempt to read the setting.
4. **Use First Match**: Return the value from the first source that has it.
5. **Fall Back to Default**: If no source has the setting, use the property's default value from the class definition.
6. **Validate**: Apply the configured validator (if any) to the resolved settings.

### Example Resolution

Given:
```csharp
[SourceOrder("User", "Machine")]
public class AppSettings : SettingsBase
{
    [SourceOrder("Machine", "User")]  // Property override
    public string Theme { get; set; } = "Light";  // Default
}
```

If we have:
- User source: Theme = "Dark"
- Machine source: Theme = "Classic"

For `FontSize` (no property-level override):
- Resolution order: "User" → "Machine" (class-level order)
- Result: First available value

For `Theme` (property-level override):
- Resolution order: "Machine" → "User" (property-level order)
- Result: "Classic" (Machine source wins)

## Thread Safety

- **Concurrent Reads**: Multiple threads can safely read `manager.Current` concurrently.
- **Write Operations**: Write operations are serialized and thread-safe.
- **Reload Operations**: Reload and write operations are mutually exclusive; they cannot happen simultaneously.

## Error Handling

The library provides clear, typed errors:

```csharp
try
{
    var manager = new SettingsManagerBuilder<AppSettings>()
        .AddSource("User", userSource)
        .Build();
}
catch (InvalidOperationException ex)
{
    // Thrown if referenced sources are not registered or validation fails at build time
    Console.WriteLine($"Configuration error: {ex.Message}");
}
```

## Practical Examples

### Example 1: Machine Policy with User Overrides

```csharp
[SourceOrder("User", "Machine")]
public class CompanySettings : SettingsBase
{
    public int FontSize { get; set; } = 12;
    
    // Locked by policy: users cannot override this
    [SourceOrder("Machine", "GroupPolicy")]
    public string[] AllowedEncryptionMethods { get; set; } = Array.Empty<string>();
}

var userSource = new JsonFileSource("User", 
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
    "MyApp", "settings.json"));

var machineSource = new RegistrySource("Machine", RegistryHive.LocalMachine, 
    @"Software\MyCompany\MyApp");

var manager = new SettingsManagerBuilder<CompanySettings>()
    .AddSource("User", userSource)
    .AddSource("Machine", machineSource)
    .Build();
```



### Example 2: Watch for Changes

```csharp
var watchableUserSource = new JsonFileSource("User", "config.json") as IObservableSettingsSource;

var manager = new SettingsManagerBuilder<AppSettings>()
    .AddSource("User", (ISettingsSource)watchableUserSource, watchForChanges: true)
    .Build();

manager.SettingsChanged += (sender, args) =>
{
    Console.WriteLine("User settings file changed, reloaded automatically");
};

manager.SettingsReloadRejected += (sender, args) =>
{
    Console.WriteLine($"Failed to reload settings: {args.Error}");
};
```

## Architecture

```
ISettingsManager<TSettings>
├── ISettingsSource (User)
├── ISettingsSource (Machine)
├── ISettingsSource (Custom)
└── SettingsResolver
    └── SettingsTypeMetadata
        └── Source ordering rules
```

**Key Components:**

- **ISettingsManager<TSettings>**: Main interface for reading and writing settings
- **ISettingsSource**: Abstract representation of a settings storage backend
- **SettingsBase**: Marker base class for all settings models
- **SettingsResolver**: Internal component that applies ordering rules and resolves values
- **SettingsValidator**: Optional validation callback for resolved settings

## License

MIT

## Contributing

Contributions are welcome! Please ensure all tests pass and maintain the existing code style.

## Building and Testing

```bash
# Build the library
dotnet build UltimateSettings/UltimateSettings.csproj

# Run tests
dotnet test UltimateSettings.Tests/UltimateSettings.Tests.csproj

# Build with release configuration
dotnet build -c Release UltimateSettings/UltimateSettings.csproj
```

## Requirements

- .NET 10.0 or later
