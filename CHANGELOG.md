# Changelog

Notable changes to **ADR Guard** (`RodriOliveira.AdrGuard`, the .NET CLI/Tool and its coordinated distribution) are tracked here by published version. The separately versioned VS Code extension has its own [changelog](extensions/vscode/CHANGELOG.md).

Version links below point to GitHub Releases. Dates and previously shipped highlights follow the published release history. Detailed current release notes are available in [English](docs/releases/v1.3.0.md) and [Portuguese](docs/releases/v1.3.0.pt-BR.md).

## [1.3.0](https://github.com/rodri-oliveira-dev/adr-guard/releases/tag/v1.3.0) — 2026-10-08

**Combined stable release:** v1.3.0 includes the full feature scope previously developed across the v1.3 and v1.4 branches. The [GitHub Release](https://github.com/rodri-oliveira-dev/adr-guard/releases/tag/v1.3.0) was published on 2026-10-08, with the `RodriOliveira.AdrGuard.1.3.0.nupkg` artifact attached.

### Added
- Explicit, opt-in MADR 4.0 validation; canonical format stays the default.
- Deterministic ADR relationship governance and diagnostics `ADR010`–`ADR014`, alongside existing diagnostics.
- Git-aware incremental validation of changed ADRs with explicit base refs and global integrity safeguards.
- Strict versioned diagnostic baselines, explicit creation/update, and new/existing/resolved reporting.
- Explicit AI-assisted comparative review of two selected ADR revisions with bounded source context.
- NuGet package branding icon and a separate VS Code extension icon; curated bilingual release notes.

### Changed
- Combined the capabilities originally planned for a separate v1.4.0 into NuGet **1.3.0**; no separate v1.4.0 feature publication is needed for that scope.
- Updated relevant user documentation and retained the existing manual, validated release process and security defaults.

**Details:** [v1.3.0 release notes](docs/releases/v1.3.0.md) · [Notas em português](docs/releases/v1.3.0.pt-BR.md) · [GitHub Release](https://github.com/rodri-oliveira-dev/adr-guard/releases/tag/v1.3.0) · [Release PR #116](https://github.com/rodri-oliveira-dev/adr-guard/pull/116) · [Full Changelog: v1.2.0...v1.3.0](https://github.com/rodri-oliveira-dev/adr-guard/compare/v1.2.0...v1.3.0)

## [1.2.0](https://github.com/rodri-oliveira-dev/adr-guard/releases/tag/v1.2.0) — 2026-10-08

- Added safe, idempotent `adr-guard init` with dry-run, explicit overwrite, and optional least-privilege workflow creation.
- Added optional centralized `.adrguard.yml` configuration with CLI > configuration > default precedence.
- Added deterministic versioned JSON and SARIF 2.1.0 `check` output.
- Hardened manual release versioning, distribution verification, and backward compatibility.

## [1.1.7](https://github.com/rodri-oliveira-dev/adr-guard/releases/tag/v1.1.7) — 2026-09-28

- Refreshed documentation to reflect published feature availability.

## [1.1.6](https://github.com/rodri-oliveira-dev/adr-guard/releases/tag/v1.1.6) — 2026-09-27

- Exposed opt-in ADR review through the reusable GitHub Action and added bilingual guidance.

## [1.1.5](https://github.com/rodri-oliveira-dev/adr-guard/releases/tag/v1.1.5) — 2026-09-27

- Updated pinned .NET SDK dependency.

## [1.1.4](https://github.com/rodri-oliveira-dev/adr-guard/releases/tag/v1.1.4) — 2026-09-27

- Hardened AI review handling of untrusted context and provider output.

## [1.1.3](https://github.com/rodri-oliveira-dev/adr-guard/releases/tag/v1.1.3) — 2026-09-27

- Added deterministic, optional technical review enforcement policies.

## [1.1.2](https://github.com/rodri-oliveira-dev/adr-guard/releases/tag/v1.1.2) — 2026-09-27

- Introduced read-only, advisory architectural review for existing ADRs.

## [1.1.1](https://github.com/rodri-oliveira-dev/adr-guard/releases/tag/v1.1.1) — 2026-09-24

- Synchronized CodeRabbit configuration.

## [1.1.0](https://github.com/rodri-oliveira-dev/adr-guard/releases/tag/v1.1.0) — 2026-09-22

- Added offline ADR creation with built-in Minimal/Extended templates and explicit custom templates.
- Added opt-in template selection for AI-assisted `draft`; retained existing defaults and human approval.

## [1.0.1](https://github.com/rodri-oliveira-dev/adr-guard/releases/tag/v1.0.1) — 2026-09-21

- Aligned GitHub Action documentation with the published `v1` compatibility tag.

## [1.0.0](https://github.com/rodri-oliveira-dev/adr-guard/releases/tag/v1.0.0) — 2026-09-21

- Released the reusable GitHub Action with `check`/`index`, validation annotations, hardened container execution, and versioned Action references.
- Coordinated Action, NuGet, and container publishing and verification.

## [0.1.12](https://github.com/rodri-oliveira-dev/adr-guard/releases/tag/v0.1.12) — 2026-09-21

- Updated pinned GitHub Actions dependencies.

## [0.1.11](https://github.com/rodri-oliveira-dev/adr-guard/releases/tag/v0.1.11) — 2026-09-18

- Updated pinned .NET SDK.

## [0.1.10](https://github.com/rodri-oliveira-dev/adr-guard/releases/tag/v0.1.10) — 2026-09-14

- Updated NuGet dependencies and pinned .NET SDK.

## [0.1.9](https://github.com/rodri-oliveira-dev/adr-guard/releases/tag/v0.1.9) — 2026-09-12

- Updated .NET SDK and container runtime dependencies.

## [0.1.8](https://github.com/rodri-oliveira-dev/adr-guard/releases/tag/v0.1.8) — 2026-09-08

- Updated pinned .NET SDK.

## [0.1.7](https://github.com/rodri-oliveira-dev/adr-guard/releases/tag/v0.1.7) — 2026-09-04

- Fixed remediable medium-severity container vulnerabilities.

## [0.1.6](https://github.com/rodri-oliveira-dev/adr-guard/releases/tag/v0.1.6) — 2026-09-04

- Hardened container base-image update practices.

## [0.1.5](https://github.com/rodri-oliveira-dev/adr-guard/releases/tag/v0.1.5) — 2026-09-04

- Added hardened multi-architecture container images.

## [0.1.4](https://github.com/rodri-oliveira-dev/adr-guard/releases/tag/v0.1.4) — 2026-09-03

- Hardened AI drafting security, context handling, document structure, and persistence.

## [0.1.3](https://github.com/rodri-oliveira-dev/adr-guard/releases/tag/v0.1.3) — 2026-09-03

- Introduced optional AI-assisted ADR drafting.

## [0.1.2](https://github.com/rodri-oliveira-dev/adr-guard/releases/tag/v0.1.2) — 2026-09-03

- Added ADR reference resources.

## [0.1.1](https://github.com/rodri-oliveira-dev/adr-guard/releases/tag/v0.1.1) — 2026-09-03

- Added project status badges.

## [0.1.0](https://github.com/rodri-oliveira-dev/adr-guard/releases/tag/v0.1.0) — 2026-09-03

- Introduced the ADR Guard .NET CLI and automated releases and dependency updates.
