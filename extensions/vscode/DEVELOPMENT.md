# ADR Guard for VS Code development

This extension is versioned independently from the .NET CLI. Version `0.1.0` targets VS Code `^1.100.0` and its Node workspace extension host. Development and tests use Node.js 22 or newer because the pinned official VS Code test tooling requires it; the runtime bundle targets Node.js 20-compatible JavaScript.

Use the lockfile from this directory:

```text
npm ci
npm run lint
npm run typecheck
npm run build
npm run test:unit
npm run test:integration
npm run package:vsix
npm run test:vsix
```

`esbuild.mjs` bundles the runtime into `dist/extension.js`. The host-provided `vscode` module remains external, and there are no extension runtime npm dependencies. The extension-host tests are pinned to VS Code 1.100.0 through `.vscode-test.mjs`.

The extension runs where the workspace lives. On desktop that is the local Node extension host; with WSL, Remote-SSH, or Dev Containers it is the corresponding remote Node extension host, whose filesystem and `PATH` are used. Pure web and virtual workspaces cannot run the CLI and are declared unsupported. CLI execution also requires Workspace Trust and a local `file:` folder.

Trusted extension-host configurations use unique temporary user-data directories. The official `@vscode/test-electron` launcher currently adds `--disable-workspace-trust` unconditionally, so `scripts/run-untrusted-tests.mjs` launches the same pinned VS Code and `@vscode/test-cli` runner directly without that flag, using isolated user-data and extension directories. This makes the Restricted Mode assertion real rather than reusing or disabling trust state. `scripts/verify-vsix.mjs` installs the generated VSIX into another isolated profile and activates that installed package in the pinned host.

`adrGuard.cli.path` has machine scope so repository settings cannot select a binary. When it is empty, discovery searches only absolute directories in the extension host `PATH`, rejects candidates resolving inside an open workspace, and never downloads a CLI. The process adapter uses argument arrays with `shell: false`, bounded output and time, cancellation, and disposal cleanup.

## MVP command and validation behavior

The `initializeRepository`, `createAdr`, `validateAdrs`, and `generateIndex` commands reuse the same discovery and process adapter. Initialization always runs `init --dry-run` first, shows its bounded preview, and requires modal confirmation; the extension never passes `--overwrite`. ADR creation trusts only the documented single-line `ADR written: <path>` result and opens the file only after its real path is verified inside the selected workspace and ADR directory. Index generation is explicit and warns before the CLI can update `README.md`.

Validation invokes `check <absolute-directory> --format json` and accepts structured reports only for schema version `1.0`. Exit 0 must agree with `valid: true`; exit 1 must agree with `valid: false`. Exit 2–4, malformed/empty JSON, count mismatches, unknown properties, symlink escapes, and diagnostics outside the selected ADR directory are operational errors. Problems entries retain the CLI code/message and use a zero-length range at document position `(0, 0)` because the v1 report has no line or column contract; this is a navigation fallback, not a claimed source location.

`adrGuard.validation.onSave` is opt-in and defaults to `false`. Eligible saves are local Markdown ADR files below `adrGuard.validation.directory` (default `docs/adr`); `README.md`, dirty documents, virtual/untrusted workspaces, and files outside the real configured directory are ignored. `adrGuard.validation.debounceMilliseconds` is clamped to 200–5000 ms. Save bursts are coalesced, obsolete processes are cancelled, stale results cannot publish diagnostics, and automatic failures are logged without repeated notifications. Diagnostics are cleared after a clean validation, file deletion, rename, workspace removal, relevant configuration change, or deactivation. Closing an editor does not remove persisted workspace diagnostics. Neither validation nor activation invokes `index`, `new`, a network service, or an AI provider.

## Explorer and v1.4 governance adapters

The **Architecture Decision Records** view is contributed to VS Code's existing Explorer container. It supports multi-root workspaces, recursively catalogs Markdown files below each folder's configured ADR directory, ignores `README.md`, orders workspace-relative paths deterministically, and uses native product/file icons. File watchers cover only the configured ADR globs; create/change/delete bursts are debounced, catalog generations prevent stale scans from being cached, and all watchers, timers, event emitters, and caches are disposed with the extension.

Explorer metadata is deliberately non-authoritative. A bounded 256 KiB prefix is read from regular, non-symlink Markdown files. The display extractor recognizes the first ATX H1, a numeric filename prefix, the first non-empty body line under an H2 `Status`, and MADR 4.0 top-level `status` front matter. It recognizes Markdown links only under H2 `References`, `Superseded by`, `Supersedes`, `Depends on`, or `Dependencies`; MADR's exact `superseded by ADR-NNNN` status is resolved only when that ID is unique. Code-fenced text is ignored. Unknown or ambiguous values stay unknown and never produce Problems diagnostics. Linked files are opened only after URI, real-path, workspace, ADR-directory, extension, existence, uniqueness, and symlink-boundary checks. Relationship validity and all `ADRxxx` rules remain exclusively owned by the CLI.

`adrGuard.validation.adrFormat` defaults to `canonical`. The extension omits `--adr-format` for that default to retain compatibility with older CLIs and passes the explicit `--adr-format madr-4` argument to `check` and `index` when selected. `--format json` remains the separate machine-output option. A CLI usage failure for an explicitly requested v1.4 flag is reported as an unsupported capability; the extension does not infer this from the package version.

`ADR Guard: Validate Changed ADRs` passes a validated, literal base ref as `check <dir> --changed --base-ref <ref> --format json`. The extension never invokes Git itself and never fetches. Git absence, an invalid/missing ref, shallow history, missing merge base, or another CLI operational failure stays a failure; the user may explicitly choose a subsequent full validation. Added, changed, renamed, deleted, and global-integrity behavior remains the CLI's responsibility.

`ADR Guard: Validate ADRs with Baseline` reads the existing workspace-relative JSON path in `adrGuard.validation.baseline` (default `.adrguard-baseline.json`) and passes `--baseline` without writing it. The version `1.0` JSON adapter preserves `valid`, validates baseline counts/states, publishes both new and existing current diagnostics to Problems with display labels, and reports new/existing/resolved counts. Existing diagnostics may coexist with `valid: true`; the extension never recomputes validity. There is no automatic baseline generation or update command in this phase.

Manifest strings use `package.nls.json` and `package.nls.pt-br.json`. Runtime strings use `vscode.l10n.t` with bundles in `l10n/`; tests enforce key and placeholder coverage.
