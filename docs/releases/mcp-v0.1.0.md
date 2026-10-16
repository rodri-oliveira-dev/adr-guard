# ADR Guard MCP v0.1.0 release candidate

[Português (Brasil)](mcp-v0.1.0.pt-BR.md)

This is the prepared release note for the first independently versioned ADR Guard MCP distribution. It is not evidence that the package, GitHub Release, or Registry entry has been published.

## Included

- Seven bounded read-only tools: `list_adrs`, `get_adr`, `search_adrs`, `get_adr_relationships`, `validate_adrs`, `analyze_impact`, and `verify_architecture`.
- A framework-dependent .NET 10 Tool named `RodriOliveira.AdrGuard.Mcp`, invoked as `adr-guard-mcp` over `stdio`.
- The same authoritative parsing, validation, impact, drift, configuration, and Git adapters used by ADR Guard; no parallel rule engine.
- Isolated package installation and MCP handshake tests on Windows, Linux, and macOS release runners.

## Install after publication

```shell
dotnet tool install --global RodriOliveira.AdrGuard.Mcp --version 0.1.0
adr-guard-mcp --repository /absolute/path/to/repository --adr-directory docs/adr
```

.NET 10 is required. Git is required for impact analysis and commit-aware drift evidence. The server has no network transport, write tools, provider integration, automatic downloads, or authority to approve ADRs. See the [MCP overview](../mcp.md), [configuration](../mcp-configuration.md), and [security model](../mcp-security.md).
