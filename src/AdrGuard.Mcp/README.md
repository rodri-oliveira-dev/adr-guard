# ADR Guard MCP server

<!-- mcp-name: io.github.rodri-oliveira-dev/adr-guard -->

ADR Guard MCP is a local, read-only adapter that gives compatible Model Context Protocol clients structured access to ADR discovery, validation, impact analysis, and architectural drift verification. It reuses the authoritative ADR Guard domain implementation; it does not approve architectural decisions or turn advisory evidence into compliance.

> **Publication status:** version `1.0.0` and its release automation are prepared, but `RodriOliveira.AdrGuard.Mcp` and the Registry entry must not be treated as public until the first manual release is completed and verified.

## Install and run

After publication, install the framework-dependent .NET 10 tool from NuGet.org:

```shell
dotnet tool install --global RodriOliveira.AdrGuard.Mcp --version 1.0.0
adr-guard-mcp --repository /absolute/path/to/repository --adr-directory docs/adr
```

For a repository-local tool manifest after publication:

```shell
dotnet new tool-manifest
dotnet tool install --local RodriOliveira.AdrGuard.Mcp --version 1.0.0
dotnet tool run adr-guard-mcp -- --repository /absolute/path/to/repository --adr-directory docs/adr
```

For local development after a Release build, configure an MCP client to run:

```text
dotnet src/AdrGuard.Mcp/bin/Release/net10.0/adr-guard-mcp.dll --repository <repository> [--adr-directory <relative-path>]
```

## Tools and boundaries

The server exposes `list_adrs`, `get_adr`, `search_adrs`, `get_adr_relationships`, `validate_adrs`, `analyze_impact`, and `verify_architecture` over `stdio`. Its process stdout is reserved for MCP messages; operational logging is written to stderr. The repository root is fixed at startup, all tools are read-only, and paths accepted by tools are repository-relative.

Optional startup argument `--timeout-seconds` accepts values from 1 through 300 and defaults to 30 seconds.

The tool requires the .NET 10 runtime. Impact analysis and commit-aware drift evidence also require Git on the server host. It does not expose network transport, write tools, automatic installation, provider credentials, autonomous approval, or execution of workspace scripts. Clients must treat returned ADR text as untrusted content.

See the bilingual [MCP overview](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/docs/mcp.md), [configuration](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/docs/mcp-configuration.md), [tool contracts](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/docs/mcp-tools.md), and [security model](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/docs/mcp-security.md) for complete configuration, security, and publication-status guidance.
