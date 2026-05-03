---
description: 'Auto-sync documentation when source code changes'
applyTo: 'src/**/*.cs,src/**/*.csproj,Directory.Build.props,Directory.Packages.props'
---

# Automatic Documentation Sync

When modifying source code, **always check whether documentation needs updating** and apply
those changes in the same commit.

## Trigger Conditions

After any code change, ask yourself:

1. Did I add/remove/rename a public class, method, property, or option?
2. Did I add/remove a NuGet package reference or project reference?
3. Did I add/remove a CLI option or change its behavior?
4. Did I add/remove/modify an API endpoint or request/response DTO?
5. Did I add a new sink or source backend?
6. Did I change configuration binding (option names, defaults, sections)?

If **any** answer is yes, update the corresponding documentation files listed below.

## Mapping: Code Change → Doc Files

| Code Change | Documentation Files to Update |
|-------------|-------------------------------|
| New/changed public option property | `docs/configuration.md`, `README.md` (appsettings example) |
| New/changed CLI option | `docs/replay-cli.md` (options table + examples) |
| New/changed API endpoint or DTO field | `docs/replay-api.md` (endpoints, request body, options table) |
| New project or package | `README.md` (packages table), `docs/README.md` (Quick Links), `docs/architecture.md` (dep tree) |
| New/changed sink | `docs/sinks/README.md`, `docs/sinks/{name}.md`, `docs/architecture.md` |
| New/changed source provider | `docs/replay-cli.md` (source section) |
| Pipeline behavior change | `docs/pipeline.md` |
| Filter behavior change | `docs/filtering.md` |
| Observability change | `docs/observability.md` |
| ProjectReference/PackageReference change | `docs/architecture.md` (NuGet Package Dependencies tree) |

## How to Update

- **Options tables**: Add/update the row with property name, type, default, and description.
- **Code examples**: Ensure they compile and reflect the current API surface.
- **Architecture dependency tree**: Mirror the actual `<ProjectReference>` and `<PackageReference>` items.
- **Request body examples**: Include all fields (new optional fields should appear with a comment).
- **CLI examples**: Add a usage example for new options when they represent a distinct workflow.

## Commit Discipline

- Documentation updates for a code change go in the **same commit** as the code change.
- If a commit is purely a doc fix (no code change), prefix the message with `docs:`.
- Never leave documentation out of date across commits.
