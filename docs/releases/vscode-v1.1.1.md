# ADR Guard for VS Code 1.1.1 — release candidate

[Português (Brasil)](vscode-v1.1.1.pt-BR.md)

ADR Guard for VS Code `1.1.1` is prepared but **not published**. Marketplace `1.1.0` remains the current public version, verified through the Marketplace Gallery API on 2026-11-05.

This maintenance patch preserves all commands, settings, VS Code `1.101.0+`, schema `1.0` visual create/edit contracts, and the optional default-off MCP provider. It incorporates path-handling, bounded-message, maintainability, and regression-test improvements without adding a CLI or MCP binary to the VSIX.

The extension continues to invoke a separately installed compatible CLI. CLI `1.10.0` is the prepared target for the complete documented command set; capability checks and safe fallbacks remain in place. MCP `1.0.1` is optional, independently installed, and separately released.

After merge, run **VS Code VSIX Package** on `main` to produce and validate `adr-guard-1.1.1.vsix`. The workflow does not publish to the Marketplace. Marketplace publication requires a separate, explicitly authorized owner action with Marketplace authentication, followed by verification that `rodrioliveira.adr-guard` reports `1.1.1`.
