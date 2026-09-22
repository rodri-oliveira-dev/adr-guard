# ADR Guard

[![CI](https://github.com/rodri-oliveira-dev/adr-guard/actions/workflows/ci.yml/badge.svg)](https://github.com/rodri-oliveira-dev/adr-guard/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/RodriOliveira.AdrGuard.svg)](https://www.nuget.org/packages/RodriOliveira.AdrGuard)
[![GitHub Release](https://img.shields.io/github/v/release/rodri-oliveira-dev/adr-guard)](https://github.com/rodri-oliveira-dev/adr-guard/releases)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)
[![License](https://img.shields.io/github/license/rodri-oliveira-dev/adr-guard)](LICENSE)

[Português (Brasil)](README.pt-BR.md)

**GitHub Action consumers:** see the [consumer guide](docs/github-action.md), [release policy](docs/github-action-release.md), [security model](docs/github-action-security.md), [external verification evidence](docs/github-action-external-verification.md), and [Marketplace publication checklist](docs/github-marketplace.md). Independent pre-release consumer verification has passed, the `@v1` compatibility tag is published, and the Marketplace listing is still **forthcoming**. Support is available through [SUPPORT.md](SUPPORT.md); security reports follow [SECURITY.md](SECURITY.md).

> **Version availability:** Offline `new` and optional template-based `draft` are introduced in version **1.1.0**; older 1.0.x packages do not include them. Before the 1.1.0 release is published, build `feature/issues-59` to try them; afterward install the versioned package or image. The public GitHub Action `@v1` continues to support only `check`/`index`.

ADR Guard is a lightweight .NET command-line tool for validating and indexing Architecture Decision Records (ADRs).

It is designed for repositories that want ADR conventions to be explicit, reviewable, and enforceable in local development and CI without introducing a heavy runtime dependency.

## Features

- validates ADR filenames, titles, statuses, and required sections;
- detects duplicate ADR IDs;
- detects broken relative links between ADRs;
- enforces a valid `Superseded by` link for superseded decisions;
- generates a deterministic Markdown index;
- avoids rewriting an index that is already current;
- exposes stable validation codes (`ADR001` through `ADR009`);
- exposes predictable exit codes for CI/CD;
- creates human-editable `Proposed` ADRs offline using built-in or custom Markdown templates;
- supports human-reviewed AI-assisted `Proposed` ADR drafting through explicit providers and context;
- ships as a .NET Tool with no third-party runtime dependencies.

## Install

Releases are published to both NuGet.org and [GitHub Packages](https://github.com/rodri-oliveira-dev?tab=packages).

The simplest installation uses NuGet.org:

```bash
dotnet tool install --global RodriOliveira.AdrGuard
```

Update an existing installation with:

```bash
dotnet tool update --global RodriOliveira.AdrGuard
```

GitHub Packages is also available as a secondary registry. NuGet clients require GitHub authentication to consume packages from that source.

The installed command is:

```bash
adr-guard
```

## Container images

ADR Guard is also distributed as the same release version through GHCR and Docker Hub:

```text
ghcr.io/rodri-oliveira-dev/adr-guard
docker.io/rodrigodotnet/adr-guard
```

Images support `linux/amd64` and `linux/arm64` and publish exact SemVer, minor, major, and `latest` tags. For example:

```bash
docker run --rm \
  -v "$PWD:/workspace:ro" \
  ghcr.io/rodri-oliveira-dev/adr-guard:latest \
  check docs/adr
```

The image runs as a non-root user. Release images include OCI metadata, SBOM and provenance attestations, and the CI path is gated by Hadolint, smoke tests, Dependabot base-image updates, and a Trivy vulnerability scan.

See the [container image and supply-chain guide](docs/container.md) for writable mounts, AI-provider credentials, immutable digest pinning, tags, and verification details.

## GitHub Action

ADR Guard provides a composite GitHub Action that invokes the published GHCR image directly, so consuming repositories do not need to install the .NET SDK. The repository must be checked out first, and the action supports Linux runners with a working Docker daemon, such as `ubuntu-latest`.

> The moving `@v1` compatibility tag is published. Consumers can use `uses: rodri-oliveira-dev/adr-guard@v1`; the Marketplace listing is tracked separately.

For complete pull-request/main workflows, inputs, annotations, required-check configuration, troubleshooting, and release/Marketplace status, use the [GitHub Action consumer guide](docs/github-action.md).

The default operation validates `docs/adr`:

```yaml
name: ADR validation

on:
  pull_request:
  push:

permissions:
  contents: read

jobs:
  adr-guard:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v7
        with:
          persist-credentials: false

      - name: Validate ADRs
        uses: rodri-oliveira-dev/adr-guard@vX.Y.Z
        with:
          path: docs/adr
          command: check
```

Inputs are deliberately small and map directly to supported CLI behavior:

| Input | Default | Allowed values / policy |
| --- | --- | --- |
| `path` | `docs/adr` | Repository-relative ADR directory. Absolute paths, `..` traversal, missing directories, and paths resolving outside `GITHUB_WORKSPACE` are rejected. |
| `command` | `check` | `check` or `index`. |
| `version` | empty | Optional exact image version in `X.Y.Z` or `vX.Y.Z` form. When omitted, `@vX.Y.Z` selects the exact image tag and `@vX` selects the matching moving major image tag. SHA/branch pins require an explicit exact version. |

Version selection never falls back to `latest`. For `uses: rodri-oliveira-dev/adr-guard@v1.2.3`, the Action invokes `ghcr.io/rodri-oliveira-dev/adr-guard:1.2.3`. For `uses: rodri-oliveira-dev/adr-guard@v1`, it invokes the matching moving major image tag `:1`. Exact Action tags are immutable; major tags move only to newer successful releases in that major line. If the Action is pinned by commit SHA or a branch, specify the image explicitly:

```yaml
- name: Validate ADRs from a pinned action commit
  uses: rodri-oliveira-dev/adr-guard@<commit-sha>
  with:
    path: architecture/adr
    command: check
    version: 1.2.3
```

See the [GitHub Action release policy](docs/github-action-release.md) for exact versus major tag semantics, idempotency guarantees, release ordering, and the verification procedure.

The `check` command mounts the checked-out workspace read-only, so validation cannot mutate repository files. For `index`, the workspace remains read-only and only the selected ADR directory is over-mounted as writable because the CLI generates or refreshes `README.md` there:

```yaml
- name: Generate ADR index
  uses: rodri-oliveira-dev/adr-guard@vX.Y.Z
  with:
    path: docs/adr
    command: index

- name: Fail if the generated index was not committed
  run: git diff --exit-code -- docs/adr/README.md
```

All user inputs are passed as discrete process arguments rather than executable shell fragments. Paths are resolved against the checked-out workspace before Docker starts, including symlink resolution, and the CLI exit-code contract remains unchanged: `0` success, `1` validation failure, `2` usage/input error, and `3` operational error.

When a `check` or `index` run returns exit code `1`, the action converts recognized `ADR001`–`ADR009` CLI diagnostics into GitHub **file-level error annotations** for existing ADR files within the validated directory. The CLI does not provide reliable line numbers, so annotations deliberately do not include a line number. Diagnostic paths and messages are validated and escaped before emitting workflow commands; unexpected output or operational failures are not annotated as ADR rules.

The action writes a compact `GITHUB_STEP_SUMMARY` with the outcome, exit code, and (for validation failures with recognized output) total and per-rule diagnostic counts. **At most 50 file annotations** are emitted per run; all diagnostics remain available in the raw CLI log. Raw output is replayed with GitHub workflow-command processing temporarily suspended to prevent untrusted ADR content from injecting annotations or other workflow commands. Reporting errors never replace the original CLI exit code.

The Action itself does not require `GITHUB_TOKEN`, repository write permissions, provider API keys, or network access. It runs containers with a read-only root filesystem, all Linux capabilities dropped, `no-new-privileges`, and networking disabled. AI-assisted `draft` and provider credentials are intentionally outside the default Action contract. See the [GitHub Action security model](docs/github-action-security.md) for registry access, secret handling, non-root execution, and version/digest pinning guidance.

Windows, macOS, Linux runners without a working Docker daemon, and root execution for writable `index` are not supported.

The public `rodri-oliveira-dev/adr-guard@v1` Action offers **only `check` and `index`**. Run `new` or AI `draft` separately via the CLI/.NET Tool or a versioned container, not as Action inputs.

## ADR format

ADR Guard expects Markdown files named with a four-digit ID followed by a lowercase kebab-case slug:

```text
0001-use-postgresql.md
```

A minimal valid ADR looks like this:

```markdown
# Use PostgreSQL

## Status

Accepted

## Context

We need a relational database.

## Decision

Use PostgreSQL.

## Consequences

The service depends on PostgreSQL operational knowledge.
```

Supported statuses:

- `Proposed`
- `Accepted`
- `Deprecated`
- `Superseded`

The required sections are `Context`, `Decision`, and `Consequences`. A `Superseded` ADR must also contain a `Superseded by` section linking to an existing ADR.

## Create Proposed ADRs offline

The new `adr-guard new` command on this development branch creates an editable ADR **without AI, credentials, or network access**. The destination directory must exist. Minimal and `en-US` are the defaults; Extended and Custom are opt-in.

```bash
mkdir -p docs/adr
adr-guard new docs/adr --title "Adopt Redis"
adr-guard new docs/adr --title "Adopt Kafka" --template extended --culture pt-BR
adr-guard new docs/adr --title "Adopt Cache" --template-file docs/examples/templates/team.en-US.md
adr-guard new docs/adr --title "Preview only" --template minimal --preview
adr-guard check docs/adr
adr-guard index docs/adr
```

`--dry-run` is an alias for `--preview`; neither writes an ADR, updates the index, or reserves an ID. `new` never updates the index automatically: run `check` and then `index` explicitly. `--template minimal|extended` and `--template-file <path>` are mutually exclusive. `--culture en-US|pt-BR` localizes instructional text, **not** the invariant headings `Status`, `Context`, `Decision`, `Consequences` or initial `Proposed` status. Custom files must be UTF-8 `.md`, at most 65,536 bytes, selected individually and resolved relative to the invocation directory; store templates outside the ADR directory. Creating uses the next ID after the highest existing ID, locks cooperating same-host creators and writes atomically without overwrite; `{{id}}` is regenerated using the final allocated ID. Structural validation is **not architectural approval**; an architect must review and replace the editable notes.

**Validator-compliant samples:** [Minimal EN](docs/examples/generated/minimal/0001-adopt-redis.md), [Extended pt-BR](docs/examples/generated/extended/0001-adotar-redis.md), [Custom EN](docs/examples/generated/custom/0001-adopt-cache.md), [Custom pt-BR](docs/examples/generated/custom-pt-BR/0001-adotar-cache.md). Read the [full offline creation and exit-code guide](docs/creation.md) and [custom placeholder rules](docs/custom-templates.md). Native MADR/alternate-format validation is not supported.

## Validate ADRs

Validate a directory recursively:

```bash
adr-guard check docs/adr
```

When the directory is omitted, ADR Guard uses the current directory:

```bash
adr-guard check
```

A successful validation returns exit code `0`. Validation failures are printed with the file path, stable rule code, and message.

Example:

```text
docs/adr/0002-use-cache.md: ADR004 Status 'Approved' is invalid. Allowed values: Proposed, Accepted, Deprecated, Superseded.
Validation failed with 1 issue(s).
```

## Generate the ADR index

Validate the ADR set and generate `README.md` inside the ADR directory:

```bash
adr-guard index docs/adr
```

The generated file is deterministic:

```markdown
# Architecture Decision Records

| ADR | Decision | Status |
| --- | --- | --- |
| [0001](0001-use-postgresql.md) | Use PostgreSQL | Accepted |
| [0002](0002-adopt-opentelemetry.md) | Adopt OpenTelemetry | Proposed |
```

The index is written only after validation succeeds. If the existing file already matches the generated content, it is left untouched.

A custom output outside the ADR directory can be supplied with:

```bash
adr-guard index docs/adr --output adr-index.md
```

Inside the ADR directory, generated Markdown must be named `README.md`; otherwise it would become an ADR candidate on the next validation.

## AI-assisted ADR drafting

ADR Guard can ask a configured external AI provider to draft an ADR while keeping persistence, context selection, and architectural acceptance under human control. AI output is always treated as a **proposal**: ADR Guard forces the generated status to `Proposed`, validates the document structure, and never accepts an architectural decision on behalf of the team.

A minimal persisted draft uses only the inline architectural context supplied on the command line:

```bash
adr-guard draft docs/adr \
  --title "Adopt a message broker" \
  --context "We need asynchronous integration." \
  --provider openai \
  --model <openai-model>
```

The normal persistence workflow allocates the next ADR ID deterministically, creates a compliant filename, and validates the generated candidate with the normal ADR parser/validator. Persistence writes the complete candidate to a temporary file in the ADR directory, flushes it, and only then atomically promotes it to the final filename without overwrite. If cancellation, provider failure, validation failure, an I/O error, or a concurrent filename race occurs, ADR Guard does not leave a partial final ADR and cleans up its temporary file.

The production CLI propagates cancellation through the draft workflow. Pressing `Ctrl+C` requests graceful cancellation across context loading, provider HTTP calls, validation boundaries, and persistence.

### Optional templates for AI-assisted drafts

Without `--template` or `--template-file`, `draft` retains its original rendering, supported .NET cultures, provider contract and explicit context selection. With a selected template, only the final rendering changes locally: template source, guidance and path are **never sent to the AI provider**. Use `--template minimal|extended` or `--template-file docs/examples/templates/team.en-US.md` alongside the existing `--provider`, `--model`, `--title` and `--context`. Existing ADRs are shared only with explicit `--include-existing-adrs`; context files only with explicit `--context-file`. Unlike offline `new --preview`, `draft --preview` still calls the provider but skips persistence. See the [AI template/privacy guide](docs/draft-templates.md).

### Providers, models, and authentication

ADR Guard does not choose a model automatically. Both `--provider` and `--model` are required at runtime.

| Provider | CLI value | Authentication | Endpoint |
| --- | --- | --- | --- |
| OpenAI | `openai` | `OPENAI_API_KEY` | official endpoint; custom `--endpoint` rejected |
| Anthropic | `anthropic` | `ANTHROPIC_API_KEY` | official endpoint; custom `--endpoint` rejected |
| Gemini | `gemini` | `GEMINI_API_KEY` | official endpoint; custom `--endpoint` rejected |
| OpenAI-compatible | `openai-compatible` | `ADR_GUARD_OPENAI_COMPATIBLE_API_KEY` (optional) | `--endpoint <uri>` required |

Examples:

```bash
adr-guard draft docs/adr --title "Decision" --context "Context" \
  --provider anthropic --model <anthropic-model>

adr-guard draft docs/adr --title "Decision" --context "Context" \
  --provider gemini --model <gemini-model>

adr-guard draft docs/adr --title "Decision" --context "Context" \
  --provider openai-compatible --model <model> \
  --endpoint https://example.internal/v1
```

Authentication is read from environment variables rather than CLI arguments, which keeps credentials out of command history and ADR content. The CLI reports provider and model selection but does not print authentication values.

For `openai-compatible`, remote endpoints must use HTTPS even when no API key is configured, because the architectural context itself may be sensitive. Plain HTTP is allowed only for loopback endpoints such as `localhost`, `127.0.0.1`, or `::1`, which keeps local Ollama/LM Studio-style workflows available. If `ADR_GUARD_OPENAI_COMPATIBLE_API_KEY` is set, HTTPS is required even for loopback endpoints so the Bearer credential is never sent over plaintext transport. Official OpenAI requests explicitly set `store: false`.

### Language and inline context

`--context` supplies the architectural problem or constraints directly and remains required. Generated prose defaults to `en-US`; use a .NET globalization culture name such as `pt-BR` when another language is desired:

```bash
adr-guard draft docs/adr \
  --title "Adotar cache distribuído" \
  --context "Precisamos reduzir a latência de leitura." \
  --culture pt-BR \
  --provider openai \
  --model <openai-model>
```

Canonical ADR headings and the `Proposed` status remain unchanged regardless of the selected culture. Provider-generated prose is rejected if it attempts to introduce another level-one title or duplicate canonical level-two `Status`, `Context`, `Decision`, or `Consequences` sections. Headings inside fenced code blocks remain ordinary section content.

### Context size limits

ADR Guard bounds provider input deterministically before invoking the configured AI provider:

| Context source | Maximum |
| --- | ---: |
| Inline `--context` | 20,000 characters |
| Each `--context-file` | 50,000 characters |
| All explicit context files combined | 100,000 characters |
| Parsed existing ADR context | 12,000 characters |
| Final composed generation context | 120,000 characters |

Explicit files are read only up to the per-file limit plus one character so arbitrarily large files are not loaded fully just to detect overflow. Oversized inline, per-file, aggregate, or composed context is rejected with an actionable error before provider invocation. Explicit files are never silently truncated.

### Existing ADR context

Existing ADRs are **not** sent to an AI provider by default. Add `--include-existing-adrs` to opt in:

```bash
adr-guard draft docs/adr \
  --title "Adopt a message broker" \
  --context "We need asynchronous integration." \
  --include-existing-adrs \
  --provider openai \
  --model <openai-model>
```

ADR Guard builds this context from parsed ADR data rather than concatenating repository files. Each selected ADR contributes its ID, title, status, decision, and local Markdown relationships. Ordering is deterministic by numeric ID and then filename.

Existing ADR context is bounded to **12,000 characters**. Complete ADR representations are appended in deterministic order while they fit; when the next complete representation would exceed the limit, that ADR and all following ADRs are omitted. Individual ADR fields are not partially truncated by this strategy. The CLI explicitly warns when existing ADR content will be sent to the provider.

### Explicit context files

Use repeatable `--context-file <path>` options to add explicitly selected Markdown or text files:

```bash
adr-guard draft docs/adr \
  --title "Adopt a message broker" \
  --context "We need asynchronous integration." \
  --context-file ./architecture/constraints.md \
  --context-file ./notes/runtime.txt \
  --provider openai \
  --model <openai-model>
```

Only the exact `.md` and `.txt` paths supplied by the user are read. ADR Guard does not recursively scan the repository, source tree, sibling files, or parent directories. Multiple files are composed in the same order in which they appear on the command line.

Before generation, the CLI prints the resolved local paths being used. The provider request contains each selected file's name and content, not its resolved local filesystem path.

Context composition is deterministic:

1. inline `--context`;
2. explicit `--context-file` content in command-line order;
3. parsed existing ADR context when `--include-existing-adrs` is enabled.

### Preview without persistence

Use `--dry-run` or its alias `--preview` to exercise the normal generation and validation path without creating a file:

```bash
adr-guard draft docs/adr \
  --title "Adopt a message broker" \
  --context "We need asynchronous integration." \
  --provider openai \
  --model <openai-model> \
  --dry-run
```

Preview calculates the same deterministic candidate ID and filename, generates the ADR, forces `Proposed`, parses and validates it, then prints the candidate path and complete generated Markdown. It skips only the final write step: the ADR directory and generated index remain unchanged.

### Privacy, limitations, and human review

Any inline context, explicitly selected context-file content, and opted-in existing ADR context is sent to the configured external provider. Review selected material for credentials, personal data, confidential business information, and other sensitive content before generation. Provider-side storage, retention, training, and processing behavior is governed by the provider you configure.

AI-assisted drafting deliberately remains human-in-the-loop. Generated ADRs can be structurally valid while still containing incorrect assumptions, weak trade-offs, security problems, or fabricated details. **A human architect or responsible reviewer must review the architectural reasoning before changing an ADR from `Proposed` to another status.**

This workflow does not perform source-code scanning, repository-wide context ingestion, Git diff analysis, automatic detection that an ADR is required, automatic modification of existing ADR statuses, commits or pull requests, RAG/vector search/embeddings, provider fallback, or automatic model routing.

## Validation rules

| Code | Validation |
| --- | --- |
| `ADR001` | Filename must match `NNNN-lowercase-kebab-case.md` |
| `ADR002` | Level-one title is required |
| `ADR003` | Status is required |
| `ADR004` | Status must be supported |
| `ADR005` | Required section is missing or empty |
| `ADR006` | ADR ID is duplicated |
| `ADR007` | Relative ADR reference is broken |
| `ADR008` | Superseded ADR has no valid `Superseded by` link |
| `ADR009` | Canonical level-two ADR section is duplicated |

ADR IDs do not need to be contiguous. Gaps are allowed because ADRs may be archived, migrated, or removed without renumbering historical decisions.

## Exit codes

| Code | Meaning |
| ---: | --- |
| `0` | Success |
| `1` | ADR validation failed |
| `2` | Invalid command-line usage |
| `3` | Operational error |

This makes CI integration straightforward:

```yaml
- name: Validate ADRs
  run: adr-guard check docs/adr
```

## Build from source

Requirements:

- .NET SDK 10.0.400 or a compatible patch in the same feature band.

Build and test:

```bash
dotnet restore AdrGuard.slnx
dotnet build AdrGuard.slnx --configuration Release --no-restore
dotnet test AdrGuard.slnx --configuration Release --no-build
```

Create the tool package:

```bash
dotnet pack src/AdrGuard/AdrGuard.csproj --configuration Release --no-build --output artifacts/package
```

Install the locally built package:

```bash
dotnet tool install --tool-path ./.tools RodriOliveira.AdrGuard --version 1.1.0 --add-source ./artifacts/package
./.tools/adr-guard check docs/adr
```

## Architecture decisions

ADR Guard validates its own architecture decisions. See [docs/adr](docs/adr/README.md).

The repository CI builds and tests the solution, packages the .NET Tool, installs that package locally, runs the packaged `adr-guard` against `docs/adr`, regenerates the ADR index, and verifies that no documentation drift was introduced. The container path additionally lints the `Dockerfile`, builds and smoke-tests the image, and blocks fixable `HIGH` or `CRITICAL` vulnerabilities detected by Trivy.

For release highlights, see the [v1.1.0 release notes](docs/releases/v1.1.0.md) ([pt-BR](docs/releases/v1.1.0.pt-BR.md)).

## Additional resources

For more background on Architecture Decision Records, including documents, templates, and examples:

- [Architecture Decision Record reference repository](https://github.com/architecture-decision-record/architecture-decision-record/tree/main/locales/en)

## Releases

After a pull request is merged into `main`, the release workflow waits for the `CI` workflow for that `main` commit to complete successfully. It then:

1. resolves a stable SemVer version, starting from `VersionPrefix` and incrementing the patch for subsequent releases;
2. packs `RodriOliveira.AdrGuard` with that version;
3. publishes the package to NuGet.org through Trusted Publishing (OIDC) and to GitHub Packages;
4. publishes the multi-platform OCI image to GHCR and Docker Hub with exact/minor/major/`latest` tags, OCI metadata, SBOM, and provenance attestations;
5. verifies the published architectures, attestations, exact image digest, and moving major image digest;
6. smoke-tests Action runtime resolution for the exact `vMAJOR.MINOR.PATCH` and moving `vMAJOR` references;
7. creates or verifies the immutable exact Action tag and advances the moving major Action tag only after all runtime/package publication jobs succeed;
8. creates the GitHub Release and attaches the `.nupkg` without overwriting an existing immutable asset.

A failed container publication cannot expose a new Action tag. Reruns preserve immutable SemVer tags and never move a major compatibility tag backwards. See the [GitHub Action release policy](docs/github-action-release.md) for the full verification and idempotency rules.

## License

Licensed under the [MIT License](LICENSE).
