# ADR Guard contributor instructions

## Project overview

ADR Guard is a .NET tool for creating, validating, indexing, reviewing, impact-mapping, and deterministically checking Architecture Decision Records (ADRs). The product treats ADRs as human decisions: AI integrations may provide bounded evidence or advice, but must never imply architectural approval or compliance.

The repository has three principal product surfaces:

- `src/AdrGuard`: the authoritative .NET CLI and all domain behavior, including parsing, validation, generation, review contracts, impact analysis, baselines, and architectural-drift evidence.
- `extensions/vscode`: a workspace-host TypeScript adapter. It discovers and invokes the CLI, validates versioned output contracts, and presents results in VS Code. It must not reimplement CLI business rules.
- `website`: the Astro/Starlight documentation portal. It consumes the public English and pt-BR documentation and validates routes, links, SEO, HTML, spelling, and accessibility-related behavior.

The root composite GitHub Action is also an adapter around the released CLI/container contract. Keep decision logic in the .NET core; UI, Action, provider, filesystem, Git, and process code are boundary adapters.

## Repository structure

| Path | Responsibility |
| --- | --- |
| `src/AdrGuard/Cli` | Command parsing, exit codes, output selection, and orchestration. |
| `src/AdrGuard/{Parsing,Validation,Generation,Indexing,Baselines}` | Core ADR workflows and deterministic validation. |
| `src/AdrGuard/{Impact,Drift}` | Versioned mappings, evidence, findings, baselines, and architectural-drift analysis. |
| `src/AdrGuard/Review` | Optional provider adapters, bounded review context, policy, security boundary, and versioned reports. |
| `src/AdrGuard/Git` | Narrow Git process and change-inventory adapters. |
| `tests/AdrGuard.Tests` | xUnit v3 unit and integration tests, organized to mirror production areas. |
| `tests/AdrGuard.Tests/Fixtures` | Checked-in deterministic compatibility and contract fixtures. |
| `extensions/vscode` | VS Code extension source, contracts, tests, localization, and VSIX verification. |
| `docs` | Public source documentation, schemas, ADRs, examples, and release documentation. |
| `website` | Generated documentation portal and its validation tooling. |
| `.github/workflows` | CI, release, security, portal, extension, and distribution gates. |
| `.agents/skills` | Contributor-facing Codex workflows for developing this repository. |
| `.agents/skill-overlays` | Repository bindings for portable skills. |
| `skills` | Eleven consumer-facing skills distributed by ADR Guard; do not treat these as contributor tooling. |
| `scripts` | Release, Action, distribution, documentation, and skill validation utilities. |

## Architecture rules

- Preserve deterministic validation. The same repository state and explicit inputs must produce the same findings, fingerprints, ordering, exit status, and machine-readable output.
- Fail closed when evidence is missing, malformed, unsupported, outside its declared scope, or cannot be verified. Represent uncertainty as `unknown`/inconclusive rather than inferred compliance.
- Keep parsing, policy, validation, impact, and drift rules in the .NET core. The VS Code extension and GitHub Action invoke and adapt the CLI; they do not maintain parallel rule engines.
- Preserve separation between pure decisions and adapters for files, Git, processes, HTTP providers, console rendering, VS Code, and GitHub Actions.
- Treat JSON/SARIF schemas, CLI arguments and exit codes, Action inputs/outputs, configuration formats, report fields, extension settings/commands, and skill frontmatter as versioned public contracts.
- Maintain backward compatibility unless a breaking change is explicit, documented, tested, and versioned. Reject unknown contract versions and unexpected data where strict parsing is part of the security boundary.
- Keep outputs reviewable and traceable to concrete files, rules, evidence, provider metadata, hashes, and repository state. Do not invent evidence.
- ADR status and approval remain human-owned. Generated drafts and AI review are advisory until a human records and accepts the decision.
- Preserve the CLI exit-code contract: `0` success, `1` validation findings, `2` usage error, `3` operational failure, and `4` deterministic policy failure.

## Coding standards

