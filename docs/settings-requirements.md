# UltimateSettings Goals And Requirements

## Product Goal
Provide a reusable, strongly typed settings system for .NET that can read from multiple setting sources with deterministic precedence, source-level write capabilities, and per-setting policy control.

## Core Principles
- Reusable library-first design with minimal host assumptions.
- Strong typing for settings models and values.
- Deterministic resolution when multiple sources provide the same setting.
- Security and policy controls must be first-class.
- Extensibility for new settings and custom sources.

## Functional Requirements
- FR-01 Multi-source read: The system must read settings from multiple configurable sources.
- FR-02 Ordered precedence: Sources must be evaluated in a defined order to resolve final values.
- FR-03 Global default order: The library must support a default source order for all settings.
- FR-04 Per-setting override order: A specific setting must be able to override the default source order.
- FR-05 Policy enforcement use case: The system must support scenarios where machine or policy sources override user-local values.
- FR-06 User-preferred use case: The system must support scenarios where user-local values override machine-global values for selected settings.
- FR-07 Setting-level mutability policy: Each setting must support a policy that controls whether user-local overrides are allowed.
- FR-08 Policy-driven overridability: Whether a setting is overridable may itself be controlled by another setting, role, or rule source.
- FR-09 Easy setting expansion: Existing settings schemas must be easy to extend with new properties or values.
- FR-10 Custom setting sources: Consumers must be able to add custom setting sources without changing core library code.
- FR-11 Write protection awareness: Sources may be marked read-only/write-protected.
- FR-12 Targeted write: Callers must be able to write a setting change to a specific source.
- FR-13 Administrative write routing: Administrative callers must be able to choose machine-global output instead of user-local output.
- FR-14 Optional change notification: Sources may optionally notify the library of external changes so settings can be reloaded while the host application is running.

## Source Model Requirements
- SR-01 Source identity: Each source must have a stable identifier and human-readable name.
- SR-02 Capabilities: Each source must expose read support and write support flags.
- SR-03 Write constraints: Write operations against write-protected sources must fail with a clear, typed error.
- SR-04 Availability handling: Temporarily unavailable sources must produce explicit operational errors.

## Resolution And Policy Requirements
- RP-01 Deterministic outcome: Same inputs and source ordering must always produce the same resolved result.
- RP-02 Per-setting resolution rules: Resolution order can differ by setting key or typed property.
- RP-03 Traceability: Resolution should provide optional metadata about winning source and overridden sources.
- RP-04 Safe defaults: If a setting is missing across all sources, the library must fall back to schema default values.
- RP-05 Policy-evaluated precedence: Policy evaluation may alter the effective precedence for a specific setting and subject context without reclassifying a valid value as invalid.

## Write Requirements
- WR-01 Explicit target writes: Write API must allow selecting target source by identifier.
- WR-02 Capability validation: Write API must validate target source is writable before persisting.
- WR-03 No implicit cross-write: Writing to one source must not silently persist to other sources.
- WR-04 Optional elevated workflows: The API should support host-driven privilege checks for protected sources.

## Change Notification Requirements
- CN-01 Opt-in behavior: Hot reload must be opt-in per source and per manager; a source that does not support or enable change notification must never trigger a reload.
- CN-02 Validate before apply: A newly detected external change must be loaded and validated before it replaces the currently active settings.
- CN-03 Reject unsafe changes: If validation fails, the currently active settings must remain unchanged, and the failure must be observable by the host (error message and/or log).
- CN-04 Atomic apply: Applying a validated change must be an atomic swap of the active settings snapshot, with no consumer able to observe a partially updated state.
- CN-05 Change visibility: Consumers must be able to observe that settings changed, through an event, `INotifyPropertyChanged`, or an equivalent mechanism.

## Extensibility Requirements
- ER-01 Pluggable source contract: Provide an interface/abstraction to implement custom sources.
- ER-02 Non-breaking settings growth: Adding a new setting should not require redesign of existing source contracts.
- ER-03 Serializer abstraction: Serialization behavior should be replaceable for custom formats if needed.

## Non-Functional Requirements
- NFR-01 Reliability: Failed reads/writes must return actionable errors and preserve existing persisted data.
- NFR-02 Performance: Resolving one setting should only access required sources when possible.
- NFR-03 Thread safety: Concurrent read operations must be safe; write semantics must be documented.
- NFR-04 Testability: All resolution and write behaviors must be testable via interfaces and mocks.
- NFR-05 Portability: Core functionality should work across Windows, Linux, and macOS for file-based sources.

## Acceptance Criteria
- AC-01 A default source order can be configured once and applied globally.
- AC-02 A specific setting can define custom precedence that differs from global order.
- AC-03 A policy-like setting can be configured so user-local values cannot override machine/global values.
- AC-04 A user-preference setting can be configured so user-local values can override machine/global values.
- AC-05 A custom source implementation can be registered and participates in resolution.
- AC-06 Writing to a read-only source returns a clear error and does not change any source data.
- AC-07 Caller can explicitly write to machine-global source instead of user-local source.
- AC-08 Adding a new setting field does not require redesign of source registration/resolution APIs.
- AC-09 A policy setting can make a specific setting overridable for one user group while keeping it locked for another group.
- AC-10 An external change to a watched source triggers a reload attempt, and an invalid candidate does not replace the currently active settings.
- AC-11 A source that does not opt into change notification never triggers a reload, even if its underlying storage changes externally.

