# ADR Guard for VS Code

ADR Guard for VS Code is a workspace extension that presents the existing ADR Guard CLI inside the editor. It provides native commands, Problems diagnostics, and an ADR Explorer while leaving parsing, validation, policy, generation, and Git-aware analysis in the .NET CLI.

> Status: extension version **0.1.1** is prepared as a validated VSIX for manual publication. It is not yet published in the Visual Studio Marketplace. The Marketplace publisher is registered as `rodrioliveira`. Advanced commands use the compatible CLI contracts published in ADR Guard CLI v1.3.0 (including the former v1.4 development scope). The CLI is already public; the VS Code Marketplace publication remains separate and pending.

## Features

- Check that the CLI is discoverable in the workspace extension host.
- Initialize an ADR repository through a dry-run preview and explicit confirmation.
- Create ADRs with the CLI's configured, minimal, extended, or workspace-local custom template.
- Validate ADRs and publish versioned JSON findings in the VS Code Problems panel.
- Opt in to debounced validation after saving an ADR.
- Generate an ADR index only after explicit confirmation.
- Browse canonical and MADR 4.0 files in the native Explorer, including safe display-only status and relationship navigation.
- Run Git-aware changed-file validation against an explicit local base ref, with no hidden fetch.
- Classify diagnostics against an existing read-only baseline.
- Use English UI by default and Brazilian Portuguese when VS Code uses `pt-BR`.

## Requirements and installation

- VS Code `1.100.0` or newer in the `1.x` line.
- A trusted local `file:` workspace.
- A compatible `adr-guard` executable installed in the environment where the **workspace extension host** runs.

Install the CLI as a .NET tool when the required CLI version is available:

```shell
dotnet tool install --global RodriOliveira.AdrGuard
adr-guard --version
```

For development capabilities that are not released yet, build or install the CLI from the corresponding repository branch and set `adrGuard.cli.path` to its trusted absolute executable path. Otherwise the extension searches absolute directories on the extension host `PATH`. It never downloads or installs the CLI.

To install the release artifact manually, open **Extensions: Install from VSIX...** and select `adr-guard-0.1.1.vsix`, or run:

```shell
code --install-extension adr-guard-0.1.1.vsix
```

## Commands

Open the Command Palette and choose the command under **ADR Guard**:

| Command | Behavior |
| --- | --- |
| Check Installation | Runs `adr-guard --version` using safe discovery. |
| Initialize Repository | Previews `init --dry-run`, then asks before writing. Existing files are not overwritten. |
| Create ADR | Runs `new` and opens the verified created file. |
| Validate ADRs | Runs a full JSON validation and updates Problems. |
| Validate Changed ADRs | Uses `--changed --base-ref`; it never performs `git fetch`. |
| Validate ADRs with Baseline | Reads the configured existing baseline and reports new/existing/resolved counts. |
| Generate ADR Index | Warns before the CLI can update the ADR `README.md`. |
| Select ADR Format | Selects `canonical` or `madr-4` for the workspace folder. |
| Refresh ADR Explorer | Invalidates the Explorer cache and reloads it. |
| Reveal Current ADR | Selects the active ADR in the Explorer. |

Multi-root workspaces are supported. Commands use the active file's folder when possible and otherwise ask which folder to use. Settings with resource scope can differ by workspace folder.

## ADR Explorer and formats

The **Architecture Decision Records** view appears in the built-in Explorer. It scans the configured ADR directory, ignores its generated `README.md`, and displays a bounded subset of title, status, and recognized Markdown relationships. Those fields are navigation metadata only; the CLI remains authoritative for canonical and MADR 4.0 structure and governance.

Relationship targets must resolve to regular Markdown files within the selected workspace and ADR directory. Ambiguous, missing, external, virtual, or symlink-escaping targets are not opened.

## Diagnostics, save validation, and baseline

`Validate ADRs` maps schema `1.0` CLI JSON findings to Problems. Because that schema does not define line and column positions, diagnostics navigate to `(0, 0)` without claiming an exact source location. Malformed JSON, incompatible schemas, contradictory exit codes, files outside the ADR directory, and CLI exits `2`–`4` are operational failures rather than clean results.

`adrGuard.validation.onSave` defaults to `false`. When enabled, only saved Markdown ADRs below the configured directory trigger validation; save bursts are debounced, older runs are cancelled, and stale results cannot replace newer diagnostics. Save validation never creates ADRs, generates an index, runs AI, fetches Git refs, or writes a baseline.

Baseline validation uses the existing `adrGuard.validation.baseline` JSON file read-only. Creating or updating a baseline remains an explicit CLI workflow outside this MVP.

## Workspace Trust, remote environments, and security

The extension declares untrusted and virtual workspaces unsupported. It does not execute a process until the workspace is trusted and the selected folder resolves to a local `file:` path. The configured executable setting has machine scope; PATH candidates controlled by an open workspace are rejected. CLI arguments are passed as a literal array with `shell: false`, and execution has cancellation, timeout, and independent stdout/stderr limits.

In WSL, Remote-SSH, and Dev Containers the workspace extension runs remotely. Install the CLI and configure its path **inside that remote environment**, not only on the local UI machine. Pure VS Code Web and virtual workspaces without a Node workspace extension host and local filesystem are not supported.

ADR Guard for VS Code does not collect telemetry, install software, access AI provider credentials, invoke AI automatically, modify ADRs on save, write baselines automatically, or run hidden network/Git fetch operations. Explicit `init`, `new`, and `index` commands can write files as described above.

## Settings

- `adrGuard.cli.path`: trusted absolute CLI path in user/machine settings.
- `adrGuard.cli.timeoutMilliseconds`: process timeout, 1–120 seconds.
- `adrGuard.cli.maxOutputBytes`: separate stdout/stderr capture limit.
- `adrGuard.validation.directory`: workspace-relative ADR directory.
- `adrGuard.validation.adrFormat`: `canonical` or `madr-4`.
- `adrGuard.validation.baseline`: workspace-relative baseline JSON path.
- `adrGuard.validation.onSave`: opt-in save validation.
- `adrGuard.validation.debounceMilliseconds`: save debounce interval.

## Troubleshooting

- **CLI not found:** run **Check Installation** and verify `adr-guard --version` in the same local or remote environment as the workspace. Set an absolute `adrGuard.cli.path` if needed.
- **Unsupported option / exit code 2:** the installed CLI is older than the requested MADR, changed-validation, or baseline contract. Install a compatible CLI or use full canonical validation.
- **Workspace disabled:** trust the workspace and use a local `file:` folder. Virtual workspaces and pure browser hosts are intentionally unsupported.
- **Timeout/output limit:** raise the bounded machine setting only when the repository legitimately requires it.
- **No exact diagnostic line:** JSON schema `1.0` supplies a file but no source range; open the diagnostic and inspect the named ADR rule.
- **Remote CLI mismatch:** install/configure the CLI on the remote extension host.

For reproducible bugs and support, use the [GitHub issue tracker](https://github.com/rodri-oliveira-dev/adr-guard/issues). Security reports follow the repository's [security policy](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/SECURITY.md), and contributions follow the [repository guidance](https://github.com/rodri-oliveira-dev/adr-guard). The extension is licensed under the [MIT License](LICENSE). Portuguese documentation is available in [README.pt-BR.md](README.pt-BR.md).
