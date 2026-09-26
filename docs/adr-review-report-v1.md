# ADR review report schema v1

ADR Guard's AI-assisted review report contract is versioned independently from provider response formats.

The machine-readable schema is [`schemas/adr-review-report-v1.schema.json`](schemas/adr-review-report-v1.schema.json). Reports emitted by this version use `schemaVersion: "1.0"` and stable camelCase field names.

## Output modes

`adr-guard review ...` defaults to a Markdown-compatible text report. Use `--format json` for one JSON report object on stdout. In JSON mode, pre-provider source disclosure is written to stderr so stdout remains machine-readable.

Report files are created only with explicit `--output <path>`. Text reports require `.md` or `.txt`; JSON reports require `.json`. Existing files are not replaced unless `--overwrite` is also supplied. The selected ADR and its `README.md` index cannot be used as report output targets.

## Semantics

`dimensions` always follows the documented eight-dimension order and preserves provider assessments, including observed evidence and not-applicable entries.

`findings` contains only follow-up items: potential risk, missing context, or a recommendation for human investigation. `followUpPriority` is workflow guidance only (`required` or `recommended`); it is not an architectural severity score and never means that an ADR is approved or rejected.

`outcome` is deterministic:

- `no-follow-up-findings`: no assessment requires follow-up;
- `follow-up-suggested`: one or more follow-ups exist without missing-context findings;
- `needs-context`: at least one finding explicitly reports insufficient context.

Evidence paths are privacy-safe filenames from the selected review scope. ADR Guard does not invent line numbers: `line` remains null unless verified line information is available. Unknown or unselected sources are rejected before a successful report is emitted.

The report always includes limitations and a provider-cost caveat. Provider token usage or charges may apply, but ADR Guard does not fabricate precise token counts or cost estimates.