## Suggested Initial Domain Examples
- EX-01 FontSize: user-local override allowed.
- EX-02 AllowedEncryptionMethods: user-local override denied; policy or machine source wins.
- EX-03 CanOverrideSettingXY: IT users may override the user-vs-system precedence for SettingXY, while HR users may not.

## API Design

### Settings Class Shape
Settings are plain classes deriving from a shared base, with public properties and no embedded resolution logic. Precedence and override rules are declared entirely through attributes, so the ruleset is visible at a glance.

`SourceOrder` may be placed on the class itself to set the default order for every property that has no property-level `SourceOrder`, mirroring how ASP.NET's `AuthorizeAttribute` can be set on a controller and overridden per endpoint.

```csharp
[SourceOrder("User", "Machine", "GroupPolicy")] // class-level default
public class AppSettings : SettingsBase
{
    [SourceOrder("User", "Machine")] // property-level override of the class default
    public int FontSize { get; set; } = 12;

    [SourceOrder("Machine", "GroupPolicy")]
    public string[] AllowedEncryptionMethods { get; set; } = Array.Empty<string>();

    [SourceOrder("GroupPolicy", "Machine")] // "User" intentionally omitted, see Gate Property Rule
    public bool CanOverrideSettingXY { get; set; }

    [SourceOrder("GroupPolicy", "Machine")] // default: no user override
    [SourceOrderIf(nameof(CanOverrideSettingXY), "User", "GroupPolicy", "Machine")]
    public string SettingXY { get; set; } = string.Empty;
}
```

Effective order precedence, most specific wins: property-level `SourceOrder` > class-level `SourceOrder` > library-wide default order.

### Attribute Contracts

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
public sealed class SourceOrderAttribute : Attribute
{
    public SourceOrderAttribute(params string[] sourceIds) => SourceIds = sourceIds;
    public IReadOnlyList<string> SourceIds { get; }
}

