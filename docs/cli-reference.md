# ADR Guard CLI and configuration reference

> Availability: `init`, `.adrguard.yml`, and structured `check` output were published in ADR Guard v1.2.0. MADR 4.0 format selection was published in v1.3.0. See [GitHub Releases](https://github.com/rodri-oliveira-dev/adr-guard/releases) for exact artifacts.

> Architecture impact analysis was introduced in v1.5.0, architectural drift in v1.6.0, and visual form contracts in v1.7.0. CLI 1.10.0 is published on [NuGet.org](https://www.nuget.org/packages/RodriOliveira.AdrGuard/1.10.0) and in its [GitHub Release](https://github.com/rodri-oliveira-dev/adr-guard/releases/tag/v1.10.0). Native MADR generation is prepared for CLI 1.11.0 but is not yet published.

> The schema `1.0` visual-form contracts require CLI 1.7.0 or newer and a separately versioned compatible VS Code extension when used visually. CLI, extension, and MCP versions are independent.

## Command inventory

This table mirrors the command dispatcher and built-in `--help`. Specialized guides provide the detailed contracts and security limits.

| Command | Required inputs and principal options | Writes / external effects | Exit contract |
| --- | --- | --- | --- |
| `init [repository]` | Optional ADR directory, template, workflow generation, `--dry-run`, `--overwrite` | Writes configuration/template/workflow only when explicitly requested; dry run does not write | `0`, `2`, `3` |
| `doctor [repository]` | Optional repository directory; no other options | Read-only local diagnostics; one bounded `git --version` probe; no network, provider, installation, or fix | `0` without errors, `2`, `3` |
| `check [directory]` | Text/JSON/SARIF, ADR format, lifecycle/supersession, filename/placeholder/metadata/profile policy, optional changed/base ref and diagnostic baseline | Read-only; local Git read for `--changed` | `0` valid, `1` findings, `2`, `3` |
| `baseline [directory] --output <file>` | Validation policy options; `--update` required to replace | Creates or explicitly replaces one versioned baseline | `0`, `2`, `3` |
| `index [directory]` | Optional output, validation policy, `--catalog enriched` | Writes deterministic standard index or enriched catalog only after validation | `0`, `1`, `2`, `3` |
| `new [directory]` | Human flow requires `--title`; structured flow requires `--form`; ADR format/template/culture/filename options | Preview is read-only; create writes one proposed canonical or MADR ADR atomically | `0`, `1`, `2`, `3` |
| `edit <adr-file> --form` | Schema `1.0` stdin request plus format/policy options and optional ADR directory | `load` is read-only; fingerprint-checked `save` atomically updates only the selected ADR | `0`, `1`, `2`, `3` |
| `draft [directory]` | Title, context, provider, model; optional endpoint/files/existing ADRs/template/culture/preview | Calls the selected provider; preview does not write, create writes one `Proposed` ADR | `0`, `1`, `2`, `3` |
| `review <adr-file>` | Provider/model; optional compare ref, context, deterministic policy, text/JSON/output | Provider call; ADR remains read-only; report file only with explicit `--output` | `0`, `1`, `2`, `3`, `4` |
| `impact [repository] --base-ref <ref> --map <file>` | Text or JSON | Read-only local Git analysis; never fetches | `0`, `2`, `3` |
| `drift [repository] --rules <file>` | Optional evidence, baseline, baseline write/update, format, advisory/enforce policy | Reads bounded evidence; baseline write only when explicit; never runs producers | `0`, `2`, `3`, `4` |

Global `-h`/`--help` prints help and `--version` prints the assembly version. Exit `2` is invalid usage, `3` is operational or inconclusive failure, and `4` is a deterministic policy failure; AI findings alone never produce approval or policy compliance.

## Initialize a repository

```text
adr-guard init [repository] [--adr-directory <path>]
  [--template minimal|extended | --template-file <path>]
  [--github-actions] [--dry-run] [--overwrite]
```

`repository` defaults to the invocation directory. Managed paths must be relative, stay inside that repository, and cannot traverse symbolic links/reparse points. `--dry-run` reports the exact planned files without writing. Repeating the same command does not rewrite unchanged files. Existing files cause an operational error unless `--overwrite` explicitly authorizes replacement.

`--github-actions` writes `.github/workflows/adr-guard.yml` with `contents: read`, no provider credentials, and no write permission.

## Diagnose a local environment

> Release status: `doctor` is published in CLI 1.10.0.

```text
adr-guard doctor [repository]
adr-guard doctor --help
```

`repository` defaults to the invocation directory. The command reports six checks in stable order: CLI identity, repository detection, strict `.adrguard.yml` configuration, the effective ADR directory, the current .NET runtime, and the optional local Git executable. Each line begins with `OK`, `WARNING`, or `ERROR`, includes a short diagnosis, and provides `Fix:` guidance when there is a safe manual action. A final summary counts each severity.

The command is local and read-only. It does not read ADR contents, inspect provider credentials, access the network or Git remotes, install tools, create files/directories, or apply fixes. Configuration uses the same strict 64 KiB schema 1 loader and contained-path protections as other commands. Git detection invokes only a bounded local `git --version` probe with no shell.

Warnings, including missing optional Git or absent configuration with legacy defaults, return `0`. Any `ERROR` returns operational exit `3`; invalid arguments return `2` on stderr. Paths in the report are limited to `.`, `.adrguard.yml`, and validated repository-relative paths; absolute paths and raw local process errors are not emitted.

## Configuration

ADR Guard looks for `.adrguard.yml` in the invocation directory. The v1 schema is deliberately a small set of inert top-level scalars:

```yaml
schema-version: 1
adr-directory: "docs/adr"
template: minimal
# template-file: "docs/templates/team.md" # exclusive with template
# adr-format: canonical                   # canonical or madr-4
# lifecycle-statuses: Rejected=rejected,Under Review=proposed
# conventional-supersession: true          # opt in to status-text relationships
# filename-policy: canonical               # canonical, unnumbered, adr-prefix, numeric:1..9
# placeholder-policy: warn                 # off (default), warn, or error
# metadata-policy: validate                # optional bounded metadata validation
# validation-profile: legacy              # legacy, advisory, standard, strict
```

Unknown/duplicate properties, unsupported schema versions, nested YAML, collections, tags, anchors, aliases, block scalars, invalid UTF-8, oversized files, unsafe paths, and simultaneous `template`/`template-file` are rejected. Relative paths resolve from the configuration directory. Configuration is never interpreted as a command and does not expand environment variables.

Precedence is:

1. Explicit CLI argument.
2. `.adrguard.yml` value.
3. The command's legacy default.

Configured ADR directories apply when the positional directory is omitted from `check`, `index`, `new`, and `draft`. Configured templates apply only when `new`/`draft` do not receive an explicit template option. Configured `adr-format` now applies to `new`; explicit `--adr-format` wins and omission preserves `canonical`. `review` continues to require an explicit target.

## Create an ADR through a structured client

```text
adr-guard new <directory> --form
  [--adr-format canonical|madr-4]
  [--template minimal|extended | --template-file <path>]
  [--culture en-US|pt-BR] [--filename-policy <policy>]
```

`new --form` is a bounded JSON contract for trusted clients such as the VS Code extension, not an interactive terminal form. It reads one schema `1.0` request from stdin and writes one schema `1.0` response to stdout. The traditional `new --title` command remains the direct human-facing flow and supports both built-in and constrained custom templates.

The supported operations are:

- `describe`: returns the selected template name, compatibility, and ordered field IDs with their required state. Canonical `minimal` exposes required `context`, `decision`, and `consequences`; canonical `extended` adds its existing optional fields. To preserve schema `1.0` clients, MADR reuses published IDs: required `context`, `optionsConsidered`, and `decision` map to `Context and Problem Statement`, `Considered Options`, and `Decision Outcome`; extended also exposes optional `decisionDrivers`, `consequences`, and `references` (`More Information`).
- `preview`: requires `title` and the described string-valued `fields`; it validates and returns the prospective `path` and exact `markdown` with `written: false`, without writing, updating the index, or reserving an ID.
- `create`: accepts the same content, reallocates the final ID under the creation lock, validates, and persists atomically. Its response returns the final `path` and `written: true`; it does not return the Markdown body.

The native `minimal` and `extended` canonical and MADR templates can be mapped to structured fields without changing the schema `1.0` envelope; format selection remains a CLI/configuration concern outside JSON. Clients must call `describe` for the selected format instead of assuming field IDs. When `--template-file` is selected, `describe` returns `compatible: false` with reason `custom-template-field-mapping-unavailable`; `preview` and `create` reject that combination. Custom templates remain canonical-only. `--culture` accepts `en-US` or `pt-BR` guidance, and filename policies remain the same as for traditional `new`.

The schema `1.0` MADR form intentionally omits distinct fields for `Confirmation` and `Pros and Cons of the Options`; traditional extended generation still includes editable guidance for those sections. Adding new field IDs that existing clients reject would require a new form schema version rather than a silent `1.0` change.

> Availability: native `new --adr-format madr-4` generation is prepared for CLI 1.11.0 but is not included in the published CLI 1.10.0.

Traditional creation uses the same format selection:

```text
adr-guard new <directory> --title <title>
  [--adr-format canonical|madr-4]
  [--template minimal|extended | --template-file <path>]
  [--culture en-US|pt-BR] [--filename-policy <policy>]
  [--dry-run|--preview]
```

`canonical` remains the default. MADR output starts with `status: "proposed"` and passes the MADR validator before persistence. `--template-file` cannot be combined with `madr-4`.

```json
{"schemaVersion":"1.0","operation":"describe"}
```

```json
{"schemaVersion":"1.0","operation":"preview","title":"Adopt a queue","fields":{"context":"Orders need asynchronous processing.","decision":"Use the managed queue.","consequences":"We must operate retries and dead-letter handling."}}
```

Responses contain `schemaVersion`, `operation`, `template` (`name`, `compatible`, optional `reason`, and ordered `fields`), `written`, and the operation-specific `path`/`markdown`. Unknown or duplicate JSON properties, unsupported field IDs, invalid or missing required values, unsupported schema versions, and excessive input size/depth fail closed. Contract/usage errors return `2`, ADR validation findings return `1`, and filesystem, cancellation, or other operational failures return `3`. See the [creation guide](creation.md) for renderer, template, preview, and concurrency behavior.

## Edit an ADR through a structured client

```text
adr-guard edit <adr-file> --form [--adr-directory <directory>]
  [--adr-format canonical|madr-4]
  [--lifecycle-statuses <mapping>] [--conventional-supersession]
  [--filename-policy <policy>] [--placeholder-policy off|warn|error]
  [--validate-metadata]
  [--validation-profile <profile>]
```

`edit --form` uses the same bounded schema `1.0` stdin/stdout boundary and is likewise not an interactive terminal editor. It supports both `canonical` and opt-in `madr-4`; `--adr-directory` supplies the validation set and must contain the target.

`load` accepts exactly `schemaVersion` and `operation`. It validates the current ADR and returns `format`, normalized `path`, a 64-character SHA-256 `fingerprint`, `title`, `written: false`, and an ordered `fields` array. Each field contains `id`, `label`, `kind`, `required`, and `value`; status fields also include their permitted `options`. Depending on the selected format, editable fields cover supported scalar front matter, canonical status, and parsed level-two sections.

```json
{"schemaVersion":"1.0","operation":"load"}
```

`save` accepts exactly `schemaVersion`, `operation`, `fingerprint`, `title`, and `fields`. `fields` is an object whose keys are the field IDs returned by `load`, with every loaded field present exactly once. Its successful acknowledgement is deliberately bounded: it returns only the schema version, operation, fingerprint for the current bytes, and `written`, which is `true` only if content changed.

```json
{"schemaVersion":"1.0","operation":"save","fingerprint":"<64-character SHA-256 from load>","title":"Adopt a durable queue","fields":{"section-0":"Proposed","section-1":"Updated context.","section-2":"Updated decision.","section-3":"Updated consequences."}}
```

```json
{"schemaVersion":"1.0","operation":"save","fingerprint":"<64-character SHA-256 for the current bytes>","written":true}
```

Field IDs are opaque per-load identifiers; clients must not infer or hard-code the example IDs. Saving patches only the changed title or field value spans. It preserves the filename and ADR identifier, UTF-8 BOM when present, line-ending style for replaced sections, nested content, unknown sections, comments, metadata, and all other untouched Markdown. It validates the updated target against the selected format and repository context before atomically replacing that file. No changes produce `written: false` and leave the file bytes intact.

`save` requires the fingerprint returned by `load`. If the file has changed externally, it returns operational error `3` and leaves those external bytes untouched; the client must reload before retrying. Unknown or duplicate JSON properties, the wrong property set, invalid fingerprints, missing/extra field IDs, unsafe field values, unsupported versions, and excessive input size/depth return `2`. ADR validation findings return `1`; invalid paths, non-Markdown/symbolic-link targets, invalid UTF-8, oversized files, I/O, and cancellation return `3`. The fingerprint check prevents ordinary stale-form writes, although a non-cooperating writer can still change the target in the unavoidable interval between the check and atomic rename.

## Check output

> Published availability: `--diagnostic-locations` is available in CLI 1.9.0 and remains opt-in. Older packages keep the previous file-level JSON/SARIF behavior.

```text
adr-guard check [directory] [--format text|json|sarif] [--diagnostic-locations]
```

`text` remains the default. JSON uses schema version `1.0`; SARIF uses version `2.1.0`. `--diagnostic-locations` opts machine reports into evidenced one-based line/column properties or SARIF regions; it omits locations that cannot be proven. Without the flag, the existing report shape is preserved for strict clients. Both formats are written to stdout on success and ADR validation failure, while operational diagnostics use stderr. Exit codes remain `0` for valid, `1` for ADR diagnostics, `2` for invalid usage/configuration, and `3` for operational failure.

## Architecture impact

```text
adr-guard impact [repository] --base-ref <ref> --map <file> [--format text|json]
```

`impact` is explicit, advisory, and read-only. `repository` defaults to the current directory; `--map` is repository-relative. Text is the default and JSON uses the independent report schema `1.0`. Affected/unmapped findings return `0`, invalid usage returns `2`, and Git/history/manifest/bounds failures return `3`. See the [complete guide](guides/architecture-impact-analysis.md).

## Architectural drift

```text
adr-guard drift [repository] --rules <file> [--evidence <file>]... [--baseline <file>] [--write-baseline <file> [--update-baseline]] [--format text|json] [--policy advisory|enforce]
```

`drift` verifies explicit ADR-backed rules without evaluating MSBuild or executing external tests. Advisory is the default. Conclusive analysis returns `0`; invalid usage returns `2`; unknown/inconclusive evidence or input failures return `3`; explicit enforcement returns `4` only for a new gate-enabled deterministic static violation. See the [complete guide](guides/architectural-drift.md).

## Troubleshooting

- **Configuration is ignored:** invoke ADR Guard from the directory containing `.adrguard.yml`, or pass the directory/template explicitly.
- **Refusing to overwrite:** inspect the existing file and rerun `init --overwrite` only when replacement is intended.
- **Unsafe path:** remove absolute paths, `..` escapes, or symbolic-link/reparse-point components from managed paths.
- **Machine report appears empty:** JSON/SARIF is on stdout; do not redirect stderr into the same file.
- **CI needs Code Scanning:** generate SARIF first, then use a separate upload step with `security-events: write`; the standard ADR Guard Action intentionally remains read-only.

## Migration

No migration is required. Repositories without `.adrguard.yml` retain the legacy defaults. Add configuration only when it helps centralize repeated directory, template, or format arguments.
