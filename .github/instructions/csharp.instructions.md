---
description: 'C# development guidelines'
applyTo: '**/*.cs'
---

# C# Development Guidelines

## Naming Conventions

- Use **PascalCase** for public types, methods, properties, and constants.
- Use **_camelCase** with leading underscore for private and internal fields.
- Use **camelCase** for local variables and method parameters.
- Prefix interface names with "I" (e.g., `IUserService`).
- All constant fields must use **PascalCase** regardless of access level.

## Code Formatting

- Apply code formatting style defined in `.editorconfig` (4 spaces, 160 char line limit).
- Always place opening braces on a new line for all code blocks.
- Use file-scoped namespace declarations: `namespace CompanyName.ProjectName;`
- Keep `using` directives outside the namespace, with `System` directives first.
- Ensure a final newline at the end of all files.

## Type and Null Handling

- Declare variables non-nullable by default. Only use `nullable<T>` or `?` when `null` is intentional.
- Use `is null` and `is not null` exclusively; never use `== null` or `!= null`.
- Trust C# null annotations—do not add redundant null checks when the type system guarantees a value cannot be null.

## Modern C# Patterns

- Always use pattern matching over `is` checks with casts or `as` followed by null checks.
- Prefer switch expressions for conditional logic.
- Use `nameof()` instead of string literals when referring to member names.
- Leverage inlined variable declarations in pattern matching.
- Use throw expressions for concise error handling.

## Control Structures and Expression Bodies

- Always use braces for `if`, `for`, `while`, `foreach`, and `using` statements—no single-line statements without braces.
- Avoid expression-bodied methods (`=>` syntax); use explicit block bodies instead.
- Expression-bodied properties and auto-accessors are acceptable.

## Code Organization

- Place method members in a logical order: public before private, static before instance.
- Group related fields, properties, and methods together.
- Add XML doc comments for all public APIs and non-obvious internal members when needed.

## Collections and Initializers

- Prefer object initializers and collection initializers for readability.
- Keep initializer elements on separate lines (one element per line) unless very simple.

## Testing

- Test method names should clearly express what is being tested (e.g., `MethodName_ExpectedBehavior_GivenCondition`).
- Do not include "Arrange", "Act", "Assert" comments in test code.
- Write test cases for critical paths and edge cases.

## Performance and Best Practices

- Use `async/await` properly; apply `.ConfigureAwait(false)` in library code.
- Prefer making lambda expressions `static` when they don't capture local state.
- Use readonly fields and properties to prevent unintended mutations.
- Avoid redundant operations: remove unnecessary casts, using statements, and string interpolations.

## ReSharper/Rider Compliance

- Adhere to inspection severities configured in the solution settings:
  - **Error**: Unused local members, local-only violations, unused auto-property accessors.
  - **Warning**: Unused global members, fields not accessed, redundancy issues.
  - **Suggestion**: Type preferences, code style, convenience patterns.
- Severity levels are enforced by ReSharper/Rider inline inspection.
