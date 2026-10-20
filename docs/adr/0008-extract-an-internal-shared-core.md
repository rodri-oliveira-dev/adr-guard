# Extract an internal shared Core

## Status

Accepted

## Context

The command-line tool and the MCP server need the same ADR parsing, validation, configuration, Git inventory, impact, and architectural-drift behavior. The MCP executable previously reached those rules through the CLI project. That executable-to-executable dependency blurred transport and domain ownership, caused the MCP package to carry CLI implementation details, and made it harder to prove that both products use one deterministic rule engine.

The CLI, MCP server, VS Code extension, GitHub Action, container, and NuGet tools already expose separately versioned contracts. Extracting shared code must not change commands, exit codes, JSON/SARIF schemas, MCP tool contracts, or publication streams.

## Decision

Create `AdrGuard.Core` as an internal, non-packable .NET class library. It owns reusable models, parsing, validation, configuration, bounded Git inventory, impact analysis, drift evaluation, and shared drift-policy semantics. Existing namespaces and internal visibility are preserved where practical so the extraction does not create a new public API.

Both executables reference Core directly:

```text
VS Code ──process──> CLI ──reference──> Core
GitHub Action/container ──process─────> CLI ──reference──> Core
MCP client ──stdio──> MCP ────────────> Core
```

The CLI retains command routing, exit-code adaptation, human and machine presentation, write workflows, providers, and boundary orchestration. MCP retains its seven read-only versioned tools, `stdio` transport, request/response contracts, and workspace boundary. Core references neither executable; its shared Git process adapter remains bounded, cancellation-aware, and isolated from pure decisions. Automated architecture tests reject Core-to-executable edges, MCP-to-CLI references, cycles, and accidental visibility seams.

Packages and containers built from source after this decision distribute Core only as a runtime dependency inside the CLI and MCP .NET Tool packages and inside the CLI container. This decision does not retroactively change published artifacts. Core has no independent package ID, release, tag, or compatibility stream. The CLI and MCP products continue to version and publish independently.

## Alternatives considered

- Keep shared behavior in the CLI and let MCP reference the executable project. This preserves fewer project files but couples an independent protocol host to CLI presentation and packaging.
- Duplicate the rules in MCP. This avoids a project reference but risks divergent findings, ordering, fingerprints, and security limits.
- Publish Core as a standalone NuGet package. This creates an unsupported public API and an additional release stream without a consumer requirement.

## Consequences

CLI and MCP now share one deterministic implementation without depending on one another. New package and Docker builds from this source must carry `AdrGuard.Core.dll`, while new MCP package builds must not carry `adr-guard.dll`. Changes to Core must trigger CLI cross-platform and compatibility coverage.

The additional assembly is an internal implementation detail, not a new product. Contributors must keep adapters at executable boundaries, preserve strict contracts, and move a rule into Core only when it is genuinely shared or domain-owned. Existing release histories remain unchanged; availability of source or a local package is not evidence of a newly published product version.
