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

| Product / capability | Repository version | Minimum dependency | Runtime / host | Optional dependencies | Works without MCP? | Distribution status checked 2026-10-20 |
| --- | --- | --- | --- | --- | --- | --- |
| CLI / .NET Tool | `1.9.0` candidate | None | .NET 10 | Git only for incremental validation, comparative review, impact, and commit-aware drift metadata; configured AI provider only for `draft`/`review` | Yes | **Published:** `1.8.0` on NuGet.org and GitHub Releases. **Not published:** `1.9.0`; local packages add opt-in diagnostic locations and include internal Core. |
| VS Code core commands, Problems, Explorer | `1.1.0` | Compatible CLI `1.3.0+` for the documented baseline command set | VS Code `1.101.0+`; Node workspace extension host; trusted local `file:` workspace | Git/provider requirements come from the selected CLI command | Yes | **Published:** Marketplace `1.1.0`. Workflow-built VSIX artifacts are a separate 21-day installation channel and are never published automatically. |
| VS Code visual create/edit | `1.1.0` | CLI `1.7.0+`, form schema `1.0` | Same VS Code boundary | None beyond the CLI | Yes | **Published:** Marketplace `1.1.0`. |
| VS Code impact, drift, baseline, enriched catalog, draft, review/compare | `1.1.0` | CLI build whose `<command> --help` exposes the requested contract; published CLI `1.8.0` supports the documented workflows | Same VS Code boundary | Local Git/mappings/evidence or AI provider only when the chosen CLI workflow requires them | Yes | **Published:** Marketplace `1.1.0`; the extension still requires a separately installed CLI. |
| VS Code MCP provider | `1.1.0` | Separately installed MCP `1.0.0+` with unchanged schema `1.0` contracts | VS Code `1.101.0+`, trusted local/remote workspace host | `dotnet` on the extension-host `PATH` when configured with a DLL | Not applicable; this row is the optional integration | **Published:** included in Marketplace `1.1.0`, disabled by default. MCP `1.0.1` remains an unpublished candidate. |
| MCP server / .NET Tool | `1.0.1` candidate | None; extension is not required | .NET 10, local `stdio` client | Git only for `analyze_impact` and commit metadata | Not applicable; it runs independently | **Published:** `1.0.0` on NuGet.org, GitHub Releases, and MCP Registry. **Not published:** `1.0.1`; the checked-in Registry manifest intentionally remains at `1.0.0`. |

The CLI runs independently of both the extension and MCP. The extension retains its complete CLI-backed behavior when MCP is disabled, missing, or incompatible. The MCP server runs with any compatible `stdio` client and does not require the extension.

## Internal assembly and package boundaries

Current source builds use this dependency graph:

```text
VS Code / GitHub Action ──process or container──> AdrGuard CLI ──> AdrGuard.Core
compatible MCP client ───────────────stdio──────> AdrGuard.Mcp ──> AdrGuard.Core
```

`AdrGuard.Core` is an internal, non-packable class library. It contains shared parsing, validation, configuration, bounded Git inventory, impact, and drift behavior, but exposes no independently versioned consumer product. Candidate CLI packages include `adr-guard` plus `AdrGuard.Core.dll`; candidate MCP packages include `adr-guard-mcp` plus `AdrGuard.Core.dll` and exclude `adr-guard.dll`. CLI and MCP therefore share deterministic rules while retaining separate executables, package IDs, versions, release tags, and publication workflows. The source extraction does not retroactively change published CLI 1.8.0, MCP 1.0.0, Marketplace 1.1.0, or Action `@v1` artifacts.

Confirm an installed CLI with `adr-guard --version` plus the relevant command's `--help`. Confirm extension versions in the Visual Studio Marketplace, and MCP versions in both NuGet.org and the MCP Registry. A branch, local package, workflow VSIX, manifest, or release note alone does not establish registry publication.

