# ADR Guard CLI and configuration reference

> Availability: `init`, `.adrguard.yml`, and structured `check` output were published in ADR Guard v1.2.0. MADR 4.0 format selection was published in v1.3.0. See [GitHub Releases](https://github.com/rodri-oliveira-dev/adr-guard/releases) for exact artifacts.

> Architecture impact analysis was introduced in v1.5.0, architectural drift in v1.6.0, and visual form contracts in v1.7.0. CLI 1.8.0 is published on [NuGet.org](https://www.nuget.org/packages/RodriOliveira.AdrGuard/1.8.0) and in its [GitHub Release](https://github.com/rodri-oliveira-dev/adr-guard/releases/tag/v1.8.0).

> The schema `1.0` visual-form contracts require CLI 1.7.0 or newer and a separately versioned compatible VS Code extension when used visually. CLI, extension, and MCP versions are independent.

## Command inventory

This table mirrors the command dispatcher and built-in `--help`. Specialized guides provide the detailed contracts and security limits.

| Command | Required inputs and principal options | Writes / external effects | Exit contract |
| --- | --- | --- | --- |
| `init [repository]` | Optional ADR directory, template, workflow generation, `--dry-run`, `--overwrite` | Writes configuration/template/workflow only when explicitly requested; dry run does not write | `0`, `2`, `3` |
| `check [directory]` | Text/JSON/SARIF, ADR format, lifecycle/supersession, filename/placeholder/metadata/profile policy, optional changed/base ref and diagnostic baseline | Read-only; local Git read for `--changed` | `0` valid, `1` findings, `2`, `3` |
| `baseline [directory] --output <file>` | Validation policy options; `--update` required to replace | Creates or explicitly replaces one versioned baseline | `0`, `2`, `3` |
| `index [directory]` | Optional output, validation policy, `--catalog enriched` | Writes deterministic standard index or enriched catalog only after validation | `0`, `1`, `2`, `3` |
| `new [directory]` | Human flow requires `--title`; structured flow requires `--form`; template/culture/filename options | Preview is read-only; create writes one `Proposed` ADR atomically | `0`, `1`, `2`, `3` |
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

Configured ADR directories apply when the positional directory is omitted from `check`, `index`, `new`, and `draft`. Configured templates apply only when `new`/`draft` do not receive an explicit template option. `review` continues to require an explicit target.

## Create an ADR through a structured client

```text
adr-guard new <directory> --form
  [--template minimal|extended | --template-file <path>]
  [--culture en-US|pt-BR] [--filename-policy <policy>]
```

`new --form` is a bounded JSON contract for trusted clients such as the VS Code extension, not an interactive terminal form. It reads one schema `1.0` request from stdin and writes one schema `1.0` response to stdout. The traditional `new --title` command remains the direct human-facing flow and supports both built-in and constrained custom templates.

The supported operations are:

- `describe`: returns the selected template name, compatibility, and ordered field IDs with their required state. The native `minimal` template exposes required `context`, `decision`, and `consequences` fields. `extended` adds optional `decisionDrivers`, `optionsConsidered`, `rationale`, `positiveConsequences`, `negativeConsequences`, `risks`, and `references`.
- `preview`: requires `title` and the described string-valued `fields`; it validates and returns the prospective `path` and exact `markdown` with `written: false`, without writing, updating the index, or reserving an ID.
- `create`: accepts the same content, reallocates the final ID under the creation lock, validates, and persists atomically. Its response returns the final `path` and `written: true`; it does not return the Markdown body.

Only the native `minimal` and `extended` canonical templates can be mapped to structured fields. When `--template-file` is selected, `describe` returns `compatible: false` with reason `custom-template-field-mapping-unavailable`; `preview` and `create` reject that combination. Use the traditional creation flow for custom templates. `--culture` accepts `en-US` or `pt-BR` guidance, and filename policies remain the same as for traditional `new`.

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

> Release candidate: `--diagnostic-locations` is included in the unpublished CLI 1.9.0 candidate, not in the currently published CLI 1.8.0 package. Use it only with a validated 1.9.0 local package or source build until that version is publicly verified.

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
