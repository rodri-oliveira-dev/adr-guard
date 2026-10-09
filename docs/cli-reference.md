# ADR Guard CLI and configuration reference

> Availability: `init`, `.adrguard.yml`, and structured `check` output were published in ADR Guard v1.2.0. MADR 4.0 format selection was published in v1.3.0. See [GitHub Releases](https://github.com/rodri-oliveira-dev/adr-guard/releases) for exact artifacts.

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
```

Unknown/duplicate properties, unsupported schema versions, nested YAML, collections, tags, anchors, aliases, block scalars, invalid UTF-8, oversized files, unsafe paths, and simultaneous `template`/`template-file` are rejected. Relative paths resolve from the configuration directory. Configuration is never interpreted as a command and does not expand environment variables.

Precedence is:

1. Explicit CLI argument.
2. `.adrguard.yml` value.
3. The command's legacy default.

Configured ADR directories apply when the positional directory is omitted from `check`, `index`, `new`, and `draft`. Configured templates apply only when `new`/`draft` do not receive an explicit template option. `review` continues to require an explicit target.

## Check output

```text
adr-guard check [directory] [--format text|json|sarif]
```

`text` remains the default. JSON uses schema version `1.0`; SARIF uses version `2.1.0`. Both are written to stdout on success and ADR validation failure, while operational diagnostics use stderr. Exit codes remain `0` for valid, `1` for ADR diagnostics, `2` for invalid usage/configuration, and `3` for operational failure.

## Troubleshooting

- **Configuration is ignored:** invoke ADR Guard from the directory containing `.adrguard.yml`, or pass the directory/template explicitly.
- **Refusing to overwrite:** inspect the existing file and rerun `init --overwrite` only when replacement is intended.
- **Unsafe path:** remove absolute paths, `..` escapes, or symbolic-link/reparse-point components from managed paths.
- **Machine report appears empty:** JSON/SARIF is on stdout; do not redirect stderr into the same file.
- **CI needs Code Scanning:** generate SARIF first, then use a separate upload step with `security-events: write`; the standard ADR Guard Action intentionally remains read-only.

## Migration

No migration is required. Repositories without `.adrguard.yml` retain the legacy defaults. Add configuration only when it helps centralize repeated directory, template, or format arguments.
