# Optional templates for AI-assisted `draft`

The template-enabled `draft` workflow is implemented on `feature/issues-59` for the coordinated v1.1.0 release. The existing command with **no** `--template` or `--template-file` keeps its exact historical rendering and accepts its previous set of .NET culture names.

```bash
adr-guard draft ./docs/adr --title "Adopt Redis" --context "We need bounded caching." \
  --provider openai --model YOUR_MODEL --template minimal --culture en-US --preview

adr-guard draft ./docs/adr --title "Adopt Redis" --context "We need bounded caching." \
  --provider openai --model YOUR_MODEL --template extended --culture pt-BR

adr-guard draft ./docs/adr --title "Adopt Redis" --context "We need bounded caching." \
  --provider openai --model YOUR_MODEL --template-file ./templates/team-template.md
```

**Selection.** `--template minimal|extended` and `--template-file <path>` are optional, mutually exclusive and must not be repeated. Template guidance supports `en-US` and `pt-BR` only; omitting `--culture` defaults to `en-US`. A relative file path resolves from the working directory when the command starts, **not** from the ADR output directory. A missing, inaccessible, malformed, invalid-UTF-8 or oversized custom template is rejected before provider construction or invocation. File templates obey the 65,536-byte limit and strict placeholders documented in [custom-templates.md](custom-templates.md). Use `--preview` / `--dry-run` to inspect the proposed Markdown without writing anything.

## Exact data sent to the AI provider

Template selection changes **only local rendering after the provider returns**. ADR Guard does not add the template name, Markdown source, placeholders, localized template guidance, supplemental sections, template file path or any other custom template bytes to the provider request. The provider receives the existing `AdrGenerationRequest`: the trimmed ADR title, the existing composed architectural context, and the requested .NET culture. The architectural context consists of:

- The required explicit `--context` string (normalized and subject to existing size limits).
- Only files individually passed through `--context-file`, bounded and read using the established context-file loader; unselected files are never scanned or attached.
- Existing parsed ADR context **only** when `--include-existing-adrs` is explicitly set, with the existing deterministic 12,000-character bound. Without the flag, no existing ADR text is sent.

The provider's three returned fields are validated and inserted **once** into `{{context}}`, `{{decision}}` and `{{consequences}}` by the shared renderer. Selected template guidance and optional sections remain **local editorial scaffolding**, not instructions or context sent to the AI. If the template does not contain a given field placeholder, that field is not included in the saved Markdown. The command does not request the provider to fill additional template-specific sections. Secrets remain in their established environment-based provider configuration and are not written to logs by template selection.

## Structure, safety and concurrency

Rendered ADRs keep `Proposed`, exactly one H1, invariant required sections and the existing parser/validator contract. The template path never chooses the output filename or destination. Provider-generated H1 or H2 headings (other than headings inside fenced code blocks) are rejected for **template-enabled** drafts instead of being inserted as structural Markdown. The default non-template draft keeps its existing validation rules.

If another cooperating writer commits an ADR after the preview allocation, template-enabled persistence re-renders `{{id}}` with the **final** allocated ID under the shared creation mutex, validates that same content, then atomically writes it without overwriting another ADR. No provider call occurs inside the mutex. The returned saved Markdown matches the actual persisted bytes. Dry-run/preview uses a prospective ID and does not reserve anything; a later actual creation can receive a different ID.

Templates create editable **Proposed drafts**, not automatic architectural approval. Human review remains required. The GitHub Action `@v1` contract is unchanged and does not expose `new` or AI-assisted `draft`.
