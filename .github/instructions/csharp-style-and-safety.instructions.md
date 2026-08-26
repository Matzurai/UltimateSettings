---
description: "Use when creating or modifying C# source files in this project. Enforces braces on control statements, deterministic IDisposable cleanup, and English-only comments and variable names."
name: CSharp Style And Safety
applyTo: "**/*.cs"
---
# C# Style And Safety Rules

These are hard rules for this project.

## Enforcement Scope
- Enforce these rules for all new code and any code touched during the current task.
- Do not perform unrelated mass refactors solely to satisfy these rules.

## Braces Are Mandatory
- Always use curly braces for control-flow statements, even for a single statement.
- Applies to: if, else, for, foreach, while, do, using, lock, and fixed blocks.
- Never use single-line control statements without braces.

## IDisposable Must Be Disposed
- Any object implementing IDisposable must be deterministically disposed.
- Prefer using declarations or using statements.
- If lifetime must cross scopes, use try/finally and call Dispose in finally.
- Do not leave disposable objects for GC finalization as the cleanup strategy.

## English-Only Names And Comments
- All variable names must be English.
- All code comments must be English.
- Keep names clear and domain-specific; avoid abbreviations unless widely known.

## Review Checklist
- Every control-flow statement has braces.
- Every IDisposable instance is disposed exactly once.
- New or edited variable names and comments are in English.
