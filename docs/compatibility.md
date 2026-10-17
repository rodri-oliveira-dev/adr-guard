# ADR format and validation compatibility

Document format and validation rigor are independent. Existing commands default to `canonical` plus `legacy`, preserving the published behavior and exit codes.

| Profile | Lifecycle | Supersession text | Placeholders | Metadata |
| --- | --- | --- | --- | --- |
| `legacy` | Four historical statuses | Off | Off | Optional, unchecked |
| `advisory` | Historical statuses | Off | Warning | Optional, unchecked |
| `standard` | Adds `Rejected` | On | Warning | Validated |
| `strict` | Adds `Rejected` | On | Error | Validated |

Use `adr-format: canonical` or `madr-4` independently with `validation-profile: legacy|advisory|standard|strict`. Explicit lifecycle, filename, placeholder, and metadata settings refine the selected profile. Baseline fingerprints retain diagnostic code, path, and message identity; warnings do not change the exit code. JSON adds `severity` only for warnings, and SARIF emits their standard `warning` level.

## Product compatibility matrix

The products use independent SemVer streams. Equal version numbers are not required and source availability is not publication evidence.

| Product / capability | Repository version | Minimum dependency | Runtime / host | Optional dependencies | Works without MCP? | Publication status checked 2026-10-17 |
| --- | --- | --- | --- | --- | --- | --- |
| CLI / .NET Tool | `1.8.0` | None | .NET 10 | Git only for incremental validation, comparative review, impact, and commit-aware drift metadata; configured AI provider only for `draft`/`review` | Yes | GitHub Release `v1.8.0` published with `.nupkg` asset; independently verify NuGet.org availability. |
| VS Code core commands, Problems, Explorer | `1.1.0` | Compatible CLI `1.3.0+` for the documented baseline command set | VS Code `1.101.0+`; Node workspace extension host; trusted local `file:` workspace | Git/provider requirements come from the selected CLI command | Yes | Source candidate `1.1.0`; last repository-documented Marketplace baseline `1.0.0`. Verify Marketplace listing independently. |
| VS Code visual create/edit | `1.1.0` | CLI `1.7.0+`, form schema `1.0` | Same VS Code boundary | None beyond the CLI | Yes | Prepared as part of extension 1.1.0; the extension probes the contract. |
| VS Code impact, drift, baseline, enriched catalog, draft, review/compare | `1.1.0` | CLI build whose `<command> --help` exposes the requested contract; CLI `1.8.0` is the coordinated recommendation | Same VS Code boundary | Local Git/mappings/evidence or AI provider only when the chosen CLI workflow requires them | Yes | Prepared as part of extension 1.1.0. |
| VS Code MCP provider | `1.1.0` | Separately installed MCP `1.0.0` candidate | VS Code `1.101.0+`, trusted local/remote workspace host | `dotnet` on the extension-host `PATH` when configured with a DLL | Not applicable; this row is the optional integration | Prepared, disabled by default, not published in the Marketplace release yet. |
| MCP server / .NET Tool | `1.0.0` | None; extension is not required | .NET 10, local `stdio` client | Git only for `analyze_impact` and commit metadata | Not applicable; it runs independently | Package, GitHub Release, and Registry entry are prepared but unpublished. |

The CLI runs independently of both the extension and MCP. The extension retains its complete CLI-backed behavior when MCP is disabled, missing, or incompatible. The MCP server runs with any compatible `stdio` client and does not require the extension.

Confirm an installed CLI with `adr-guard --version` plus the relevant command's `--help`. Confirm extension versions in the Visual Studio Marketplace, and MCP versions in both NuGet.org and the MCP Registry. A branch, local package, VSIX, manifest, or release note alone does not establish public availability.

