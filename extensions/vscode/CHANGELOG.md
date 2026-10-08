# Changelog

All notable changes to ADR Guard for VS Code are documented here. Extension versions are independent from ADR Guard CLI versions.

## 0.1.0 — Unreleased

Initial review-ready MVP:

- Secure discovery and bounded execution of the ADR Guard CLI in trusted local workspace hosts.
- Native commands for installation checks, repository initialization, ADR creation, validation, and index generation.
- Problems diagnostics from the versioned CLI JSON contract and opt-in incremental save validation.
- Native ADR Explorer with canonical/MADR 4.0 display metadata and safe relationship navigation.
- Explicit changed-ADR and read-only baseline validation adapters for the v1.4 CLI contracts.
- Multi-root workspace handling, cancellation, stale-result protection, English/pt-BR localization, deterministic tests, isolated CI, and minimal VSIX packaging.

The extension has not been published to the Visual Studio Marketplace. Its publisher identifier remains to be confirmed by the repository owner. The advanced MADR, relationship, changed-file, and baseline CLI contracts are implemented on the unmerged v1.3/v1.4 branch chain and are not presented as capabilities of a published CLI release.

Planned after review: confirm a Marketplace publisher identity, retarget after the dependent CLI pull requests merge, and perform any separately authorized Marketplace publication. No publication is part of `0.1.0` preparation.
