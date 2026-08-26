---
description: "Use when creating or modifying C# tests for this settings library. Enforces coverage for serialization, persistence, compatibility, and disposal-related behavior."
name: Settings Library Testing
applyTo:
  - "**/Tests/**/*.cs"
  - "**/*Tests.cs"
  - "**/*Test.cs"
---
# Settings Library Testing Rules

These are hard rules for this project.

## Test Scope
- Cover public behavior, not private implementation details.
- Use deterministic test data and isolated file paths.
- Clean up all temporary resources after each test.

## Required Scenarios
- Round-trip serialization and deserialization retains expected values.
- Missing settings file behavior is explicitly verified.
- Corrupted settings payload behavior is explicitly verified.
- Compatibility behavior for unknown and missing fields is verified.
- Save and load operations respect cancellation behavior.

## Resource Safety In Tests
- Dispose all IDisposable objects in tests.
- Ensure file handles are released before assertions on file state.
- Avoid hidden global state and cross-test contamination.

## Assertions And Structure
- Follow Arrange, Act, Assert structure.
- Assert observable outcomes and error contracts.
- Keep one main behavior per test.

## Review Checklist
- Core settings scenarios are covered.
- Resource cleanup is deterministic.
- Tests are stable and independent.
