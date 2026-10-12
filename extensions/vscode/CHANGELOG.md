# Changelog

All notable changes to ADR Guard for VS Code are documented here. Extension versions are independent from ADR Guard CLI versions.

## Unreleased

- Added **ADR Guard: Create ADR with Form** to the Command Palette and ADR Explorer, with multi-root workspace selection, native minimal/extended fields, document-language selection, and a side-effect-free Markdown source preview.
- Added strict schema `1.0` CLI adaptation over bounded stdin, duplicate-submit protection, safe created-path verification, and explicit fallback to the unchanged traditional flow for missing/incompatible CLIs or custom templates.
- Added English and Brazilian Portuguese UI, theme/high-contrast styling, keyboard-accessible labels and focus, restrictive CSP, nonce-protected local scripts, and text-only preview rendering.
- Requires an ADR Guard CLI containing the `new --form` contract (planned for CLI 1.7.0); the existing **Create ADR** command remains compatible with older CLIs.

This functionality is unreleased until a later extension release is published.

## 0.1.1 — 2026-10-08

First manual-publication candidate:

- Secure discovery and bounded execution of the ADR Guard CLI in trusted local workspace hosts.
- Native commands for installation checks, repository initialization, ADR creation, validation, and index generation.
- Problems diagnostics from the versioned CLI JSON contract and opt-in incremental save validation.
- Native ADR Explorer with canonical/MADR 4.0 display metadata and safe relationship navigation.
- Explicit changed-ADR and read-only baseline validation adapters for the CLI contracts consolidated into NuGet v1.3.0.
- Multi-root workspace handling, cancellation, stale-result protection, English/pt-BR localization, deterministic tests, isolated CI, and minimal VSIX packaging.
- Forced process-tree termination and a final settlement deadline when a CLI ignores graceful termination or descendants retain inherited output pipes.
- Workspace diagnostics now remain visible when an editor closes and are cleared only by lifecycle or validation events that invalidate them.

The extension has not yet been published to the Visual Studio Marketplace. Its Marketplace publisher ID is registered as `rodrioliveira`. Advanced MADR, relationship, changed-file, and baseline features require the compatible CLI published as NuGet v1.3.0. The CLI is already public; the extension remains unpublished in the Marketplace.

Manual publication remains a separately authorized owner action. No publication is triggered by ordinary CI, pull requests, pushes, or tags.

## 0.1.0 — 2026-10-08

Review-only MVP package; not published to the Visual Studio Marketplace.
