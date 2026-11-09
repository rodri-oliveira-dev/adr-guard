# ADR Guard for VS Code 1.2.0 — release candidate

[Português (Brasil)](vscode-v1.2.0.pt-BR.md)

ADR Guard for VS Code `1.2.0` is prepared but **not published**. Marketplace `1.1.1` remains the current public version, verified through the Marketplace Gallery API on 2026-11-06.

## Highlights

- Adds a read-only visual graph for declared ADR relationships, with node selection, essential decision details, and secure opening of the original Markdown file.
- Adds search, status and relationship-type filters, plus direct-link focus for one ADR and its incoming and outgoing edges.
- Distinguishes dependency and supersession direction and highlights existing cycle, broken-reference, and ambiguous-reference diagnostics without inferring any relationship.
- Adds explicit refresh, loading/empty/error/incompatible states, keyboard navigation, roving focus, visible focus indicators, accessible labels, and English/pt-BR localization.
- Accepts bounded reports of at most 5,000 ADRs and 10,000 relationships while rendering at most 100 matching nodes. Larger reports must be narrowed or focused before rendering.

## Compatibility and security

- Requires a separately installed CLI that exposes `adr-guard relationships` schema `1.0`; prepared CLI `1.12.0` provides that contract.
- Before loading a report, the extension probes `relationships --help`. Missing commands, unsupported schema versions, malformed output, and exceeded limits produce explicit fail-closed states rather than falling back to local parsing.
- Markdown navigation accepts only an exact path from the loaded report that resolves to an existing regular `.md` file physically inside the selected trusted `file:` workspace.
- The extension never creates, edits, approves, supersedes, or changes the lifecycle status of an ADR through the graph. The VSIX contains neither the CLI nor MCP.

## Packaging and publication

After merge, run **VS Code VSIX Package** on `main` to produce and validate `adr-guard-1.2.0.vsix`. The workflow installs from the lockfile, audits, lints, typechecks, builds, runs unit/localization/extension-host tests, packages the VSIX, installs it, and smoke-tests activation.

The workflow uploads a 21-day artifact and does **not** publish to the Visual Studio Marketplace. Marketplace publication requires a separate, explicitly authorized owner action and Marketplace authentication, followed by verification that `rodrioliveira.adr-guard` reports `1.2.0`.
