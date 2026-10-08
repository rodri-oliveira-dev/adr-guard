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
```

`esbuild.mjs` bundles the runtime into `dist/extension.js`. The host-provided `vscode` module remains external, and there are no extension runtime npm dependencies. The extension-host tests are pinned to VS Code 1.100.0 through `.vscode-test.mjs`.

The extension runs where the workspace lives. On desktop that is the local Node extension host; with WSL, Remote-SSH, or Dev Containers it is the corresponding remote Node extension host, whose filesystem and `PATH` are used. Pure web and virtual workspaces cannot run the CLI and are declared unsupported. CLI execution also requires Workspace Trust and a local `file:` folder.

`adrGuard.cli.path` has machine scope so repository settings cannot select a binary. When it is empty, discovery searches only absolute directories in the extension host `PATH`, rejects candidates resolving inside an open workspace, and never downloads a CLI. The process adapter uses argument arrays with `shell: false`, bounded output and time, cancellation, and disposal cleanup.

## MVP command and validation behavior

The `initializeRepository`, `createAdr`, `validateAdrs`, and `generateIndex` commands reuse the same discovery and process adapter. Initialization always runs `init --dry-run` first, shows its bounded preview, and requires modal confirmation; the extension never passes `--overwrite`. ADR creation trusts only the documented single-line `ADR written: <path>` result and opens the file only after its real path is verified inside the selected workspace and ADR directory. Index generation is explicit and warns before the CLI can update `README.md`.

Validation invokes `check <absolute-directory> --format json` and accepts structured reports only for schema version `1.0`. Exit 0 must agree with `valid: true`; exit 1 must agree with `valid: false`. Exit 2–4, malformed/empty JSON, count mismatches, unknown properties, symlink escapes, and diagnostics outside the selected ADR directory are operational errors. Problems entries retain the CLI code/message and use a zero-length range at document position `(0, 0)` because the v1 report has no line or column contract; this is a navigation fallback, not a claimed source location.

`adrGuard.validation.onSave` is opt-in and defaults to `false`. Eligible saves are local Markdown ADR files below `adrGuard.validation.directory` (default `docs/adr`); `README.md`, dirty documents, virtual/untrusted workspaces, and files outside the real configured directory are ignored. `adrGuard.validation.debounceMilliseconds` is clamped to 200–5000 ms. Save bursts are coalesced, obsolete processes are cancelled, stale results cannot publish diagnostics, and automatic failures are logged without repeated notifications. Diagnostics are cleared after a clean validation and on close, delete, rename, workspace removal, or deactivation. Neither validation nor activation invokes `index`, `new`, a network service, or an AI provider.

Manifest strings use `package.nls.json` and `package.nls.pt-br.json`. Runtime strings use `vscode.l10n.t` with bundles in `l10n/`; tests enforce key and placeholder coverage.
