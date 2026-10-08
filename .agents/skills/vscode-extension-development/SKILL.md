---
name: vscode-extension-development
description: Develop and review the repository-local ADR Guard VS Code extension, including its manifest, workspace-host behavior, secure CLI adapter, structured contracts, tests, and VSIX contents. Use for extension issues #109-#112; keep ADR business rules in the .NET CLI.
metadata:
  version: "1.0"
---

# ADR Guard VS Code extension development

Work in `extensions/vscode` and inspect the current issue plus the actual .NET CLI contracts before changing behavior. Treat the CLI as the authority for parsing, validation, generation, policy, exit codes, and versioned JSON; TypeScript adapts commands and presents results, but does not reproduce those rules.

## Architecture and host

- Keep VS Code UI/commands, workspace selection, settings, process discovery/execution, and output-contract adapters separate.
- Run as a Node workspace extension (`extensionKind: ["workspace"]`) so local, WSL, Remote-SSH, and Dev Container execution occurs beside the workspace. Do not claim browser-only support.
- Activate only for declared commands or views. Activation must not scan files, locate/run the CLI, access credentials, or perform network calls.
- Accept CLI-backed work only for trusted, local `file:` workspace folders. Resolve real paths, enforce workspace boundaries, and reject virtual resources and symlink escapes.

## Process and configuration safety

- Prefer an explicit trusted user/machine executable setting, then search absolute directories on the workspace extension host `PATH`. Never install/download a binary or execute a workspace-repository-controlled candidate.
- Spawn the resolved executable with a discrete argument array and `shell: false`. Bound time and stdout/stderr, support cancellation, terminate children on disposal, and convert spawn/signal/exit outcomes into typed results.
- Do not log environment variables, credentials, full untrusted output, ADR content, or provider responses. Log bounded operational metadata and show actionable user messages.
- Preserve CLI exits: 0 success, 1 validation findings, 2 usage, 3 operational failure, 4 deterministic policy failure. Parse machine output only through an explicit schema/version adapter; malformed or missing JSON is never a clean result.

## Manifest, tests, and packaging

- Keep `engines.vscode`, `@types/vscode`, Node target, activation events, contributions, `extensionKind`, and `capabilities` consistent. Declare untrusted and virtual workspace support deliberately.
- Unit-test discovery, literal arguments, exit normalization, spawn failures, output bounds, timeout, cancellation, trust, URI/path boundaries, and deterministic contract fixtures. Add extension-host tests for native activation and VS Code integration using `@vscode/test-cli` with `@vscode/test-electron`.
- Bundle the Node entry with `vscode` externalized. Before packaging, run install-from-lockfile, lint, typecheck, build, unit and extension-host tests; inspect the VSIX allowlist so source maps, fixtures, credentials, .NET binaries, and development dependencies are excluded.
- Keep tests offline and deterministic. Do not require an installed ADR Guard, provider credentials, AI/network services, or mutable user files.

Use current official guidance when an API or packaging rule may have changed:

- [VS Code Extension API](https://code.visualstudio.com/api)
- [Extension host](https://code.visualstudio.com/api/advanced-topics/extension-host)
- [Workspace Trust](https://code.visualstudio.com/api/extension-guides/workspace-trust)
- [Virtual workspaces](https://code.visualstudio.com/api/extension-guides/virtual-workspaces)
- [Testing extensions](https://code.visualstudio.com/api/working-with-extensions/testing-extension)
- [Bundling extensions](https://code.visualstudio.com/api/working-with-extensions/bundling-extension)
- [Official extension samples](https://github.com/microsoft/vscode-extension-samples)
- [OpenAI skills guide](https://developers.openai.com/api/docs/guides/tools-skills)
