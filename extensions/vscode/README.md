# ADR Guard for VS Code

ADR Guard for VS Code is a workspace extension that presents the existing ADR Guard CLI inside the editor. It provides native commands, Problems diagnostics, and a dedicated Activity Bar container with the ADR Explorer while leaving parsing, validation, policy, generation, and Git-aware analysis in the .NET CLI.

> **Published extension:** [ADR Guard for VS Code](https://marketplace.visualstudio.com/items?itemName=rodrioliveira.adr-guard) is available on the Visual Studio Marketplace as `rodrioliveira.adr-guard`. The VS Code extension and .NET CLI are distributed and versioned separately; the extension does not bundle the CLI.

> **Release status (verified 2026-10-20):** version 1.1.0 is published in the Marketplace and adds the MCP provider and visual Impact, Drift, baseline, enriched catalog, AI Draft, and Technical Review workflows described below. MCP 1.0.0 is published separately on NuGet.org and in the MCP Registry; MCP 1.0.1 remains an unpublished candidate.

## Features

- Check that the CLI is discoverable in the workspace extension host.
- Initialize an ADR repository through a dry-run preview and explicit confirmation.
- Create ADRs with the CLI's configured, minimal, extended, or workspace-local custom template.
- Create ADRs through a guided visual form with a Markdown source preview when the configured template is native.
- Browse, search, filter, sort, and open ADRs from the visual ADR Guard home screen.
- Validate ADRs and publish versioned JSON findings in the VS Code Problems panel.
- Opt in to debounced validation after saving an ADR.
- Generate an ADR index only after explicit confirmation.
- Browse canonical and MADR 4.0 files in the native Explorer, including safe display-only status and relationship navigation.
- Run Git-aware changed-file validation against an explicit local base ref, with no hidden fetch.
- Classify diagnostics against an existing read-only baseline.
- Explicitly run visual Architecture Impact and Architectural Drift reports through strict CLI JSON contracts.
- Explicitly create or update diagnostic baselines and generate standard or enriched catalogs.
- Preview AI-assisted drafts and run advisory technical reviews with selected providers and context.
- Optionally register a separate, local read-only MCP server with compatible VS Code agents.
- Use English UI by default and Brazilian Portuguese when VS Code uses `pt-BR`.

## Interface preview

The images below document the visual interface introduced before and retained by the published extension 1.1.0, captured in a real Extension Development Host with a compatible CLI. Visual creation and editing require the CLI 1.7.0 schema `1.0` form contracts. The architecture, governance, AI, and MCP screens are described textually because no additional verified capture is available; the existing images are not presented as evidence of those screens.

### Browse and search

![Visual ADR list with status, search, sorting, edit, and Markdown actions](https://raw.githubusercontent.com/rodri-oliveira-dev/adr-guard/main/website/public/images/vscode/adr-list.png)

### Create with a native template

![Minimal ADR creation form with document setup and decision content](https://raw.githubusercontent.com/rodri-oliveira-dev/adr-guard/main/website/public/images/vscode/adr-create-minimal.png)

### Edit a supported ADR

![ADR edit form with title, status, and document sections](https://raw.githubusercontent.com/rodri-oliveira-dev/adr-guard/main/website/public/images/vscode/adr-edit.png)

### Configure the extension

![ADR Guard settings for language, repository directory, and ADR format](https://raw.githubusercontent.com/rodri-oliveira-dev/adr-guard/main/website/public/images/vscode/adr-settings.png)

The portal's [VS Code Visual Guide](https://rodri-oliveira-dev.github.io/adr-guard/product/vscode-visual-guide/) covers the Explorer, filters, Minimal and Extended creation, Markdown preview, external-change-safe editing, validation settings, CLI integration, and Problems diagnostics.

## Requirements and installation

- VS Code `1.101.0` or newer in the `1.x` line.
- A trusted local `file:` workspace.
- A compatible `adr-guard` executable installed in the environment where the **workspace extension host** runs.

Install the compatible CLI (v1.3.0 or newer) in the local or remote workspace extension host environment:

```shell
dotnet tool install --global RodriOliveira.AdrGuard
adr-guard --version
```

For unreleased development capabilities, build the CLI from the corresponding repository branch and set `adrGuard.cli.path` to its trusted absolute executable path. Otherwise the extension searches absolute directories on the extension host `PATH`. It never downloads or installs the CLI.

Install the published extension from the VS Code **Extensions** view (search **ADR Guard**) or run:

```shell
code --install-extension rodrioliveira.adr-guard
```

For manual testing, download the latest validated VSIX from [VS Code VSIX Package workflow artifacts](https://github.com/rodri-oliveira-dev/adr-guard/actions/workflows/vscode-extension-release.yml), extract the archive, and use **Extensions: Install from VSIX...** to select `adr-guard-<version>.vsix`. Artifacts are kept for **21 days**. This workflow does **not** publish to the Marketplace; see [packaging instructions](PUBLISHING.md).

## Commands

Open the Command Palette and choose the command under **ADR Guard**:

| Command | Behavior |
| --- | --- |
| Check Installation | Runs `adr-guard --version` using safe discovery. |
| Initialize Repository | Previews `init --dry-run`, then asks before writing. Existing files are not overwritten. |
| Create ADR | Runs the compatible traditional `new` flow and opens the verified created file. |
| Open ADR Guard | Opens or reveals the visual ADR list without creating files or running validation. |
| Create ADR with Form | Opens the guided form, previews through the CLI, then creates only after explicit submission. |
| Validate ADRs | Runs a full JSON validation and updates Problems. |
| Validate Changed ADRs | Uses `--changed --base-ref`; it never performs `git fetch`. |
| Validate ADRs with Baseline | Reads the configured existing baseline and reports new/existing/resolved counts. |
| Generate ADR Index | Warns before the CLI can update the ADR `README.md`. |
| Select ADR Format | Selects `canonical` or `madr-4` for the workspace folder. |
| Refresh ADR Explorer | Invalidates the Explorer cache and reloads it. |
| Reveal Current ADR | Selects the active ADR in the Explorer. |

Multi-root workspaces are supported. Commands use the active file's folder when possible and otherwise ask which folder to use. Settings with resource scope can differ by workspace folder.

## Visual ADR list

Run **ADR Guard: Open ADR Guard** from the Command Palette or the ADR Explorer title. The reusable visual panel starts on the selected workspace folder's configured ADR directory and shows available identifier, title, status, date, and relative path metadata. Search is case-insensitive across title, identifier, and path; status filters and deterministic number/title/date ordering run locally without invoking full CLI validation. Missing metadata remains visibly unknown and unnumbered records sort after numbered ADRs.

Clicking the dedicated ADR Guard icon in the Activity Bar also opens or reveals this visual list. The same panel is reused, so repeated clicks do not create duplicate tabs.

The list header groups refresh, installation check, repository initialization, creation, and a settings gear. Initialization is available only while the configured directory has no ADRs; once ADRs exist, the disabled control explains that the repository is already initialized.

The list distinguishes loading, empty, filtered-empty, missing-directory, unavailable-workspace, and read-error states. It never creates a directory or initializes a repository while browsing. File changes are batched by the shared ADR Explorer watcher, and switching workspace folders cannot allow an older response to replace the current folder.

**New ADR** opens the existing form in the same panel and keeps the folder, directory, search, filter, order, and scroll state for the return. Successful creation returns to the refreshed list, highlights the new ADR, refreshes the native Explorer, and offers **Open Markdown** without taking focus away automatically. If current filters hide the new record, the panel says so and offers an explicit reveal action. Cancel and **Back to ADR list** return without writing; leaving a dirty form through these controlled actions requires confirmation. The direct **Create ADR with Form** command remains available and returns to the list as well.

Back and Cancel are navigation-only actions in creation, editing, and settings. They never invoke `new`, `edit --save`, `check`, or `index`; while a create, edit, or settings save is active, those controls remain disabled to prevent a competing transition. Refresh, search, filters, sorting, workspace selection, and Explorer file watchers likewise refresh read-only catalog state without starting ADR validation.

Each list item also provides **Edit**. The same visual panel loads the selected canonical or MADR 4.0 document through the CLI's schema `1.0` `edit --form` contract, exposes its supported title, status, metadata, and level-two sections, and returns to the preserved list after saving or cancelling. The CLI performs format-aware validation and localized Markdown replacement, keeps the path and identifier unchanged, preserves untouched metadata/sections/spacing, and rejects a save when the file fingerprint changed after loading. The extension revalidates workspace and catalog boundaries before every load or save.

## Visual ADR creation form

Run **ADR Guard: Create ADR with Form** from the Command Palette or use the new-file action in the ADR Explorer title. The form keeps workspace folder and document language separate from the VS Code interface language, initializes the destination from `adrGuard.validation.directory`, uses the folder's default document language and creation template, and still permits per-document changes.

The form supports the native `minimal` and `extended` creation templates. Minimal requires Context, Decision, and Consequences. Extended exposes those required fields plus optional Decision Drivers, Options Considered, Rationale, Positive/Negative Consequences, Risks, and References; optional empty sections are omitted. Creation remains canonical—the `canonical`/`madr-4` validation format setting is a separate concern and is not presented as a creation-template choice.

Configured custom templates cannot be mapped to fields safely. The form reports that limitation and offers **Use traditional creation** without replacing the custom template. The traditional **Create ADR** command remains available for all supported custom templates and older CLIs.

Visual creation requires CLI 1.7.0 or newer with the schema `1.0` `new --form` contract. Preview and creation use the same CLI renderer; preview does not write, reserve a number, or update the index, while creation resolves the final number atomically and never overwrites an existing file. Form state remains in the retained panel while it is hidden. VS Code does not provide a cancellable WebviewPanel close event, so closing the panel cannot show a guaranteed unsaved-changes confirmation and discards its in-memory state.

## Visual settings

Use the list header's settings gear to configure the existing panel without opening another tab. Interface language is stored in user settings as Automatic (VS Code), Portuguese (Brazil), or English. Automatic follows the editor locale for ADR Guard Webview text only; native VS Code commands and controls keep the editor's own locale. Default document language and creation template are independent workspace-folder settings, so multi-root folders may differ.

The Repository section reuses `adrGuard.validation.directory` and `adrGuard.validation.adrFormat`. The directory must be a safe relative path whose existing path ancestry stays within the selected workspace. Saving preferences does not create, move, delete, validate, or index ADR files. Back and Cancel preserve the list state and require confirmation before discarding changed settings.

Settings are committed through the VS Code configuration API at their documented user/machine or workspace-folder scopes. If one write in the group fails, already applied writes are rolled back and the draft remains available for retry; the panel reports the operation as failed rather than presenting partial persistence as success.

The Validation section configures opt-in validation on save, its 200-5000 ms debounce, the read-only baseline path, and the default local Git reference suggested by incremental validation. Turning save validation off preserves the debounce value. The incremental command still lets you edit the reference before execution and never runs `git fetch`. A settings change only updates VS Code preferences; validation runs later only for an applicable file-save event or an explicit validation command.

The CLI Integration section exposes the machine-scoped executable, timeout, and output limit. An empty executable uses secure discovery on the extension host `PATH`; configured values must be absolute local paths and continue through the existing executable validation. Timeout and output limits remain inside the collapsible advanced section. **Check installation** is always explicit, uses the last saved CLI settings, and reports the detected version or actionable failure without installing or updating anything.

## Visual architecture, governance, and AI workflows

The reusable ADR Guard panel exposes these published 1.1.0 workflows as internal Webview actions; they are not additional Command Palette commands:

| Area | CLI invocation | Visual behavior and boundary |
| --- | --- | --- |
| Architecture Impact | `impact <repository> --base-ref <ref> --map <file> --format json` | Shows affected decisions, changed files, evidence, and unmapped/unknown changes. It never fetches Git or labels impact as a violation. |
| Architectural Fitness | `drift <repository> --rules <file> [--evidence <file>] [--baseline <file>] --format json --policy advisory\|enforce` | Preserves `pass`, `violation`, `unknown`, `not-applicable`, confidence, and baseline state. It never runs evidence producers. |
| Diagnostic Baselines | `baseline <directory> --output <file> [--update]` | Creation is explicit. Replacing an existing baseline requires confirmation before `--update`; navigation and cancellation never write. |
| ADR Catalog | `index <directory> [--catalog enriched]` | Keeps the standard index and adds the CLI's enriched mode. Every write requires confirmation; no unsupported dry run is simulated. |
| AI-Assisted ADR Draft | `draft ... --preview`, then an explicit creation invocation | Requires provider/model and selected context, displays a privacy confirmation, and renders returned Markdown as text. Creation may call the provider again and differ from preview; it never auto-accepts an ADR. |
| AI-Assisted Technical Review | `review <ADR> ... --format json [--compare-ref <ref>]` | Presents eight dimensions, findings, evidence, uncertainty, recommendations, limitations, and comparison reference. It never edits or changes ADR status. |

Every workflow checks capability through `<command> --help` before execution, so an older CLI receives no unsupported command. Requests use literal argument arrays with `shell: false`; paths are normalized and constrained to the selected workspace. Operations require an explicit action, prevent duplicate submissions, propagate cancellation where supported, and ignore stale responses. **Back** and **Cancel** never invoke the CLI or a provider. Dirty forms require confirmation before controlled navigation discards them.

AI provider choices are `openai`, `anthropic`, `gemini`, and `openai-compatible`. The extension stores provider, model, and optional compatible endpoint preferences, but no credential. Authentication remains in the CLI's environment/secret boundary. Context files and existing ADR inclusion are opt-in for each request; confirmation identifies the provider, model, and selected context before external transmission.

These visual workflows always use the CLI directly and work with MCP enabled, disabled, missing, or unavailable. See the existing [impact](../../docs/guides/architecture-impact-analysis.md), [drift](../../docs/guides/architectural-drift.md), [baseline](../../docs/incremental-validation.md), [draft](../../docs/draft-templates.md), and [review](../../docs/adr-review.md) guides for command contracts.

## MCP integration

Install the published package with `dotnet tool install --global RodriOliveira.AdrGuard.Mcp --version 1.0.0`. This normally creates a host under `$HOME/.dotnet/tools/adr-guard-mcp` on Unix or `%USERPROFILE%\.dotnet\tools\adr-guard-mcp.exe` on Windows. Resolve the host-local absolute path with `command -v adr-guard-mcp` or `(Get-Command adr-guard-mcp).Source` and store that value in `adrGuard.mcp.path`. The extension intentionally does not assume that the extension host inherited the interactive shell `PATH`.

The optional MCP provider is disabled by default. In a trusted local `file:` workspace, set the machine-scoped `adrGuard.mcp.path` to an absolute `adr-guard-mcp.dll` or platform app host outside every open workspace, then enable `adrGuard.mcp.enabled` for the intended folder. The settings screen distinguishes disabled, not configured, not found, invalid, available to start, and known-error states. Its explicit check validates configuration only; it never reports “connected” from a valid path.

The provider ID is `adrGuard.mcpServers`. It returns one labeled `McpStdioServerDefinition` per enabled workspace folder, with an isolated repository root and ADR directory, and lets VS Code own process startup/shutdown. Activation, list/settings navigation, and unrelated preference changes start no process. Configuration changes refresh definitions. Disabling MCP removes the definition without affecting CLI commands.

For a DLL, `dotnet` must be on the workspace extension host `PATH`. In WSL, Remote-SSH, or Dev Containers, configure a path that exists on that remote host. Workspace-scoped executable paths, symlinks, executables inside an open workspace, virtual workspaces, UNC paths on Windows, and shell execution are rejected. The extension does not download the server or write `.vscode/mcp.json`.

See the source-only [MCP overview](../../docs/mcp.md), [configuration](../../docs/mcp-configuration.md), [tools](../../docs/mcp-tools.md), and [security model](../../docs/mcp-security.md). MCP and Agent Skills are complementary: MCP supplies structured tool results, while skills supply reusable agent instructions.

## ADR Explorer and formats

The **Architecture Decision Records** view appears in the dedicated ADR Guard container in the Activity Bar, not in VS Code's built-in Explorer container. It scans the configured ADR directory, ignores its generated `README.md`, and displays a bounded subset of title, status, and recognized Markdown relationships. Those fields are navigation metadata only; the CLI remains authoritative for canonical and MADR 4.0 structure and governance.

Relationship targets must resolve to regular Markdown files within the selected workspace and ADR directory. Ambiguous, missing, external, virtual, or symlink-escaping targets are not opened.

## Diagnostics, save validation, and baseline

`Validate ADRs` opts into `--diagnostic-locations` and maps schema `1.0` CLI JSON findings to Problems. Optional one-based `line` and `column` report values are converted to zero-based VS Code coordinates and navigate directly to the parsed Markdown location. If an older CLI rejects the optional flag, the extension retries without it; file-only findings then use `(0, 0)` only because no position was proven. Malformed JSON, incompatible schemas, partial or invalid positions, contradictory exit codes, files outside the ADR directory, and CLI exits `2`–`4` after compatibility handling are operational failures rather than clean results.

`adrGuard.validation.onSave` defaults to `false`. When enabled, only saved Markdown ADRs below the configured directory trigger validation; save bursts are debounced, older runs are cancelled, and stale results cannot replace newer diagnostics. Save validation never creates ADRs, generates an index, runs AI, fetches Git refs, or writes a baseline.

Baseline validation uses the existing `adrGuard.validation.baseline` JSON file read-only. The visual Governance workflow separately exposes explicit baseline creation and confirmed update; it never changes a baseline merely to make validation pass.

## Workspace Trust, remote environments, and security

The extension declares untrusted and virtual workspaces unsupported. It does not execute a process until the workspace is trusted and the selected folder resolves to a local `file:` path. The configured executable setting has machine scope; PATH candidates controlled by an open workspace are rejected. CLI arguments are passed as a literal array with `shell: false`, and execution has cancellation, timeout, and independent stdout/stderr limits.

In WSL, Remote-SSH, and Dev Containers the workspace extension runs remotely. Install the CLI and configure its path **inside that remote environment**, not only on the local UI machine. Pure VS Code Web and virtual workspaces without a Node workspace extension host and local filesystem are not supported.

ADR Guard for VS Code does not collect telemetry, install software, expose AI credentials to the Webview, invoke AI automatically, modify ADRs on save, write baselines automatically, or run hidden network/Git fetch operations. Explicit `init`, `new`, `edit`, `baseline`, draft creation, and `index` operations can write only after the confirmations described above.

## Settings

The manifest is authoritative. Resource values are saved per workspace folder by the visual settings screen; machine values belong to the local or remote extension host.

| Setting | Type / default | Scope | Purpose and restrictions |
| --- | --- | --- | --- |
| `adrGuard.cli.path` | string / `""` | machine | Trusted absolute CLI path; empty uses bounded `PATH` discovery. |
| `adrGuard.cli.timeoutMilliseconds` | number / `15000` | machine | Process timeout, `1000`–`120000`. |
| `adrGuard.cli.maxOutputBytes` | number / `262144` | machine | Independent stdout/stderr ceiling, `1024`–`1048576`. |
| `adrGuard.mcp.enabled` | boolean / `false` | resource | Offers an MCP definition for that trusted local folder. |
| `adrGuard.mcp.path` | string / `""` | machine | Absolute local path to `adr-guard-mcp.dll` or its platform app host. |
| `adrGuard.ui.language` | `auto`, `pt-BR`, `en-US` / `auto` | window | Webview language, independent from document language. |
| `adrGuard.creation.documentLanguage` | `pt-BR`, `en-US` / `en-US` | resource | Default language for new ADRs. |
| `adrGuard.creation.defaultTemplate` | `configured`, `minimal`, `extended` / `configured` | resource | Default creation template. |
| `adrGuard.validation.directory` | string / `docs/adr` | resource | Safe workspace-relative ADR directory. |
| `adrGuard.validation.adrFormat` | `canonical`, `madr-4` / `canonical` | resource | Validation/display format. |
| `adrGuard.validation.baseline` | string / `.adrguard-baseline.json` | resource | Safe relative diagnostic baseline. |
| `adrGuard.validation.onSave` | boolean / `false` | resource | Opt-in validation after applicable saves. |
| `adrGuard.validation.debounceMilliseconds` | number / `750` | resource | Save batching interval, `200`–`5000`. |
| `adrGuard.validation.baseReference` | string / `main` | resource | Local Git ref suggested for incremental validation and analysis. |
| `adrGuard.analysis.impactManifest` | string / `.adrguard-impact.json` | resource | Safe relative impact mapping JSON path. |
| `adrGuard.analysis.driftRules` | string / `.adrguard-fitness.json` | resource | Safe relative fitness rules JSON path. |
| `adrGuard.analysis.driftBaseline` | string / `""` | resource | Optional safe relative existing drift baseline. |
| `adrGuard.analysis.driftPolicy` | `advisory`, `enforce` / `advisory` | resource | Default drift policy; enforcement is never automatic. |
| `adrGuard.governance.catalog` | `standard`, `enriched` / `standard` | resource | Default explicit index/catalog mode. |
| `adrGuard.ai.provider` | provider enum / `""` | resource | Optional preference; empty requires selection. |
| `adrGuard.ai.model` | string / `""` | resource | Model identifier; no implicit model default. |
| `adrGuard.ai.endpoint` | string / `""` | machine | Optional compatible endpoint; never a credential. |

## Compatibility

| Surface | Requirement |
| --- | --- |
| VS Code | `1.101.0` or newer in the `1.x` line. |
| Extension 1.1.0 | Published in the Visual Studio Marketplace. Extension and CLI releases remain independent. |
| Core CLI integration | Compatible `adr-guard` v1.3.0 or newer for the documented baseline commands. |
| Visual create/edit | CLI 1.7.0 schema `1.0` form contracts. |
| Impact, drift, baseline, enriched catalog, draft, review/compare | A CLI build whose `<command> --help` exposes the requested flags; the extension checks capabilities instead of assuming matching versions. |
| MCP | Optional. Extension 1.1.0 uses the stable VS Code MCP provider API with separately installed MCP 1.0.0 or a compatible schema `1.0` build; the extension remains fully functional without it. |

## Troubleshooting

- **CLI not found:** run **Check Installation** and verify `adr-guard --version` in the same local or remote environment as the workspace. Set an absolute `adrGuard.cli.path` if needed.
- **Visual form unavailable:** install CLI 1.7.0 or a development build containing `new --form`, or choose **Use traditional creation**. The extension detects support from the command contract instead of trusting a version string.
- **Unsupported option / exit code 2:** the installed CLI is older than the requested MADR, changed-validation, or baseline contract. Install a compatible CLI or use full canonical validation.
- **Workspace disabled:** trust the workspace and use a local `file:` folder. Virtual workspaces and pure browser hosts are intentionally unsupported.
- **Timeout/output limit:** raise the bounded machine setting only when the repository legitimately requires it.
- **No exact diagnostic line:** the installed CLI did not provide an evidenced location for this finding; the extension opens the file at `(0, 0)`.
- **Remote CLI mismatch:** install/configure the CLI on the remote extension host.
- **Visual capability unavailable:** run the relevant CLI command with `--help` on the extension host and use a build that exposes the required contract.
- **MCP unavailable:** confirm explicit enablement, workspace trust, the host-local absolute path, exact server filename, and `dotnet` availability for a DLL. A valid path means available to start, not connected.

For reproducible bugs and support, use the [GitHub issue tracker](https://github.com/rodri-oliveira-dev/adr-guard/issues). Security reports follow the repository's [security policy](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/SECURITY.md), and contributions follow the [repository guidance](https://github.com/rodri-oliveira-dev/adr-guard). The extension is licensed under the [MIT License](LICENSE). Portuguese documentation is available in [README.pt-BR.md](README.pt-BR.md).
