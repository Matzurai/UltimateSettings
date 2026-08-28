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
    void Save<TValue>(Expression<Func<TSettings, TValue>> property, TValue value, string sourceId);
}
```

```csharp
settingsManager.Save(s => s.FontSize, 14, SourceIds.User);
settingsManager.Save(s => s.SettingXY, "AES-256", SourceIds.Machine);
```

`Save` resolves the `PropertyInfo` from the expression, validates the target source against the property's write constraints, and persists only to that source. A future optimization may add a source generator to emit per-property write methods (for example `SaveFontSize(value, sourceId)`), but this is deferred until the attribute-based model is proven, since it adds build-time complexity not required for v1.

## Out Of Scope For Initial Version
- OOS-01 UI for editing settings.
- OOS-02 Remote secret-management integrations.
- OOS-03 Distributed synchronization between machines.
