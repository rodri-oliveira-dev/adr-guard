# ADR format and validation compatibility

Document format and validation rigor are independent. Existing commands default to `canonical` plus `legacy`, preserving the published behavior and exit codes.

| Profile | Lifecycle | Supersession text | Placeholders | Metadata |
| --- | --- | --- | --- | --- |
| `legacy` | Four historical statuses | Off | Off | Optional, unchecked |
| `advisory` | Historical statuses | Off | Warning | Optional, unchecked |
| `standard` | Adds `Rejected` | On | Warning | Validated |
| `strict` | Adds `Rejected` | On | Error | Validated |

Use `adr-format: canonical` or `madr-4` independently with `validation-profile: legacy|advisory|standard|strict`. Explicit lifecycle, filename, placeholder, and metadata settings refine the selected profile. Baseline fingerprints retain diagnostic code, path, and message identity; warnings do not change the exit code. JSON adds `severity` only for warnings, and SARIF emits their standard `warning` level.

MADR 4.0 validation is published and remains opt-in. Native MADR generation through `new` and `new --form` is assigned to the prepared CLI `1.11.0` candidate but has no published package or image yet. Canonical generation remains the default in both cases.

## Product compatibility matrix

The products use independent SemVer streams. Equal version numbers are not required and source availability is not publication evidence. In the table, **published** means available from the named public registry, **candidate** means a versioned source/package baseline prepared and validated for release but not promoted to a registry, and **source only** means the implementation is present without a release baseline for consumers.

| Product / capability | Repository version | Minimum dependency | Runtime / host | Optional dependencies | Works without MCP? | Distribution status checked 2026-11-05 |
| --- | --- | --- | --- | --- | --- | --- |
| CLI / .NET Tool | `1.11.0` candidate | None | .NET 10 | Git only for incremental validation, comparative review, impact, and commit-aware drift metadata; configured AI provider only for `draft`/`review` | Yes | **Published:** `1.10.0` on NuGet.org and in the exact `v1.10.0` GitHub Release. **Not published:** `1.11.0`, including native MADR generation. |
| Native MADR 4.0 generation (`new`, `new --form`) | `1.11.0` candidate | Candidate CLI `1.11.0`; structured clients continue to use form schema `1.0` | .NET 10 | None | Yes | **Published:** MADR validation only, since CLI `1.3.0`. **Not published:** native MADR generation in any package, image, Action tag, or release. Canonical generation remains the default. |
| VS Code core commands, Problems, Explorer | `1.1.1` candidate | Compatible CLI `1.3.0+` for the documented baseline command set | VS Code `1.101.0+`; Node workspace extension host; trusted local `file:` workspace | Git/provider requirements come from the selected CLI command | Yes | **Published:** Marketplace `1.1.0`. **Not published:** maintenance candidate `1.1.1`. Workflow-built VSIX artifacts are a separate 21-day installation channel and are never published automatically. |
| VS Code visual create/edit | `1.1.1` candidate | CLI `1.7.0+`, form schema `1.0` | Same VS Code boundary | None beyond the CLI | Yes | **Published:** Marketplace `1.1.0`. Candidate `1.1.1` preserves schema `1.0`. |
| VS Code impact, drift, baseline, enriched catalog, draft, review/compare | `1.1.1` candidate | CLI build whose `<command> --help` exposes the requested contract; published CLI `1.10.0` supports the documented workflows | Same VS Code boundary | Local Git/mappings/evidence or AI provider only when the chosen CLI workflow requires them | Yes | **Published:** Marketplace `1.1.0`; candidate `1.1.1` still requires a separately installed CLI. |
| VS Code MCP provider | `1.1.1` candidate | Separately installed MCP `1.0.0+` with unchanged schema `1.0` contracts | VS Code `1.101.0+`, trusted local/remote workspace host | `dotnet` on the extension-host `PATH` when configured with a DLL | Not applicable; this row is the optional integration | **Published:** included in Marketplace `1.1.0`, disabled by default. MCP `1.0.1` is published independently on NuGet.org and GitHub Releases; VS Code `1.1.1` remains unpublished. |
| MCP server / .NET Tool | `1.0.1` released | None; extension is not required | .NET 10, local `stdio` client | Git only for `analyze_impact` and commit metadata | Not applicable; it runs independently | **Published:** `1.0.1` on NuGet.org and in the exact `mcp/v1.0.1` GitHub Release. The checked-in MCP Registry manifest remains at `1.0.0`; verify the Registry independently. |
| Architecture Agent | unversioned development source | Compatible MCP schema `1.0`; local CLI build only for supervised `Proposed` creation | .NET 10, local console process | OpenAI provider/model and `OPENAI_API_KEY` only for remote synthesis | No; MCP is its evidence boundary | **Source only, verified 2026-11-13:** no `RodriOliveira.AdrGuard.Agent` package on NuGet.org, installer, container, manifest, release tag, or supported clean-install channel. Local read-only mode works without provider credentials or the CLI. |
| GitHub Action | published exact `v1.10.0` / moving `v1`; `v1.11.0` candidate | CLI container from the same coordinated release | Linux runner with Docker; Python 3 for bounded review/impact/drift rendering | Provider credentials only for explicit `review`; Git history/map only for `impact`; local rules/baseline/evidence for `drift` | Yes | **Published:** current `v1` and `v1.10.0` expose `check`, `index`, opt-in `review`, opt-in `impact`, and read-only `drift`. **Not published:** `v1.11.0`; the Action does not expose the `new` command or native MADR generation. |

The CLI runs independently of both the extension and MCP. The extension retains its complete CLI-backed behavior when MCP is disabled, missing, or incompatible. The MCP server runs with any compatible `stdio` client and does not require the extension.

## Internal assembly and package boundaries

Current source builds use this dependency graph:

```text
VS Code / GitHub Action ──process or container──> AdrGuard CLI ──> AdrGuard.Core
compatible MCP client ───────────────stdio──────> AdrGuard.Mcp ──> AdrGuard.Core
AdrGuard.Agent ──local stdio──> AdrGuard.Mcp; ──authorized process──> AdrGuard CLI
```

`AdrGuard.Core` is an internal, non-packable class library. It contains shared parsing, validation, configuration, bounded Git inventory, impact, and drift behavior, but exposes no independently versioned consumer product. Published CLI `1.10.0` and candidate CLI `1.11.0` packages include `adr-guard` plus `AdrGuard.Core.dll` and exclude MCP. Published MCP `1.0.1` packages include `adr-guard-mcp` plus `AdrGuard.Core.dll` and exclude `adr-guard.dll`. CLI and MCP retain separate executables, package IDs, versions, release tags, and publication workflows.

Confirm an installed CLI with `adr-guard --version` plus the relevant command's `--help`. Confirm extension versions in the Visual Studio Marketplace, and MCP versions in both NuGet.org and the MCP Registry. A branch, local package, workflow VSIX, manifest, or release note alone does not establish registry publication.

