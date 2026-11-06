# ADR Guard for VS Code 1.1.1 — release candidate

[Português (Brasil)](vscode-v1.1.1.pt-BR.md)

ADR Guard for VS Code `1.1.1` is prepared but **not published**. Marketplace `1.1.0` remains the current public version, verified through the Marketplace Gallery API on 2026-11-05.

This candidate preserves all existing commands, settings, VS Code `1.101.0+`, schema `1.0` visual create/edit contracts, and the optional default-off MCP provider. It incorporates path-handling, bounded-message, maintainability, and regression-test improvements, and adds a read-only ADR relationship graph command without adding a CLI or MCP binary to the VSIX.

The graph consumes only the CLI relationship schema `1.0`, accepts bounded reports with up to 5,000 ADRs and 10,000 relationships, and renders at most 100 matching ADRs through a deterministic layered layout so larger reports can be filtered or focused first. It distinguishes dependency/supersession types and resolved/unresolved/ambiguous states, and renders untrusted contract text through safe DOM APIs under a restrictive local-resource CSP. It reports loading, empty, no-relationship, incompatible-CLI, operational-error, and render-limit states explicitly. This capability remains source-only until both the extension candidate and a CLI containing `relationships` are published.

The extension continues to invoke a separately installed compatible CLI. Published CLI `1.10.0` supplies the currently published command set; capability checks and safe fallbacks remain in place. The source branch adds the `relationships` JSON contract after the prepared CLI `1.11.0` baseline, so the graph shows an incompatible-CLI state with currently published packages. MCP `1.0.1` is optional, independently installed, and separately released.

After merge, run **VS Code VSIX Package** on `main` to produce and validate `adr-guard-1.1.1.vsix`. The workflow does not publish to the Marketplace. Marketplace publication requires a separate, explicitly authorized owner action with Marketplace authentication, followed by verification that `rodrioliveira.adr-guard` reports `1.1.1`.
