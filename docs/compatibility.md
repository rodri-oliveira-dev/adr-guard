# ADR format and product compatibility

[Português (Brasil)](compatibility.pt-BR.md)

Document format and validation rigor are independent. Existing commands default to `canonical` plus `legacy`, preserving published behavior and exit codes.

| Profile | Lifecycle | Supersession text | Placeholders | Metadata |
| --- | --- | --- | --- | --- |
| `legacy` | Four historical statuses | Off | Off | Optional, unchecked |
| `advisory` | Historical statuses | Off | Warning | Optional, unchecked |
| `standard` | Adds `Rejected` | On | Warning | Validated |
| `strict` | Adds `Rejected` | On | Error | Validated |

Use `adr-format: canonical` or `madr-4` independently with `validation-profile: legacy|advisory|standard|strict`. Explicit lifecycle, filename, placeholder, and metadata settings refine the selected profile. Native MADR generation is published in CLI `1.11.0`; canonical generation remains the default.

## Product compatibility matrix

The products use independent SemVer streams. Equal version numbers are not required and source availability is not publication evidence. Status was verified against GitHub Releases, NuGet.org, the Visual Studio Marketplace, the MCP Registry, and Git tags on 2026-11-06.

| Product / capability | Repository version | Minimum dependency | Distribution status |
| --- | --- | --- | --- |
| CLI / .NET Tool | `1.12.0` candidate | .NET 10 | Published: `1.11.0` on NuGet.org and GitHub Releases. Prepared, not published: `1.12.0`. |
| Native MADR generation | Included in CLI `1.11.0+` | CLI `1.11.0`; form schema remains `1.0` | Published in `1.11.0`; canonical remains the default. |
| Read-only relationship graph | CLI `1.12.0` candidate | Direct CLI use; schema `1.0` | Prepared, not published. Existing check JSON/SARIF contracts are unchanged. |
| VS Code core, forms, analysis, and optional MCP provider | `1.2.0` candidate | VS Code `1.101.0+`; separately installed CLI appropriate to each command | Marketplace `1.1.1` is published. `1.2.0` is prepared but not published; validated workflow VSIX artifacts are retained for 21 days and never published automatically. |
| VS Code relationship graph | `1.2.0` candidate | CLI `1.12.0` or a compatible build exposing `relationships` schema `1.0` | Prepared, not published. Older or malformed CLIs produce an explicit incompatible state. |
| MCP server / .NET Tool | `1.0.2` candidate | .NET 10; local `stdio` client | `1.0.1` is published on NuGet.org, GitHub Releases, GitHub Packages, and the MCP Registry. `1.0.2` is prepared but not published. |
| GitHub Action | coordinated `v1.12.0` candidate | Linux runner with Docker; matching CLI container | Exact `v1.11.0` and moving `v1` are published. The Action retains `check`, `index`, `review`, `impact`, and `drift`; it does not expose `relationships` or `new`. |

The VS Code extension probes command capabilities rather than trusting a version string. The relationship graph requires `relationships --help` plus a strict schema `1.0` response. It never falls back to parsing ADR Markdown in TypeScript. MCP remains optional and separately installed; the extension retains its CLI-backed behavior when MCP is disabled, missing, or incompatible.

## Internal assembly and package boundaries

```text
VS Code / GitHub Action --process or container--> AdrGuard CLI --> AdrGuard.Core
compatible MCP client --------------stdio------> AdrGuard.Mcp --> AdrGuard.Core
```

`AdrGuard.Core` is internal and non-packable. It has no independent version, package, tag, or release. CLI packages contain `adr-guard` plus `AdrGuard.Core.dll` and exclude MCP. MCP packages contain `adr-guard-mcp` plus `AdrGuard.Core.dll` and exclude `adr-guard.dll`. CLI and MCP retain separate package IDs, versions, tags, manifests, and manual release workflows.

Confirm an installed CLI with `adr-guard --version` and the relevant command's `--help`. Confirm extension versions in the Marketplace and MCP versions in NuGet.org plus the MCP Registry. A branch, local package, workflow artifact, manifest, or release note alone does not establish publication.