- Use the SDK pinned by `global.json` (currently .NET SDK 10.0.402), target `net10.0`, and use C# 14 as configured in `Directory.Build.props`.
- Nullable reference types, implicit usings, deterministic builds, latest-recommended analysis, build-enforced code style, and warnings-as-errors are mandatory. Fix warnings; do not broadly suppress or downgrade them.
- Follow `.editorconfig`: UTF-8, LF, final newline, spaces, four-space C# indentation, two-space JSON/YAML indentation, file-scoped namespaces, and System usings first.
- Match the existing namespace and folder layout. Use PascalCase for types and public/internal members, camelCase for locals and parameters, and descriptive domain names rather than generic helpers.
- Default implementation types and records to `internal`; expose or change a public/serialized surface only when the product contract requires it. Preserve field names, ordering, nullability, enum/string values, and schema versions in public models.
- Validate inputs at the boundary and return the established exit/result type. Catch exceptions only where they can be translated into a stable, actionable error; never hide corrupted state or convert malformed evidence into success.
- Thread `CancellationToken` through asynchronous network, process, and potentially long-running I/O paths. Do not add cancellation parameters to short, purely in-memory code without an actual boundary.
- For external processes, use discrete argument lists with shell execution disabled, bounded output/time, cancellation, and explicit exit handling. Never log secrets, environments, full untrusted payloads, or provider responses.
- Parse JSON with explicit size/depth/shape/version constraints appropriate to the contract. When XML is required (for example project inspection), prohibit DTD/external entity resolution and bound traversal. Normalize and validate file paths before access.

## Testing

Tests use xUnit v3 and Microsoft.Testing.Platform selected in `global.json`; do not assume VSTest or xUnit v2 syntax. Before running or proposing a .NET test command, use the `run-tests` skill and its repository overlay.

Canonical commands from the repository root are:

```text
dotnet restore AdrGuard.slnx
dotnet build AdrGuard.slnx --configuration Release --no-restore
dotnet test --solution AdrGuard.slnx --configuration Release --no-build
```

Use native MTP `--solution`/`--project` syntax for new local commands. xUnit v3 filters use `--filter-class`, `--filter-method`, `--filter-trait`, or one `--filter-query`; do not substitute VSTest `--filter` expressions.

- Mirror the production area under `tests/AdrGuard.Tests`; use existing nearby naming and fixture patterns.
- Add a focused regression for every fixed defect and contract change. Assert observable behavior, exact diagnostics/contracts when stable, and failure semantics—not private implementation details.
- Prefer unit tests for pure parsing, policy, mapping, and rendering. Use integration tests for CLI boundaries, filesystem/Git behavior, provider transports with local fakes, packaged tools, extension hosts, and public distribution contracts.
- Keep tests deterministic, independent, order-insensitive, offline, and safe for parallel execution. Use owned temporary directories and deterministic clocks/data; never depend on user files, credentials, mutable global state, live providers, or arbitrary sleeps.
- Treat checked-in fixtures as public compatibility evidence. Update them deliberately and cover both accepted and rejected versions/shapes.
- Run the narrowest relevant test while iterating, then the repository baseline proportional to the changed surface. Never report a test as passing unless that exact command completed successfully.

## Security

- Treat ADRs, Markdown, configuration, JSON, XML/MSBuild files, Git output, archives, provider responses, and workspace paths as untrusted input.
- Resolve canonical paths, enforce the intended repository/workspace boundary, and reject traversal, rooted-path injection, virtual resources where local files are required, and symlink/reparse-point escapes.
- Never execute content discovered in the workspace as code or as a shell command. Do not download or run tools implicitly. Quote nothing into a shell; pass literal argument arrays.
- Apply explicit limits to file counts, file sizes, JSON/XML depth, context size, process duration, and stdout/stderr. Fail safely when a limit is exceeded.
- Keep credentials in environment/secret stores, never in arguments, files, logs, fixtures, generated reports, or committed settings. Provider integrations remain opt-in and isolated behind bounded adapters.
- GitHub Actions must use least-privilege permissions, pinned immutable action SHAs, non-persisted checkout credentials when practical, validated inputs, and fail-closed publication ordering. Do not weaken CodeQL, dependency, Hadolint, Trivy, actionlint, zizmor, or equivalent existing gates.
- AI output is untrusted advisory material. It cannot approve an ADR, waive policy, establish evidence, or turn an unknown result into compliance.

