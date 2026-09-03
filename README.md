# ADR Guard

ADR Guard is a .NET command-line tool for validating and maintaining Architecture Decision Records (ADRs).

> **Status:** incremental implementation. ADR discovery, parsing, validation, CLI checks, and index generation are available. Packaging and release hardening are reserved for the final phase.

## Current capabilities

- .NET 10 CLI application
- recursive Markdown ADR discovery
- generated `README.md` indexes excluded from ADR discovery
- ADR metadata and ATX-heading parsing (title, status, sections, ID, and slug)
- fenced-code-aware Markdown parsing
- validation for file naming, title, status, required sections, duplicate IDs, ADR references, and supersession links
- stable validation codes (`ADR001` through `ADR008`)
- deterministic Markdown index generation
- idempotent index writes
- explicit CLI exit codes for CI/CD integration
- unit and integration tests with xUnit.net v3 and Microsoft Testing Platform
- centralized package version management
- shared compiler and analyzer conventions
- deterministic builds with warnings treated as errors
- GitHub Actions build and test workflow

## Expected ADR shape

ADR files are expected to use a four-digit ID followed by a lowercase kebab-case slug:

```text
0001-use-postgresql.md
```

A minimal accepted ADR contains a title, status, context, decision, and consequences:

```markdown
# Use PostgreSQL

## Status
Accepted

## Context
We need a relational database.

## Decision
Use PostgreSQL.

## Consequences
The service depends on PostgreSQL operational knowledge.
```

Supported statuses are `Proposed`, `Accepted`, `Deprecated`, and `Superseded`.

## Requirements

- .NET SDK 10.0.400 or a compatible patch in the same feature band

## Build and test

```bash
dotnet restore AdrGuard.slnx
dotnet build AdrGuard.slnx --configuration Release --no-restore
dotnet test AdrGuard.slnx --configuration Release --no-build
```

## Run

Show help or the current version:

```bash
dotnet run --project src/AdrGuard/AdrGuard.csproj -- --help
dotnet run --project src/AdrGuard/AdrGuard.csproj -- --version
```

Validate ADRs in a directory:

```bash
dotnet run --project src/AdrGuard/AdrGuard.csproj -- check docs/adr
```

Generate or refresh `docs/adr/README.md` after validation succeeds:

```bash
dotnet run --project src/AdrGuard/AdrGuard.csproj -- index docs/adr
```

Write the generated index somewhere outside the ADR directory:

```bash
dotnet run --project src/AdrGuard/AdrGuard.csproj -- index docs/adr --output adr-index.md
```

Relative `--output` paths are resolved from the current working directory. A custom Markdown index inside the ADR directory is rejected unless it is named `README.md`, preventing the generated file from being interpreted as an ADR on a later check.

## Exit codes

| Code | Meaning |
| ---: | --- |
| `0` | Success |
| `1` | ADR validation failed |
| `2` | Invalid command-line usage |
| `3` | Operational error |

These exit codes make `adr-guard check` suitable for CI pipelines without parsing console output.

## License

Licensed under the MIT License. See [LICENSE](LICENSE).
