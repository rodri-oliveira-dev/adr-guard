# ADR relationships export contract v1

[Português (Brasil)](adr-relationships-export-v1.pt-BR.md)

This document freezes the JSON exchange contract prepared for Align CLI integration. Core can now produce this deterministic data and the MCP relationship tool reuses that implementation without changing its contract. The ADR Guard CLI export command is not implemented or published yet.

The authoritative machine-readable definition is [`adr-relationships-export-v1.schema.json`](schemas/adr-relationships-export-v1.schema.json). Representative integration fixtures are in [`docs/examples/adr-relationships-export-v1`](examples/adr-relationships-export-v1/).

## Envelope and ADR metadata

Every document contains, in order, `schemaVersion: "1.0"`, `adrs`, `relationships`, and `issues`. There are no timestamps, ADR Markdown bodies, or computed effective-decision sets.

Each `adrs` item contains `identifier`, `path`, nullable `title`, and nullable `status`. Recognized status values are exactly `Accepted`, `Proposed`, `Deprecated`, and `Superseded`. A missing, unknown, or invalid source status is serialized as `null` and remains attributable through an issue; it is never promoted to `Accepted`.

All paths are portable, repository-root-relative paths using `/`. They are never relative to the configured ADR directory and never contain absolute machine paths.

## Relationships and issues

Each relationship preserves `sourceIdentifier`, `sourcePath`, `type`, `declaredTarget`, nullable `targetIdentifier`, nullable `targetPath`, and `resolution`. Supported types are `supersedes`, `superseded-by`, and `depends-on`.

Resolution values intentionally match the existing read-only MCP `get_adr_relationships` representation:

- `valid`: the declaration resolves to exactly one ADR;
- `ambiguous`: the declaration resolves to more than one candidate, so target identity and path are `null`;
- `unresolved`: the declaration resolves to no ADR, so target identity and path are `null`.

`valid` describes target resolution only. Relationship validation findings such as cycles or inconsistent supersession remain in `issues`; they are not hidden or converted into successful compliance. Each issue preserves `code`, `message`, repository-relative `path`, and `severity` (`warning` or `error`) from Core validation.

The export does not infer undeclared relationships. Reciprocal declarations remain separate typed records so consumers can apply their own deduplication policy.

## Ordering and version compatibility

Producers must emit deterministic object fields and arrays for identical inputs. ADRs are ordered by identifier and path; relationships by source identifier, source path, type, and declared target; issues by the deterministic Core validation order. Fixtures follow that policy.

`schemaVersion` uses `major.minor`:

- increment the major version for removals, renamed fields, changed nullability, changed value sets, or any incompatible semantic change;
- increment the minor version only for compatible extensions that preserve the meaning of every existing field;
- never change an existing field's semantics silently.

Consumers may accept a newer minor version within a supported major version, but should reject an unknown major version. Schema `1.0` is conceptually aligned with `get_adr_relationships` while adding the `adrs` metadata list; it does not change the MCP request or response contract.
