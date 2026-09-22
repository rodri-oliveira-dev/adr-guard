# Offline ADR creation and templates

[Português (Brasil)](creation.pt-BR.md) · [README](../README.md) · [Custom template contract](custom-templates.md) · [AI template integration](draft-templates.md)

> **Release status:** these commands are implemented on `feature/issues-59` as part of the planned `v1.1.0` release. The publicly published `v1.0.0` tool and GitHub Action `@v1` do not yet expose `new`. Build/install this branch to run the commands below before the final release. The published Action remains limited to `check` and `index`; `new` and `draft` belong to the CLI/.NET Tool or to a separately invoked container, not to Action inputs.

## Quick start — no AI, account, API key, or network required

Run from the repository root with the **branch-built** `adr-guard` command installed or on your `PATH`. The destination directory **must already exist**. The default template is `minimal` and the default guidance culture is `en-US`.

```bash
mkdir -p docs/adr
adr-guard new docs/adr --title "Adopt Redis"
adr-guard check docs/adr
adr-guard index docs/adr
```

The first command writes `docs/adr/NNNN-adopt-redis.md`, where `NNNN` is the next available ID after the **highest existing ID**, not the first gap. On an empty directory this is `0001-adopt-redis.md`. `new` does **not** update `docs/adr/README.md`; `check` validates the ADR set and `index` creates or refreshes its deterministic README only after validation. `index` does not overwrite ADR files. Re-running `new` with the same title after a successful creation allocates a new ID; concurrent creators of the same title sharing the same preview path instead produce an explicit conflict, never a silent overwrite.

## Choose Minimal, Extended, or Custom

```bash
adr-guard new docs/adr --title "Adopt Redis" --template minimal --culture en-US
adr-guard new docs/adr --title "Adotar Redis" --template extended --culture pt-BR
adr-guard new docs/adr --title "Adopt Cache" --template-file docs/examples/templates/team.en-US.md --culture en-US
adr-guard new docs/adr --title "Try Redis" --template extended --preview
adr-guard new docs/adr --title "Try Redis" --template extended --dry-run
```

Only `minimal` and `extended` are built-in names (case-sensitive). `minimal` creates the canonical `Context`, `Decision`, `Consequences` sections; `extended` also adds decision drivers, alternatives, rationale, consequences, risks, and references. `--culture` controls **instructional prose** only and accepts `en-US` or `pt-BR`. Even in Portuguese, the canonical Markdown section names **`Status`, `Context`, `Decision`, `Consequences`** and the generated status **`Proposed`** remain in English so the existing validator can read them.

A custom source is **one explicitly selected local UTF-8 `.md` file**, at most **65,536 bytes** (64 KiB including an optional UTF-8 BOM). Relative `--template-file` paths resolve from the directory where the command is launched, not from `docs/adr`. Put reusable templates **outside the ADR destination** so `check` does not interpret them as candidate ADR files. An explicit `--template` and `--template-file` are mutually exclusive, even when `--template minimal` is specified. Files are never discovered recursively. Missing, inaccessible, oversized, malformed, or non-UTF-8 files are rejected before an ADR is saved. See the [exact custom file grammar and placeholder rules](custom-templates.md) and a [ready-to-copy custom template](examples/templates/team.en-US.md).

Both `--preview` and `--dry-run` calculate the prospective path, validate, and print the **full candidate Markdown** without creating a file, index, temporary artifact, or ID reservation. A later write may receive a different ID if someone else creates an ADR in the meantime. The destination directory itself must already exist. The renderer always leaves editable guidance rather than pretending to have made a complete architectural decision.

## Validated example outputs

These examples are **actual validator-compliant starting drafts**, with instructional text deliberately left for the author to replace. Each example lives in a separate directory to avoid duplicate IDs:

