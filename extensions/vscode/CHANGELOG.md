# Changelog

All notable changes to ADR Guard for VS Code are documented here. Extension versions are independent from ADR Guard CLI versions.

## 0.1.0 — Unreleased

Initial review-ready MVP:

- Secure discovery and bounded execution of the ADR Guard CLI in trusted local workspace hosts.
- Native commands for installation checks, repository initialization, ADR creation, validation, and index generation.
- Problems diagnostics from the versioned CLI JSON contract and opt-in incremental save validation.
- Native ADR Explorer with canonical/MADR 4.0 display metadata and safe relationship navigation.
- Explicit changed-ADR and read-only baseline validation adapters for the CLI contracts consolidated into NuGet v1.3.0.
- Multi-root workspace handling, cancellation, stale-result protection, English/pt-BR localization, deterministic tests, isolated CI, and minimal VSIX packaging.

The extension has not been published to the Visual Studio Marketplace. Its Marketplace publisher ID is registered as `rodrioliveira`. Advanced MADR, relationship, changed-file, and baseline features require the compatible CLI published as NuGet v1.3.0; the extension remains unpublished in the Marketplace.

Planned after review: validate the publisher-bearing VSIX and separately authorize Marketplace publication. Compatible CLI v1.3.0 is already published; no Marketplace publication is triggered by the CI workflow.
