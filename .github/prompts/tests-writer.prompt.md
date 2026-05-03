---
agent: 'agent'
tools: ['editFiles', 'search', 'codebase', 'problems', 'runCommands']
description: 'Generate unit or integration tests for a class or method'
argument-hint: 'Class or method to test, e.g. "MarketProxy.DataService.ProcessDataAsync"'
---

# Write Tests

Generate tests for the specified class or method. Follow the [testing instructions](../instructions/testing.instructions.md).

## Project Setup

- Test projects follow the naming convention `<ProjectName>.Tests` and share the same root directory as the production project.
- Test projects reference NUnit 4, Moq 4, and FluentAssertions 7 (provided automatically by `Directory.Build.targets`).
- `InternalsVisibleTo` entries are auto-generated — do not add them manually.
- Always use the whole project as context as well as its references.
- Always test as many scenarios as possible in unit tests.

## Steps

1. **Read** the production code and its dependencies to understand behavior.
2. **Identify** the test project (`<ProjectName>.Tests`). Create it if missing.
3. **Write** unit tests covering: happy path, edge cases, error cases, and boundary conditions.
4. **Write** integration tests when appropriate, covering: common paths and error cases.
5. **Build** — run `dotnet build` on the test project to verify compilation or built-in Visual Studio/Rider build.
6. **Run** — run `dotnet test --no-build --filter "FullyQualifiedName~<TestClass>"` to verify all tests pass or using built-in ReSharper/Rider test runners.
