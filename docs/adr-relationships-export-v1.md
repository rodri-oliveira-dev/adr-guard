# ADR relationships export contract v1

[Português (Brasil)](adr-relationships-export-v1.pt-BR.md)

This document freezes the JSON exchange contract prepared for Align CLI integration. Core produces this deterministic data, and both the MCP relationship tool and the CLI export command reuse that implementation without changing the MCP contract. The CLI export command is published in version `1.12.0`.

The authoritative machine-readable definition is [`adr-relationships-export-v1.schema.json`](schemas/adr-relationships-export-v1.schema.json). Representative integration fixtures are in [`docs/examples/adr-relationships-export-v1`](examples/adr-relationships-export-v1/).

## CLI export

```text
adr-guard export relationships docs/adr
adr-guard export relationships docs/adr --output artifacts/relationships.json
```

The default destination is `<repository-root>/.adr-guard/relationships.json`. A custom destination is relative to the detected repository root, uses portable `/` separators, must use a `.json` extension, must remain inside that root, and cannot traverse symbolic links or reparse points. Missing destination directories are created. The complete UTF-8 document is written to a temporary file in the destination directory and then atomically promoted over any existing artifact; a failed write leaves the previous artifact intact.

The command returns `0` whenever it safely writes the artifact, including when `issues` is non-empty. Invalid command syntax or configuration returns `2`; input, path, security, cancellation, and write failures return `3`. It neither edits ADRs or indexes nor modifies configuration or Git state, and it requires no MCP server, model provider, credentials, or network connection.

The optional `generatedFrom` commit SHA is deliberately omitted from schema `1.0`. Reading implicit Git state would add a changing value and a Git dependency to an otherwise repository-format-only export. A future compatible contract may add explicit provenance if a consumer demonstrates that requirement without weakening deterministic output.

The exporter reads at most 1,000 ADR Markdown files of 524,288 bytes each, requires valid UTF-8, and skips symbolic links and reparse points before reading content. Exceeding a limit or encountering malformed input returns operational exit `3` without producing a replacement artifact.

## GitHub Actions and Align

[`github-action-relationships-export.yml`](examples/github-action-relationships-export.yml) shows a least-privilege job that restores a manifest-pinned ADR Guard tool, writes `.adr-guard/relationships.json`, and uploads it for an external consumer such as Align. Before enabling the example, pin `.config/dotnet-tools.json` to published CLI `1.12.0` or a newer compatible release.

Align can download or receive the artifact and join each repository-relative `path` to its imported documentation tree. It must accept only `resolution: "resolved"` as a uniquely identified target and must still inspect `issues` before treating the edge as semantically valid.

The optional `check` step in the example is deliberately separate and advisory. Export exit `0` means that the JSON artifact was written; it does **not** mean that every ADR or relationship passed validation. Teams that require validation may remove `continue-on-error`, but that is an independent CI policy and not part of the export contract.

## Envelope and ADR metadata

Every document contains, in order, `schemaVersion: "1.0"`, `adrs`, `relationships`, and `issues`. There are no timestamps, ADR Markdown bodies, or computed effective-decision sets.

Each `adrs` item contains `identifier`, `path`, nullable `title`, and nullable `status`. Recognized status values are exactly `Accepted`, `Proposed`, `Deprecated`, and `Superseded`. A missing, unknown, or invalid source status is serialized as `null` and remains attributable through an issue; it is never promoted to `Accepted`.

All paths are portable, repository-root-relative paths using `/`. They are never relative to the configured ADR directory and never contain absolute machine paths.

## Relationships and issues

Each relationship preserves `sourceIdentifier`, `sourcePath`, `type`, `declaredTarget`, nullable `targetIdentifier`, nullable `targetPath`, and `resolution`. Supported types are `supersedes`, `superseded-by`, and `depends-on`.

Export resolution values distinguish referential resolution from semantic validation:

- `resolved`: the declaration resolves to exactly one ADR;
- `ambiguous`: the declaration resolves to more than one candidate, so target identity and path are `null`;
- `unresolved`: the declaration resolves to no ADR, so target identity and path are `null`.

`resolved` describes target resolution only. It is not confirmation that the relationship is semantically valid. Relationship validation findings such as cycles, inconsistent supersession, or inactive dependencies remain in `issues`; they are not hidden or converted into successful compliance. A successful export likewise means only that the artifact was written, not that every relationship is valid. Each issue preserves `code`, `message`, repository-relative `path`, and `severity` (`warning` or `error`) from Core validation.

The export does not infer undeclared relationships. Reciprocal declarations remain separate typed records so consumers can apply their own deduplication policy.

## Ordering and version compatibility

Producers must emit deterministic object fields and arrays for identical inputs. ADRs are ordered by identifier and path; relationships by source identifier, source path, type, and declared target; issues by the deterministic Core validation order. Fixtures follow that policy.

`schemaVersion` uses `major.minor`:

- increment the major version for removals, renamed fields, changed nullability, changed value sets, or any incompatible semantic change;
- increment the minor version only for compatible extensions that preserve the meaning of every existing field;
- never change an existing field's semantics silently.

Consumers may accept a newer minor version within a supported major version, but should reject an unknown major version. Schema `1.0` uses `resolved` so consumers cannot confuse referential resolution with validation. The existing MCP response remains unchanged and continues to use `valid` for its historical referential-resolution value.
