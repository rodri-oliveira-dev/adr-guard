# ADR Guard

[![CI](https://github.com/rodri-oliveira-dev/adr-guard/actions/workflows/ci.yml/badge.svg)](https://github.com/rodri-oliveira-dev/adr-guard/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/RodriOliveira.AdrGuard.svg)](https://www.nuget.org/packages/RodriOliveira.AdrGuard)
[![GitHub Release](https://img.shields.io/github/v/release/rodri-oliveira-dev/adr-guard)](https://github.com/rodri-oliveira-dev/adr-guard/releases)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)
[![License](https://img.shields.io/github/license/rodri-oliveira-dev/adr-guard)](LICENSE)

[Português (Brasil)](README.pt-BR.md)

ADR Guard is a lightweight .NET command-line tool for validating and indexing Architecture Decision Records (ADRs).

It is designed for repositories that want ADR conventions to be explicit, reviewable, and enforceable in local development and CI without introducing a heavy runtime dependency.

## Features

- validates ADR filenames, titles, statuses, and required sections;
- detects duplicate ADR IDs;
- detects broken relative links between ADRs;
- enforces a valid `Superseded by` link for superseded decisions;
- generates a deterministic Markdown index;
- avoids rewriting an index that is already current;
- exposes stable validation codes (`ADR001` through `ADR008`);
- exposes predictable exit codes for CI/CD;
- ships as a .NET Tool with no third-party runtime dependencies.

## Install

Releases are published to both NuGet.org and [GitHub Packages](https://github.com/rodri-oliveira-dev?tab=packages).

The simplest installation uses NuGet.org:

```bash
dotnet tool install --global RodriOliveira.AdrGuard
```

Update an existing installation with:

```bash
dotnet tool update --global RodriOliveira.AdrGuard
```

GitHub Packages is also available as a secondary registry. NuGet clients require GitHub authentication to consume packages from that source.

The installed command is:

```bash
adr-guard
```

## ADR format

ADR Guard expects Markdown files named with a four-digit ID followed by a lowercase kebab-case slug:

```text
0001-use-postgresql.md
```

A minimal valid ADR looks like this:

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

Supported statuses:

- `Proposed`
- `Accepted`
- `Deprecated`
- `Superseded`

The required sections are `Context`, `Decision`, and `Consequences`. A `Superseded` ADR must also contain a `Superseded by` section linking to an existing ADR.

## Validate ADRs

Validate a directory recursively:

```bash
adr-guard check docs/adr
```

When the directory is omitted, ADR Guard uses the current directory:

```bash
adr-guard check
```

A successful validation returns exit code `0`. Validation failures are printed with the file path, stable rule code, and message.

Example:

```text
docs/adr/0002-use-cache.md: ADR004 Status 'Approved' is invalid. Allowed values: Proposed, Accepted, Deprecated, Superseded.
Validation failed with 1 issue(s).
```

## Generate the ADR index

Validate the ADR set and generate `README.md` inside the ADR directory:

```bash
adr-guard index docs/adr
```

The generated file is deterministic:

```markdown
# Architecture Decision Records

| ADR | Decision | Status |
| --- | --- | --- |
| [0001](0001-use-postgresql.md) | Use PostgreSQL | Accepted |
| [0002](0002-adopt-opentelemetry.md) | Adopt OpenTelemetry | Proposed |
```

The index is written only after validation succeeds. If the existing file already matches the generated content, it is left untouched.

A custom output outside the ADR directory can be supplied with:

```bash
adr-guard index docs/adr --output adr-index.md
```

Inside the ADR directory, generated Markdown must be named `README.md`; otherwise it would become an ADR candidate on the next validation.

## Validation rules

| Code | Validation |
| --- | --- |
| `ADR001` | Filename must match `NNNN-lowercase-kebab-case.md` |
| `ADR002` | Level-one title is required |
| `ADR003` | Status is required |
| `ADR004` | Status must be supported |
| `ADR005` | Required section is missing or empty |
| `ADR006` | ADR ID is duplicated |
| `ADR007` | Relative ADR reference is broken |
| `ADR008` | Superseded ADR has no valid `Superseded by` link |

ADR IDs do not need to be contiguous. Gaps are allowed because ADRs may be archived, migrated, or removed without renumbering historical decisions.

## Exit codes

| Code | Meaning |
| ---: | --- |
| `0` | Success |
| `1` | ADR validation failed |
| `2` | Invalid command-line usage |
| `3` | Operational error |

This makes CI integration straightforward:

```yaml
- name: Validate ADRs
  run: adr-guard check docs/adr
```

## Build from source

Requirements:

- .NET SDK 10.0.400 or a compatible patch in the same feature band.

Build and test:

```bash
dotnet restore AdrGuard.slnx
dotnet build AdrGuard.slnx --configuration Release --no-restore
dotnet test AdrGuard.slnx --configuration Release --no-build
```

Create the tool package:

```bash
dotnet pack src/AdrGuard/AdrGuard.csproj --configuration Release --no-build --output artifacts/package
```

Install the locally built package:

```bash
dotnet tool install --tool-path ./.tools RodriOliveira.AdrGuard --version 0.1.0 --add-source ./artifacts/package
./.tools/adr-guard check docs/adr
```

## Architecture decisions

ADR Guard validates its own architecture decisions. See [docs/adr](docs/adr/README.md).

The repository CI builds and tests the solution, packages the .NET Tool, installs that package locally, runs the packaged `adr-guard` against `docs/adr`, regenerates the ADR index, and verifies that no documentation drift was introduced.

## Releases

After a pull request is merged into `main`, the release workflow waits for the `CI` workflow for that `main` commit to complete successfully. It then:

1. resolves a stable SemVer version, starting from `VersionPrefix` and incrementing the patch for subsequent releases;
2. packs `RodriOliveira.AdrGuard` with that version;
3. authenticates to NuGet.org through Trusted Publishing (OIDC) and publishes the package;
4. publishes the same package to GitHub Packages;
5. creates the corresponding `vMAJOR.MINOR.PATCH` tag and GitHub Release;
6. attaches the `.nupkg` to the GitHub Release.

The workflow is idempotent for a commit that already has a release tag.

## License

Licensed under the [MIT License](LICENSE).
