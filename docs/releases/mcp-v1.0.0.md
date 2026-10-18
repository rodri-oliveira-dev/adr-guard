# ADR Guard MCP v1.0.0

[Português (Brasil)](mcp-v1.0.0.pt-BR.md)

This is the first stable release of the independently versioned ADR Guard MCP server. Version `1.0.0` is published on [NuGet.org](https://www.nuget.org/packages/RodriOliveira.AdrGuard.Mcp/1.0.0), in its dedicated [GitHub Release](https://github.com/rodri-oliveira-dev/adr-guard/releases/tag/mcp/v1.0.0), and in the MCP Registry. The seven public tool names, startup arguments, schema `1.0` structured results, package identity, command, and `stdio` transport have been reviewed and covered by contract tests; this does not promise compatibility with future breaking protocol or registry changes.

## Included

- Seven bounded read-only tools: `list_adrs`, `get_adr`, `search_adrs`, `get_adr_relationships`, `validate_adrs`, `analyze_impact`, and `verify_architecture`.
- A framework-dependent .NET 10 Tool with Package ID `RodriOliveira.AdrGuard.Mcp` and command `adr-guard-mcp`.
- Official Registry name `io.github.rodri-oliveira-dev/adr-guard` and a package README containing the required ownership marker.
- Local `stdio` transport, one fixed repository per process, canonical path containment, symlink/reparse-point defenses, bounded files/search/evidence/responses/time, cancellation, and sanitized errors.
- Structured validation, impact, and drift results that preserve invalid, unknown, inconclusive, and policy-failed states without claiming architectural approval or compliance.
- Package install, real MCP handshake, initialization, seven-tool discovery, invocation, invalid-input, impact, drift, shutdown, manifest, and archive-contract tests.

## Install

```shell
dotnet tool install --global RodriOliveira.AdrGuard.Mcp --version 1.0.0
adr-guard-mcp --repository /absolute/path/to/repository --adr-directory docs/adr
```

For a local tool manifest:

```shell
dotnet new tool-manifest
dotnet tool install --local RodriOliveira.AdrGuard.Mcp --version 1.0.0
dotnet tool run adr-guard-mcp -- --repository /absolute/path/to/repository --adr-directory docs/adr
```

.NET 10 is required. Git is required only for impact analysis and commit-aware drift metadata. The server has no network transport, write tools, provider integration, automatic download, ADR approval, or evidence-production capability.

## Publication verification

The release completed the protected `mcp-release` workflow, immutable reservation/release tags, public package verification, and Registry validation. NuGet.org is the recommended installation channel. GitHub Packages remains an optional authenticated secondary channel whose `1.0.0` backfill is a separate manual operation and must not republish the immutable release.

See the [MCP overview](../mcp.md), [configuration](../mcp-configuration.md), [tool contracts](../mcp-tools.md), [security model](../mcp-security.md), and [release prerequisites](../mcp-release.md).
