---
description: "Use when implementing C# file I/O, settings load/save flows, or library error boundaries. Enforces consistent exception and logging behavior."
name: CSharp Error Handling And Logging
applyTo: "**/*.cs"
---
# C# Error Handling And Logging Rules

These are hard rules for this project.

## Enforcement Scope
- Enforce these rules for all new code and any code touched during the current task.
- Do not perform unrelated mass refactors solely to satisfy these rules.

## Exception Strategy
- Throw exceptions for programmer errors and invalid arguments.
- For operational failures, either return a documented failure result or throw a documented domain exception.
- Do not swallow exceptions without a deliberate fallback path.

## Exception Quality
- Include actionable context in exception messages.
- Preserve root causes by chaining inner exceptions.
- Use the most specific exception type that fits the failure mode.

## Logging Strategy
- Log operational failures at boundaries where callers need diagnostics.
- Avoid duplicate logging of the same exception at multiple layers.
- Never log sensitive content from settings payloads.

## Cancellation And Async
- Accept and propagate CancellationToken in asynchronous I/O APIs.
- Do not convert cancellation into success.
- Avoid blocking calls in async code paths.

## Review Checklist
- Error behavior is documented and consistent.
- Exceptions preserve cause and context.
- Logging is useful, non-duplicative, and safe.
