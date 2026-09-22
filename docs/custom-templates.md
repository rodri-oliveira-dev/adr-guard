# Custom ADR template contract

[Português (Brasil)](custom-templates.pt-BR.md) · [Offline creation](creation.md) · [AI draft integration](draft-templates.md)

This document specifies the offline, data-only Markdown template format implemented in #54. The `adr-guard new --template-file` CLI and optional `draft --template-file` selection are implemented on `feature/issues-59` for the planned `v1.1.0` release. **The published v1.0.0 CLI and the public GitHub Action `@v1` do not provide these commands/options yet.** Build the development branch to try these examples before release. The Action remains limited to `check` and `index`.

## Template sources and selection

The built-in names are `minimal` (the default if no source is specified) and `extended`. A custom source uses one explicitly selected local `.md` file. `--template <name>` and `--template-file <path>` cannot be supplied together, even if the built-in name is `minimal`. Unknown built-in names and conflicting selections are CLI usage errors; missing, inaccessible, too-large or malformed files fail before any new ADR is persisted.

A relative template file path resolves against the invocation working directory, **not** the ADR output directory. Keep template files outside the selected ADR directory: `check` and creation validate Markdown ADRs in that directory, not template sources. The loader does not discover or recursively read other files.

Templates must be valid UTF-8 with at most **65,536 bytes**, including an optional UTF-8 BOM. The source is normalized to LF for deterministic output, and no network/provider access or code evaluation occurs.

## Exact Markdown layout

```markdown
# {{title}}

## Status

{{status}}

## Context

{{guidance-context}}

[EDIT: Describe the problem for ADR {{id}}.]

{{context}}

## Decision

{{guidance-decision}}

[EDIT: Describe the proposed approach.]

{{decision}}

## Consequences

{{guidance-consequences}}

[EDIT: Record the expected impact.]

{{consequences}}
```

The first nonblank line must be **exactly** `# {{title}}`. The first level-two section must be **exactly** `## Status` with a body of `{{status}}` or `Proposed`; the renderer always produces the invariant status `Proposed`. Define one nonempty `## Context`, `## Decision`, and `## Consequences` body. Optional additional literal level-two headings are allowed, but they cannot duplicate canonical headings or introduce another Status. Do not put H1/H2 headings inside section bodies; the renderer owns the final structure. The result must pass the existing ADR parser and validator without changes to validation policy.

A rendered template is an author-editable starting point, **not** an architect-approved decision. Replace instructional text and review the document before changing its `Proposed` status through the existing human workflow.

## Placeholder rules

Placeholders are case-sensitive, exact `{{name}}` tokens:

- `{{title}}`: safe, single-line Markdown-escaped ADR title. The H1 title is controlled by the renderer.
- `{{id}}`: the allocated numeric ADR ID as exactly four digits (such as `0042`); the caller must supply a valid ID from 1 to 9999.
- `{{status}}`: invariant `Proposed`; caller-provided status overrides are rejected.
- `{{context}}`, `{{decision}}`, `{{consequences}}`: optional single-pass author/provider substitutions. User text cannot insert H1/H2 headings outside fenced Markdown code; the existing validator checks the final ADR.
- `{{guidance-context}}`, `{{guidance-decision}}`, `{{guidance-consequences}}`: built-in instructional prose localized to `en-US` or `pt-BR`. These are reserved and cannot be overridden by input substitutions.

Unknown tokens, nested/unclosed delimiters and alternate interpolation syntax such as `{title}`, `${title}`, or `{% expression %}` are rejected. Placeholder values are inserted **once**, never recursively evaluated as templates or interpreted as shell/program code. Literal shell-looking text is just Markdown. Template content cannot supply an output path or alter the allocator's deterministic filename, output directory, no-overwrite policy or atomic persistence.

**Concurrency integration:** if another cooperating creator commits after preview, `new` and template-enabled `draft` re-render `{{id}}` using the final ID under the shared creation lock and validate the exact persisted Markdown. The lock coordinates cooperative writers on the same host; it is not a cross-host distributed lock.

## Fixtures

See `tests/AdrGuard.Tests/Fixtures/Templates/custom-template.en-US.md` and `custom-template.pt-BR.md` for valid examples. The custom-template tests also verify UTF-8, size, path resolution, structural validation, placeholder safety and the actual `check` command.
