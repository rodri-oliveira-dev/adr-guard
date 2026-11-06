# ADR Guard for VS Code 1.1.1

[Português (Brasil)](vscode-v1.1.1.pt-BR.md)

ADR Guard for VS Code `1.1.1` was published in the Visual Studio Marketplace on 2026-11-05. This maintenance patch predates and does not include the Phase 3 relationship graph prepared for `1.2.0`.

## Changes

- Improves executable and workspace path handling, bounded user messages, adapter maintainability, and regression coverage.
- Preserves all existing commands, settings, VS Code `1.101.0+`, schema `1.0` visual create/edit contracts, and the optional default-off MCP provider.
- Continues to invoke a separately installed compatible CLI; the VSIX contains neither the CLI nor MCP.

## Compatibility

- Published CLI `1.11.0` supports the command set available when this extension shipped.
- MCP `1.0.1` is optional, independently installed, and separately released.
- The relationship graph requires extension `1.2.0` plus a CLI exposing `relationships` schema `1.0`; it is not part of this immutable Marketplace version.