[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public sealed class SourceOrderIfAttribute : Attribute
{
    public SourceOrderIfAttribute(string conditionProperty, params string[] sourceIds)
    {
        ConditionProperty = conditionProperty;
        SourceIds = sourceIds;
    }

    public string ConditionProperty { get; }
    public IReadOnlyList<string> SourceIds { get; }
}
```

- `SourceOrder` declares the precedence used when no conditional override applies.
- `SourceOrderIf` declares an alternate precedence used when the named condition property currently resolves to `true`. Multiple `SourceOrderIf` attributes may stack on one property; they are evaluated top-to-bottom and the first true condition wins, otherwise the base `SourceOrder` applies.
- The condition property is referenced via `nameof(...)`, keeping the reference refactor-safe at compile time.

Write targets are declared independently from read precedence:

```csharp
[WritableTo("User", "Machine")]
public string Theme { get; init; } = "Light";

[Readonly]
public string InstallationId { get; init; } = string.Empty;
```

- `WritableTo` restricts edits to the listed registered source ids.
- Without `WritableTo`, every registered writable source is allowed.
- `Readonly` is an alias for an empty writable set.
- Declaring both `WritableTo` and `Readonly` is invalid.

### Resolution Order Rule
A property referenced by `SourceOrderIf` must be fully resolved before the dependent property is resolved, since the dependent property's effective precedence depends on it. At settings-type registration time, the library must:
1. Build a dependency graph from all `SourceOrderIf` references.
2. Topologically sort properties so condition properties resolve before dependents.
3. Detect cycles and fail fast with a clear registration-time exception, rather than allowing runtime deadlock or infinite recursion.

### Condition Type Rule
A property referenced by `SourceOrderIf` must resolve to `bool` (or `bool?`, treated as `false` when null). This is validated by reflection at registration time; an invalid condition type must throw a clear, actionable exception before any resolution occurs.

### Gate Property Rule
A property used as a `SourceOrderIf` condition should generally exclude from its own `SourceOrder` the source it is meant to guard against (for example, omitting `"User"` from `CanOverrideSettingXY`'s own order). Otherwise a subject could grant itself the override by writing directly to the guarded source. This is a documented convention that setting authors must follow, not something the library can enforce automatically.

### Type-Safe Targeted Writes
Targeted writes use expression-tree property selectors instead of per-property generated methods or stringly-typed keys, keeping calls type-checked and refactor-safe without requiring source generation:

```csharp
public interface ISettingsManager<TSettings> where TSettings : SettingsBase, new()
{
    TSettings Load();
    void Edit(string sourceId, Action<ISettingsEditor<TSettings>> edit);
}
```

```csharp
settingsManager.Edit(SourceIds.User, edit =>
{
    edit.Set(s => s.FontSize, 14);
});

settingsManager.Edit(SourceIds.Machine, edit =>
{
    edit.Set(s => s.SettingXY, "AES-256");
});
```

`Edit` resolves each `PropertyInfo` from its expression, validates the target source against each property's write constraints, and persists the complete edit to that source. A future optimization may add a source generator to emit per-property edit helpers, but this is deferred until the attribute-based model is proven, since it adds build-time complexity not required for v1.

### Source Registration
Sources are registered at the composition root, not inside the settings class. A settings class only references source ids by name via `SourceOrder`/`SourceOrderIf`; it must not construct or own source instances itself. This keeps the schema free of environment details (file paths, registry hives, per-tenant locations) and keeps sources swappable for tests without subclassing.

```csharp
public interface ISettingsSource
{
    string Id { get; }
    bool CanRead { get; }
    bool CanWrite { get; }
    void WriteMany(IReadOnlyDictionary<string, object?> values);
}
```

```csharp
public sealed class SettingsManagerBuilder<TSettings> where TSettings : SettingsBase, new()
{
    public SettingsManagerBuilder<TSettings> AddSource(string id, ISettingsSource source);
    public SettingsManagerBuilder<TSettings> WithDefaultOrder(params string[] sourceIds);
    public ISettingsManager<TSettings> Build();
}
```

```csharp
var manager = new SettingsManagerBuilder<AppSettings>()
    .AddSource(SourceIds.User, new JsonFileSource(userConfigPath))
    .AddSource(SourceIds.Machine, new JsonFileSource(machineConfigPath))
    .AddSource(SourceIds.GroupPolicy, new RegistrySource(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Contoso\App"))
    .WithDefaultOrder(SourceIds.User, SourceIds.Machine, SourceIds.GroupPolicy)
    .Build();
```

Multiple instances of the same source type with different paths are supported naturally, since `AddSource` maps an arbitrary id to any configured instance (for example two `JsonFileSource`s registered as `"UserJson"` and `"TeamJson"` with different paths).

### Source Id Constants Convention
Source ids appear both in attributes (which require compile-time constants) and in builder registration calls. Projects should define ids as `const string` fields rather than inline string literals, to avoid typos and keep renames refactor-safe:

```csharp
public static class SourceIds
{
    public const string User = "User";
    public const string Machine = "Machine";
    public const string GroupPolicy = "GroupPolicy";
}
```

### Source Registration Validation Rule
`Build()` must validate, at registration time and before any resolution occurs:
- every source id referenced by any `SourceOrder` or `SourceOrderIf` attribute on `TSettings` has a matching registered source, failing fast with a clear exception otherwise,
- no duplicate source ids are registered,
- the dependency-cycle and condition-type checks defined above also run at this point, since this is the point at which all sources and attributes are known.

### Change Notification And Hot Reload
Hot reload is opt-in at two levels: a source must actively support and be configured to watch for external changes, and the manager must apply a validated candidate before it becomes the active settings snapshot. A source that does not implement change notification never triggers a reload.

```csharp
public interface IObservableSettingsSource : ISettingsSource
{
    event EventHandler SourceChanged;
}
```

```csharp
public delegate bool SettingsValidator<TSettings>(TSettings candidate, out string? error);
```

```csharp
public sealed class SettingsManagerBuilder<TSettings> where TSettings : SettingsBase, new()
{
    public SettingsManagerBuilder<TSettings> AddSource(string id, ISettingsSource source, bool watchForChanges = false);
    public SettingsManagerBuilder<TSettings> WithDefaultOrder(params string[] sourceIds);
    public SettingsManagerBuilder<TSettings> WithValidator(SettingsValidator<TSettings> validator);
    public ISettingsManager<TSettings> Build();
}
```

```csharp
public interface ISettingsManager<TSettings> where TSettings : SettingsBase, new()
{
    TSettings Current { get; }
    TSettings Load();
    void Edit(string sourceId, Action<ISettingsEditor<TSettings>> edit);
    event EventHandler<SettingsChangedEventArgs<TSettings>> SettingsChanged;
    event EventHandler<SettingsReloadRejectedEventArgs> SettingsReloadRejected;
}
```

Reload pipeline, triggered when a watched source raises `SourceChanged`:
1. Debounce rapid successive change notifications from the same source (file system watchers commonly fire multiple events for one logical change).
2. Refresh only the cached data for the source that changed; reuse cached data from unaffected sources.
3. Re-run resolution across all sources to build a candidate `TSettings` snapshot.
4. Run the registered validator against the candidate. If it returns `false`, raise `SettingsReloadRejected` with the validator's error, log it, and leave `Current` unchanged.
5. If validation succeeds, atomically swap `Current` to the new snapshot (no consumer observes a torn/partial state) and raise `SettingsChanged` with the previous and new snapshots.

Consumers observe changes through the `SettingsChanged` event on `ISettingsManager<TSettings>`. Hosts that prefer `INotifyPropertyChanged`-style binding can wrap `Current` in an adapter that diffs snapshots and raises `PropertyChanged` per changed property; this adapter is optional and layered on top of the event-based core rather than required of every settings class.



## Out Of Scope For Initial Version
- OOS-01 UI for editing settings.
- OOS-02 Remote secret-management integrations.
- OOS-03 Distributed synchronization between machines.
