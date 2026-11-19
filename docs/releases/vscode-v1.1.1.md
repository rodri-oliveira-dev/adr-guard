# ADR Guard for VS Code 1.1.1 — published

[Português (Brasil)](vscode-v1.1.1.pt-BR.md)

ADR Guard for VS Code `1.1.1` is published on the [Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=rodrioliveira.adr-guard), verified on 2026-11-19. Its publication is independent from the CLI and MCP server.

This maintenance patch preserves all commands, settings, VS Code `1.101.0+`, schema `1.0` visual create/edit contracts, and the optional default-off MCP provider. It incorporates path-handling, bounded-message, maintainability, and regression-test improvements without adding a CLI or MCP binary to the VSIX.

The extension continues to invoke a separately installed compatible CLI. Published CLI `1.12.0` supplies visual create/edit and native MADR contracts as well as relationship export, which the extension does not expose directly. Capability checks and safe fallbacks remain active. MCP `1.0.1` is optional, independently installed, and separately released.

The **VS Code VSIX Package** workflow only builds and validates `adr-guard-1.1.1.vsix`; the already published Marketplace release required a separate, authorized maintainer step. A CI artifact alone is not proof of public availability.
