# Changelog

All notable changes to ADR Guard for VS Code are documented here. Extension versions are independent from ADR Guard CLI versions.

## 1.1.0 — 2026-10-20

- Navigate Problems diagnostics to optional CLI-provided line and column locations, while retaining the document-start fallback for older CLIs and file-only findings.
- Added optional, default-off MCP registration through the stable VS Code provider API, with one isolated `stdio` definition per trusted local workspace folder and an independently installed MCP executable or built DLL configured at machine scope.
- Added visual, explicitly invoked CLI workflows for Architecture Impact, Architectural Drift, diagnostic baseline creation/update, standard/enriched catalogs, AI-assisted ADR drafting, and advisory Technical Review with `--compare-ref`.
- Added workspace-scoped architecture/governance/AI settings, MCP availability states, bilingual accessible UI, cancellation/stale-response protection, privacy confirmations, and strict versioned JSON parsing.
- Fixed visual impact and drift execution to preserve repository-relative file operands required by the CLI, and stopped unrelated analysis/review workflows from requiring the configured ADR directory to exist.
- Published independently on the Visual Studio Marketplace as `rodrioliveira.adr-guard` 1.1.0. The repository's manual **VS Code VSIX Package** workflow still only validates and builds `adr-guard-<version>.vsix`, retained for 21 days; it does not publish to the Marketplace.
- MCP 1.0.0 remains an independent, optional dependency published separately on NuGet.org and in the MCP Registry.
- See [VSIX packaging and installation](PUBLISHING.md) and the [VS Code 1.1.0 release notes](../../docs/releases/vscode-v1.1.0.md) ([pt-BR](../../docs/releases/vscode-v1.1.0.pt-BR.md)).

## 1.0.1 — 2026-10-15

- Fixed Marketplace screenshots by using stable HTTPS image URLs and added automated local/remote PNG validation.
- Derived local VSIX packaging and smoke-test expectations from the extension manifest to reduce future release-version drift.
- Updated the package, release workflow, documentation, and validation artifact names for the 1.0.1 candidate.

## 1.0.0 — 2026-10-15

- Hardened visual-flow integration with screen-scoped message handling, complete DOM coverage for Back/Cancel and successful returns, explicit save-validation/watcher integration checks, and rollback of earlier VS Code preference writes after a later persistence failure.

- Added a dedicated ADR Guard Activity Bar container that opens or reveals the reusable visual ADR list while retaining Explorer actions for creation, refresh, reveal, and format selection.
- Added repository initialization and installation-check actions to the visual list header, scoped to its selected workspace folder; initialization is disabled with an explanatory tooltip after ADRs exist.
- Added an in-panel settings screen for user-scoped Webview language and workspace-folder-scoped document language, creation template, ADR directory, and validation format, with multi-root support, dirty-state confirmation, safe path validation, and no ADR or CLI side effects while navigating or saving preferences.
- Completed visual settings with workspace-folder validation-on-save, debounce, safe baseline and incremental Git-reference preferences plus machine-scoped executable, timeout, output limits, and an explicit version-reporting installation check; settings navigation and persistence never trigger ADR validation.
- Creation forms now start from the selected workspace folder's persisted language and template defaults while retaining per-document overrides and the traditional custom-template flow.
- Fixed template loading so a successful CLI description uses an explicit Webview message and rendering failures no longer leave the form indefinitely loading.
- Fixed **Back to ADR list** so it accepts empty forms, remains compatible while an older Extension Host is still loaded, normalizes legacy saved list state, cancels pending form work, waits for host acknowledgement before discarding values, and ignores stale form responses after navigation.
- Added in-place canonical and MADR 4.0 editing from every visual list item, reusing the existing panel/form controls with dynamic CLI-described fields, dirty-state confirmation, progress/errors, refreshed list highlighting, and safe Markdown opening.
- Added strict adaptation for the CLI schema `1.0` `edit --form` contract, including workspace/catalog path revalidation, bounded messages, duplicate-save protection, format-aware validation, untouched-content preservation, and SHA-256 conflict detection.
- Added **ADR Guard: Open ADR Guard** as the visual home screen and ADR Explorer entry, with a reusable single panel, multi-root folder selection, case-insensitive search, status filters, deterministic ordering, counts, refresh, safe Markdown opening, and explicit empty/error states.
- Integrated the existing creation form into the list flow, preserving list state on return, refreshing the shared Explorer catalog, highlighting newly created ADRs, reporting filtered-out creations, confirming controlled dirty-form navigation, and no longer opening Markdown automatically after visual creation.
- Reused the Explorer catalog, metadata parser, path protections, cache invalidation, and batched file watchers; added date display metadata, stale-response protection, per-file degradation, responsive accessible styling, and English/pt-BR strings.
- Added **ADR Guard: Create ADR with Form** to the Command Palette and ADR Explorer, with multi-root workspace selection, native minimal/extended fields, document-language selection, and a side-effect-free Markdown source preview.
- Added strict schema `1.0` CLI adaptation over bounded stdin, duplicate-submit protection, safe created-path verification, and explicit fallback to the unchanged traditional flow for missing/incompatible CLIs or custom templates.
- Added English and Brazilian Portuguese UI, theme/high-contrast styling, keyboard-accessible labels and focus, restrictive CSP, nonce-protected local scripts, and text-only preview rendering.
- Requires ADR Guard CLI 1.7.0 or newer with the `new --form` contract; the existing **Create ADR** command remains compatible with older CLIs.

## 0.1.1 — 2026-10-08

First manual-publication candidate:

- Secure discovery and bounded execution of the ADR Guard CLI in trusted local workspace hosts.
- Native commands for installation checks, repository initialization, ADR creation, validation, and index generation.
- Problems diagnostics from the versioned CLI JSON contract and opt-in incremental save validation.
- Native ADR Explorer with canonical/MADR 4.0 display metadata and safe relationship navigation.
- Explicit changed-ADR and read-only baseline validation adapters for the CLI contracts consolidated into NuGet v1.3.0.
- Multi-root workspace handling, cancellation, stale-result protection, English/pt-BR localization, deterministic tests, isolated CI, and minimal VSIX packaging.
- Forced process-tree termination and a final settlement deadline when a CLI ignores graceful termination or descendants retain inherited output pipes.
- Workspace diagnostics now remain visible when an editor closes and are cleared only by lifecycle or validation events that invalidate them.

The extension has not yet been published to the Visual Studio Marketplace. Its Marketplace publisher ID is registered as `rodrioliveira`. Advanced MADR, relationship, changed-file, and baseline features require the compatible CLI published as NuGet v1.3.0. The CLI is already public; the extension remains unpublished in the Marketplace.

Manual publication remains a separately authorized owner action. No publication is triggered by ordinary CI, pull requests, pushes, or tags.

## 0.1.0 — 2026-10-08

Review-only MVP package; not published to the Visual Studio Marketplace.
