# ADR Guard MCP changelog

ADR Guard MCP is versioned independently from the ADR Guard CLI and VS Code extension. Prepared versions are not public until the manual release workflow completes and the NuGet package, GitHub Release, and Registry entry are independently verified.

## Unreleased

- No additional MCP changes since the `1.0.1` release.

## 1.0.1 — 2026-11-05 (published)

- Reference the internal, non-packable `AdrGuard.Core` assembly directly while remaining independent from the CLI executable.
- Keep all seven read-only schema `1.0` tools, `stdio` transport, startup arguments, result contracts, limits, and error semantics unchanged.
- Resolve physical Git-directory aliases before containment checks so valid macOS repositories are not rejected.
- Package MCP plus Core and explicitly exclude `adr-guard.dll`; Core has no independent package or release stream.
- Prepare authenticated GitHub Packages distribution for future MCP versions and a separate, provenance-verified backfill for published 1.0.0; no automatic republishing of existing versions.
- The checked-in `.mcp/server.json` now records the published Registry version `1.0.1`; the protected workflow generates and validates manifests for future releases.
- See the [1.0.1 release notes](../../docs/releases/mcp-v1.0.1.md).

## 1.0.0 — 2026-10-17

- Prepared the first stable public contract for seven bounded, read-only schema `1.0` tools over local `stdio`.
- Stabilized Package ID `RodriOliveira.AdrGuard.Mcp`, command `adr-guard-mcp`, and Registry name `io.github.rodri-oliveira-dev/adr-guard`.
- Added framework-dependent .NET 10 Tool packaging, embedded Registry ownership marker, isolated installation, real MCP handshake/tool-discovery tests, and manifest/package contract tests.
- Added repository/path isolation, bounded ADR/search/evidence/response/timeout behavior, cancellation, sanitized operational errors, and explicit advisory/unknown semantics.
- Fixed relationship discovery for nested ADRs whose relative links resolve elsewhere inside the configured repository and ADR catalog.
- Prepared a manual, main-only, OIDC-based, fail-closed release workflow for NuGet.org, GitHub Releases, and the MCP Registry. No publication is performed by ordinary CI or by this version change.

## 0.1.0 — Superseded release candidate; never published

- Initial source and distribution-preparation baseline. The planned public first release was promoted to 1.0.0 after contract and distribution readiness review.
