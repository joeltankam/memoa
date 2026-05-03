---
description: 'Documentation maintenance guidelines'
applyTo: 'docs/**,README.md'
---

# Documentation Maintenance

## When to Update Documentation

Update the relevant documentation files whenever:

- A new project or NuGet package is added to the solution.
- A public API surface changes (new options, endpoints, CLI flags, extension methods).
- A configuration option is added, renamed, or removed.
- A new sink or source backend is introduced.
- Replay behavior changes (new headers, timing modes, output formats).
- Architecture or dependency graphs change.

## Files to Keep in Sync

| Change Type | Files to Update |
|-------------|-----------------|
| New package/project | `README.md`, `docs/README.md` (Quick Links table), `docs/architecture.md` (dependency graph) |
| New CLI option | `docs/replay-cli.md` (options table + examples) |
| New replay API endpoint | `docs/replay-api.md` (endpoints section) |
| New sink | `docs/sinks/README.md`, `docs/sinks/{name}.md`, `docs/README.md` (ToC), `docs/architecture.md` |
| Configuration change | `docs/configuration.md`, `README.md` (appsettings example) |
| Pipeline change | `docs/pipeline.md` |
| Filter change | `docs/filtering.md` |
| Observability change | `docs/observability.md` |

## Package Table

Both `README.md` and `docs/README.md` contain a packages table. Keep both in sync. The format is:

```markdown
| Package | Description |
|---------|-------------|
| `Memoa.Core` | Core middleware, abstractions, and pipeline |
```

## Code Examples in Docs

- Code examples must be syntactically correct and reflect the current API.
- Use triple-backtick fenced blocks with the language identifier (`csharp`, `bash`, `json`).
- When a public method signature changes, grep docs for the old name and update.

## Architecture Diagram

`docs/architecture.md` contains a dependency tree under "NuGet Package Dependencies".
When adding or removing a `ProjectReference` or `PackageReference`, update this tree.

## Replay Header

All replay-related documentation must mention the `X-Memoa-Replay: true` header that the replay
engine adds to every replayed request.
