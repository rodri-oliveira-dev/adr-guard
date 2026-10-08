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
