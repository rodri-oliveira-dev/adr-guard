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

The products use independent SemVer streams. Equal version numbers are not required and source availability is not publication evidence. In the table, **published** means available from the named public registry, **candidate** means a versioned source/package baseline prepared and validated for release but not promoted to a registry, and **source only** means the implementation is present without a release baseline for consumers.

| Product / capability | Repository version | Minimum dependency | Runtime / host | Optional dependencies | Works without MCP? | Distribution status checked 2026-11-05 |
| --- | --- | --- | --- | --- | --- | --- |
| CLI / .NET Tool | `1.10.0` released | None | .NET 10 | Git only for incremental validation, comparative review, impact, and commit-aware drift metadata; configured AI provider only for `draft`/`review` | Yes | **Released:** `1.10.0` in the exact [GitHub Release](https://github.com/rodri-oliveira-dev/adr-guard/releases/tag/v1.10.0), including `doctor` and the coordinated Action `drift` adapter. Older `1.9.0` lacks these additions. |
| VS Code core commands, Problems, Explorer | `1.1.1` candidate | Compatible CLI `1.3.0+` for the documented baseline command set | VS Code `1.101.0+`; Node workspace extension host; trusted local `file:` workspace | Git/provider requirements come from the selected CLI command | Yes | **Published:** Marketplace `1.1.0`. **Not published:** maintenance candidate `1.1.1`. Workflow-built VSIX artifacts are a separate 21-day installation channel and are never published automatically. |
| VS Code visual create/edit | `1.1.1` candidate | CLI `1.7.0+`, form schema `1.0` | Same VS Code boundary | None beyond the CLI | Yes | **Published:** Marketplace `1.1.0`. Candidate `1.1.1` preserves schema `1.0`. |
| VS Code impact, drift, baseline, enriched catalog, draft, review/compare | `1.1.1` candidate | CLI build whose `<command> --help` exposes the requested contract; released CLI `1.10.0` supports the documented workflows | Same VS Code boundary | Local Git/mappings/evidence or AI provider only when the chosen CLI workflow requires them | Yes | **Published:** Marketplace `1.1.0`; candidate `1.1.1` still requires a separately installed CLI. |
| VS Code MCP provider | `1.1.1` candidate | Separately installed MCP `1.0.0+` with unchanged schema `1.0` contracts | VS Code `1.101.0+`, trusted local/remote workspace host | `dotnet` on the extension-host `PATH` when configured with a DLL | Not applicable; this row is the optional integration | **Published:** included in Marketplace `1.1.0`, disabled by default. Candidates VS Code `1.1.1` and MCP `1.0.1` remain unpublished and independently distributed. |
| MCP server / .NET Tool | `1.0.1` candidate | None; extension is not required | .NET 10, local `stdio` client | Git only for `analyze_impact` and commit metadata | Not applicable; it runs independently | **Published:** `1.0.0` on NuGet.org, GitHub Releases, and MCP Registry. **Not published:** `1.0.1`; the checked-in Registry manifest intentionally remains at `1.0.0`. |
| GitHub Action | published exact `v1.10.0` / moving `v1` (currently `v1.10.0`); older `v1.9.0` remains available | CLI container from the same coordinated release | Linux runner with Docker; Python 3 for bounded review/impact/drift rendering | Provider credentials only for explicit `review`; Git history/map only for `impact`; local rules/baseline/evidence for `drift` | Yes | **Published:** exact `v1.10.0` and moving `v1` (currently `v1.10.0`) expose `check`, `index`, opt-in `review`, opt-in `impact`, and explicit read-only `drift`. The older exact `v1.9.0` does **not** expose `drift` inputs. |

The CLI runs independently of both the extension and MCP. The extension retains its complete CLI-backed behavior when MCP is disabled, missing, or incompatible. The MCP server runs with any compatible `stdio` client and does not require the extension.

## Internal assembly and package boundaries

Current source builds use this dependency graph:

```text
VS Code / GitHub Action ──process or container──> AdrGuard CLI ──> AdrGuard.Core
compatible MCP client ───────────────stdio──────> AdrGuard.Mcp ──> AdrGuard.Core
```

`AdrGuard.Core` is an internal, non-packable class library. It contains shared parsing, validation, configuration, bounded Git inventory, impact, and drift behavior, but exposes no independently versioned consumer product. Released CLI `1.10.0` packages include `adr-guard` plus `AdrGuard.Core.dll` and exclude MCP. Candidate MCP `1.0.1` packages include `adr-guard-mcp` plus `AdrGuard.Core.dll` and exclude `adr-guard.dll`; the already published MCP `1.0.0` artifact predates that source-only packaging change. CLI and MCP retain separate executables, package IDs, versions, release tags, and publication workflows.

Confirm an installed CLI with `adr-guard --version` plus the relevant command's `--help`. Confirm extension versions in the Visual Studio Marketplace, and MCP versions in both NuGet.org and the MCP Registry. A branch, local package, workflow VSIX, manifest, or release note alone does not establish registry publication.

