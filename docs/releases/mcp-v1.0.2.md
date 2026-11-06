# ADR Guard MCP v1.0.2

[Português (Brasil)](mcp-v1.0.2.pt-BR.md)

<!-- release-candidate-only:start -->
ADR Guard MCP `1.0.2` is prepared in source control but is **not published**. MCP `1.0.1` remains available on NuGet.org, GitHub Releases, and the official MCP Registry. This patch is released independently from CLI `1.12.0` and VS Code `1.2.0`.
<!-- release-candidate-only:end -->

## Changes

- Reuses the shared deterministic `AdrGuard.Core` relationship graph implementation in the existing `get_adr_relationships` tool.
- Keeps declared section, conventional status, and MADR supersession discovery aligned with CLI validation while preserving format-specific behavior.
- Normalizes MADR declared targets consistently and reuses the authoritative set of relationship diagnostic codes.

## Compatibility and boundaries

- The same seven read-only tools, local `stdio` transport, startup arguments, schema version `1.0`, structured fields, response limits, error semantics, and annotations are preserved.
- No MCP tool was added or removed. The server does not create, edit, approve, supersede, or change the status of ADRs and never infers undeclared relationships.
- The package includes `adr-guard-mcp` and `AdrGuard.Core.dll`, excludes the CLI executable, requires .NET 10, and remains independently installable.

## Release process

<!-- release-candidate-only:start -->
After CLI `1.12.0` has completed its coordinated release and public verification, dispatch **MCP Release** on `main` with `version=1.0.2`. The protected workflow validates, tests, packs, installs, smoke-tests all seven tools, reserves the version, and then publishes to NuGet.org, GitHub Packages, GitHub Releases, and the official MCP Registry through its gated jobs.
<!-- release-candidate-only:end -->

Merging this preparation does not publish the MCP package, manifest, Registry entry, tag, or release.
