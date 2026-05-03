# Copilot Instructions

## Documentation Maintenance

This project follows strict documentation-as-code practices. When making any source code change
that affects the public API surface, CLI options, configuration, or architecture:

1. Check `docs-sync.instructions.md` for the mapping of code changes to documentation files.
2. Update all affected documentation files in the same commit as the code change.
3. Keep package tables in `README.md` and `docs/README.md` synchronized.
4. Ensure code examples in docs remain syntactically correct and reflect the current API.

## Commit Guidelines

- Progressive commits: each commit must compile and pass tests.
- Prefix convention: `feat:`, `fix:`, `docs:`, `ci:`, `refactor:`, `test:`.
- Documentation updates for feature/fix commits go in the same commit (no separate `docs:` commit needed).