- [Minimal, English: `0001-adopt-redis.md`](examples/generated/minimal/0001-adopt-redis.md)
- [Extended, Portuguese guidance: `0001-adotar-redis.md`](examples/generated/extended/0001-adotar-redis.md)
- [Custom, English: `0001-adopt-cache.md`](examples/generated/custom/0001-adopt-cache.md) from [`team.en-US.md`](examples/templates/team.en-US.md)
- [Custom, Portuguese: `0001-adotar-cache.md`](examples/generated/custom-pt-BR/0001-adotar-cache.md) from [`team.pt-BR.md`](examples/templates/team.pt-BR.md)

```bash
adr-guard check docs/examples/generated/minimal
adr-guard check docs/examples/generated/extended
adr-guard check docs/examples/generated/custom
adr-guard check docs/examples/generated/custom-pt-BR
```

The repository CI generates the three examples independently with the **installed packaged tool**, compares their content byte-for-byte and runs these validation commands. Do not place all three samples in the same ADR directory: they each use ID `0001` intentionally.

## Custom source grammar and placeholders

A template starts with `# {{title}}`, followed by a `## Status` section whose whole body is `{{status}}` or `Proposed`, then nonempty `## Context`, `## Decision`, and `## Consequences` sections. Optional additional literal level-two headings are permitted, but must not collide with required headings. The renderer owns the one H1 title, canonical section headings and `Proposed`. Templates do not contain executable code.

Recognized exact case-sensitive placeholders: `{{title}}` (escaped single-line title), `{{id}}` (four digits), `{{status}}` (always Proposed), `{{context}}`, `{{decision}}`, `{{consequences}}`, `{{guidance-context}}`, `{{guidance-decision}}`, and `{{guidance-consequences}}`. For offline `new`, AI content placeholders resolve to empty strings; localized guidance and editable notes supply nonempty sections. The three AI content values are filled only when a **separately invoked, explicitly configured** template-enabled `draft` returns them.

Unknown or malformed placeholder syntax, duplicate structural headings and attempted status overrides fail validation. Substitutions occur **once**, never as shell commands, interpreted expressions or recursive templates. The template does not control the output directory or filename.

## Concurrent writing, safety, and errors

The same creation path is shared between offline `new` and AI `draft`: cooperating writers on the **same host** serialize ID allocation under a named mutex; the selected template is re-rendered with the **final allocated ID** while holding that lock, validated, written to a temporary file and atomically promoted without overwriting a destination. Failure/cancellation cleans up temporary files; retry may reuse an uncommitted ID. External non-cooperating writers and different hosts sharing the same filesystem require additional coordination.

The public exit-code contract is `0` success, `1` ADR validation failure, `2` invalid CLI usage (e.g., conflicting template options, unknown template/culture, missing title), `3` operational error (e.g., missing ADR directory, absent/malformed template file, I/O, ID exhaustion or cancellation). Existing invalid ADRs cause code `1` and no new file. On a successful creation, the status is **Proposed**, never an automatic architectural approval; a reviewer must replace instructional text and check the reasoning and trade-offs before accepting a decision. ADR Guard validates **structure**, not technical merit or a native MADR/alternative format.

## Optional AI-assisted draft and distributions

The existing unselected `draft` behavior, .NET culture handling and provider contract remain intact. You may select `--template minimal|extended` **or** `--template-file` on `draft` when you intentionally use an AI provider. Template content, guidance and file paths remain **local** and are **not sent to the provider**: the provider receives the required `--context`, each explicitly selected `--context-file`, and parsed existing ADR context **only** with `--include-existing-adrs`. The usual context limits, provider authentication/endpoint requirements, and human review still apply; `draft --preview` **calls the provider** but does not persist an ADR. See [provider/privacy examples and constraints](draft-templates.md).

NuGet.org and GitHub Packages distribute the .NET Tool. Both GHCR (`ghcr.io/rodri-oliveira-dev/adr-guard`) and Docker Hub (`rodrigodotnet/adr-guard`) distribute the versioned CLI image: run offline `new` with a writable mount of the ADR directory and **no AI credentials**; run AI `draft` only with intentionally supplied credentials and context. See [container examples](container.md). The published GitHub Action `rodri-oliveira-dev/adr-guard@v1` remains `check`/`index` **only**; neither `new` nor `draft` is an Action command.
