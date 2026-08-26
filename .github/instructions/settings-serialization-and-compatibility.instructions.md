---
description: "Use when implementing settings models, serializers, or persistence in this C# library. Enforces deterministic serialization and schema compatibility behavior."
name: Settings Serialization And Compatibility
applyTo: "**/*.cs"
---
# Settings Serialization And Compatibility Rules

These are hard rules for this project.

## Enforcement Scope
- Enforce these rules for all new code and any code touched during the current task.
- Do not perform unrelated mass refactors solely to satisfy these rules.

## Serializer Defaults
- Use System.Text.Json unless another format is explicitly requested.
- Keep serializer options deterministic and shared through a single configuration point.
- Prefer explicit settings over implicit defaults when behavior affects compatibility.

## Read Compatibility
- Use lenient read behavior by default for persisted settings.
- Tolerate unknown JSON fields to allow forward compatibility.
- Apply defaults for missing optional values.
- Fail only for genuinely invalid or unsafe payloads.

## Write Compatibility
- Keep written shape stable unless migration is intentional.
- Avoid renaming or removing serialized properties without a migration strategy.
- Prefer additive fields for evolving schemas.

## Validation And Recovery
- Validate semantic constraints after deserialization.
- For invalid payloads, provide a clear recovery strategy such as fallback defaults, backup, or explicit error propagation.
- Never silently lose user settings data.

## Review Checklist
- Serializer configuration is centralized and deterministic.
- Backward and forward compatibility concerns are addressed.
- Validation and recovery paths are explicit and testable.
