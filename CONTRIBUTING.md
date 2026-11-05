# Contributing to ADR Guard

Thank you for considering a contribution to ADR Guard. Contributions of all sizes are welcome, including bug fixes, tests, documentation, examples, and improvements to the developer experience.

ADR Guard helps teams document and validate Architecture Decision Records (ADRs). It **does not make or approve architectural decisions**. Keep that distinction clear in code, documentation, and proposed features.

This guide is for external contributors. The repository's [AGENTS.md](AGENTS.md) contains the authoritative engineering conventions and architectural boundaries.

## Before you start

Choose the most appropriate channel:

- [GitHub Discussions](https://github.com/rodri-oliveira-dev/adr-guard/discussions): usage questions, early-stage ideas, and examples of how you use ADR Guard.
- [GitHub Issues](https://github.com/rodri-oliveira-dev/adr-guard/issues/new/choose): reproducible bugs, scoped feature requests, and documentation improvements.
- [SUPPORT.md](SUPPORT.md): help with installation, configuration, and integration.
- [SECURITY.md](SECURITY.md): **private** vulnerability reporting. Never disclose an unpatched security issue in a public issue, discussion, or pull request.

Search existing issues and discussions before opening a new one. For significant changes, describe the problem and proposed behavior in an issue or discussion before investing in an implementation.

## Prepare your environment

ADR Guard currently uses the .NET SDK pinned in [`global.json`](global.json) (10.0.403 at the time this guide was written) and targets .NET 10.

1. Fork the repository and clone your fork.
2. Create a focused branch from the current `main`.
3. Make only the changes needed for your contribution.
4. Build and test locally before opening a pull request.

From the repository root:

```bash
dotnet restore AdrGuard.slnx
dotnet build AdrGuard.slnx --configuration Release --no-restore
dotnet test --solution AdrGuard.slnx --configuration Release --no-build
```

Tests use **xUnit v3** with **Microsoft.Testing.Platform** in native .NET 10 mode. Use `--solution` or `--project`, not a positional solution path. If you do not have a matching Release build, omit `--no-build` or build first.

For narrower test runs and filtering, see [the repository's test guidance](.agents/skill-overlays/dotnet-test/run-tests.md). For component-specific commands, follow the relevant component README and existing CI workflows.

## Know where changes belong

| Area | Responsibility |
| --- | --- |
| `src/AdrGuard.Core` | Shared deterministic parsing, validation, configuration, impact, and drift rules |
| `src/AdrGuard` | CLI commands, presentation, and write workflows |
| `src/AdrGuard.Mcp` | Read-only MCP server and protocol/transport contracts |
| `extensions/vscode` | VS Code user interface and CLI integration |
| `action.yml`, `scripts/github-action.sh` | GitHub Action integration around the CLI |
| `tests` | Unit, integration, protocol, and contract regression tests |
| `docs`, `website` | Product documentation and documentation portal sources |
| `skills/adr-guard-*` | Consumer-facing Agent Skills |

Keep reusable parsing, validation, and policy behavior in **Core** rather than duplicating it in the VS Code extension, MCP transport, or GitHub Action. Core must not depend on the executables. Keep the CLI and MCP server independent of each other.

Treat public interfaces as versioned contracts, including CLI arguments and exit codes, JSON/SARIF reports, MCP tools, Action inputs/outputs, VS Code settings, configuration formats, and Agent Skills metadata. Call out compatibility implications explicitly.

## Engineering expectations

- Follow [`.editorconfig`](.editorconfig), `Directory.Build.props`, and [AGENTS.md](AGENTS.md). Avoid unrelated formatting changes.
- Preserve deterministic results: the same inputs and repository state must yield the same validation findings, ordering, and exit status.
- Fail safely for malformed or unavailable evidence. Do not interpret missing evidence as compliance.
- Treat workspace files, paths, Markdown, configuration, and provider output as untrusted input. Never log secrets or commit credentials, tokens, private ADRs, or unsanitized reports.
- Add focused regression tests for bug fixes and public contract changes. Tests should be deterministic, isolated, and runnable offline.
- Preserve existing behavior unless a breaking change is intentional, documented, tested, and appropriately versioned.
- AI-generated content and AI review are advisory. Only people can approve an architectural decision or change an ADR's accepted status.

## Documentation and architecture decisions

Documentation is part of the change, not a separate follow-up task.

- Update the relevant README, reference, examples, and portal documentation when public behavior changes.
- Keep English and Brazilian Portuguese (`pt-BR`) documentation equivalent where the project provides both languages. Translate intent and examples, not just headings.
- For portal changes, edit the source content and use the repository's documented `website` sync workflow; do not manually edit generated pages.
- Distinguish changes in the development branch from versions **actually published** to NuGet, registries, the VS Code Marketplace, or GitHub Releases.
- Add an ADR only for a durable architectural decision with meaningful alternatives and consequences. Do not add an ADR for routine implementation details.

See the [documentation portal](https://rodri-oliveira-dev.github.io/adr-guard/), [compatibility matrix](docs/compatibility.md), [English README](README.md), and [Brazilian Portuguese README](README.pt-BR.md).

## Submit a pull request

1. Rebase or update your branch against `main` when appropriate, without overwriting other contributors' work.
2. Keep the change focused and use descriptive, semantic commit messages (for example, `fix(cli): handle malformed ADR status`).
3. Open a pull request targeting `main`. Fill out the [pull request template](.github/PULL_REQUEST_TEMPLATE.md), link relevant issues (such as `Closes #123`), and explain the behavior change.
4. Include regression coverage and report **the exact commands you actually ran**, their outcomes, and any checks you could not run.
5. Document compatibility risks, migrations, security considerations, and applicable English/`pt-BR` updates.
6. Address review comments and wait for required checks and maintainer approval.

A pull request does not authorize publishing packages, changing release tags, or accepting architectural decisions. Maintainers coordinate releases independently.

## Review checklist

Before requesting review, confirm that the following have been considered:

- [ ] The problem and proposed behavior are clear; unrelated changes are excluded.
- [ ] The implementation respects Core/CLI/MCP/VS Code/Action responsibilities.
- [ ] Compatibility and security implications are documented or marked not applicable.
- [ ] Relevant automated tests were added or updated, and executed results are reported truthfully.
- [ ] Relevant documentation and translations are updated.
- [ ] The PR template is complete and any limitations or follow-up work are visible.

Thank you for helping make ADR Guard useful, predictable, and safe for other teams.
