---
description: "Use when creating or modifying public C# APIs or project dependencies in this library. Enforces API stability and conservative dependency management."
name: CSharp Api And Dependency Stability
applyTo: "**/*.cs"
---
# C# API And Dependency Stability Rules

These are hard rules for this project.

## Enforcement Scope
- Enforce these rules for all new code and any code touched during the current task.
- Do not perform unrelated mass refactors solely to satisfy these rules.

## Public API Stability
- Treat public and protected types, members, and behavior as compatibility-sensitive.
- Avoid breaking changes to names, signatures, nullability contracts, and exception behavior.
- If a breaking change is required, document it explicitly in task output.

## Versioning Expectations
- Prefer additive changes over mutating existing contracts.
- Mark deprecated APIs before removal when feasible.
- Keep behavior consistent across patch-level updates.

## Dependency Management
- Prefer BCL and existing project dependencies over new packages.
- Add a new NuGet package only when it provides clear reliability or maintainability gains.
- Keep package surface minimal and avoid overlapping libraries for the same concern.

## Review Checklist
- Public API changes are intentional and documented.
- New dependency additions are justified.
- Existing behavior remains compatible unless explicitly approved.