## Documentation

- Update README files and public docs when user-visible commands, behavior, configuration, setup, or guarantees change. Update schema/reference docs and examples with versioned contract changes.
- Maintain the established bilingual strategy: public English documentation has a corresponding pt-BR version or portal route where the surrounding section is bilingual. Keep meaning and examples aligned rather than translating only headings.
- Every public feature, behavior change, command, contract, configuration, or integration across the ADR Guard CLI, VS Code extension, MCP server, GitHub Action, containers, or Agent Skills must update the corresponding repository and portal documentation in English and Brazilian Portuguese in the same pull request. Documentation must match implemented behavior, keep both languages equivalent, explicitly distinguish development-only capabilities from officially published ones, and never claim release or distribution availability without verifying the actual publication channel.
- Update `CHANGELOG.md`, package release notes, and `docs/releases` for release-visible changes; do not edit release/tag metadata for unreleased internal-only work without explicit scope.
- In `website`, edit source documents and run `npm run sync:docs`; do not hand-edit generated `learn/`, generated `product/*.md`, or `reference/generated/` pages.
- Add an ADR only for a durable architectural decision with meaningful alternatives and consequences. Routine implementation choices and process notes belong in code, tests, AGENTS.md, or focused documentation.

Documentation is part of the pull request Definition of Done, not optional follow-up work. When applicable, the change must update the relevant README, technical reference, portal source and equivalent pt-BR page; review executable examples, compatibility requirements, internal links, changelog placement, and real publication status; regenerate synchronized portal content through the official script; and pass the repository's documentation, route, link, SEO, HTML, spelling, and build validations. Any required item that is missing remains an open pull request task.

## Git workflow

- Respect the current branch and user scope. Never switch to or modify `main` directly without an explicit request, and do not create a new branch when work is assigned to the current branch.
- Preserve all pre-existing commits and working-tree changes. Inspect before editing and stage only files owned by the task; never reset, clean, overwrite, or silently absorb another contributor's work.
- Keep commits small, semantic, and traceable to the issue's Definition of Ready and Definition of Done. Review the complete diff and `git diff --check` before committing.
- A request for commit/push is not permission to open another PR. Do not merge, rebase, publish packages/images/extensions, move tags, create releases, or modify release aliases unless explicitly requested.
- Do not claim build, test, lint, security, packaging, or deployment success without executing the reported command and observing a successful result.

## Agent skills

Load a skill when its description matches the task; the user request and this file remain authoritative. Imported revisions and licenses are recorded in `.agents/skills/PROVENANCE.md` and `.agents/skills/sources.json`.

| Skill | Route tasks here |
| --- | --- |
| `dotnet-issue-implementation` | Implement a well-defined .NET issue against explicit requirements and DoD. |
| `dotnet-bug-investigation` | Investigate an unknown .NET defect or regression from evidence to focused fix. |
| `dotnet-security-review` | Review .NET, filesystem/process/provider, workflow, or dependency security. |
| `run-tests` | Choose or execute .NET test commands and xUnit v3/MTP filters; always apply the overlay. |
| `csharp-refactoring` | Perform behavior-preserving C# rename, extraction, move, or consolidation. |
| `repository-governance-sdd` | Change AGENTS.md, contributor skills, ADR/process governance, or agent documentation. |
| `vscode-extension-development` | Change or review the ADR Guard VS Code extension and its CLI adapter/contracts. |

The eleven `skills/adr-guard-*` folders are product artifacts for consumers. Do not move, rename, or use them as contributor skills, and do not broaden their validator to enforce `.agents/skills` rules.
