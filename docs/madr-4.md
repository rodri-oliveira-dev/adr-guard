# MADR 4.0 compatibility

> Availability: MADR 4.0 validation is published in [ADR Guard v1.3.0](https://github.com/rodri-oliveira-dev/adr-guard/releases/tag/v1.3.0) and remains opt-in.

> Release status: native MADR generation is prepared for CLI 1.11.0 but is not available in published packages or images until that release completes its publication workflow. CLI 1.10.0 remains the published baseline.

ADR Guard implements an explicit subset aligned with the official [MADR 4.0.0 templates](https://github.com/adr/madr/tree/4.0.0/template). It never guesses a format:

```bash
adr-guard check docs/decisions --adr-format madr-4
adr-guard index docs/decisions --adr-format madr-4
```

The equivalent configuration is `adr-format: madr-4`. An explicit CLI option takes precedence. Omitting both keeps the ADR Guard canonical format unchanged.

## Native generation

Generate a minimal or extended MADR draft explicitly, then validate the same
directory with the matching format:

```bash
adr-guard new docs/decisions --title "Adopt PostgreSQL" --adr-format madr-4
adr-guard new docs/decisions --title "Adopt a queue" --adr-format madr-4 --template extended --preview
adr-guard check docs/decisions --adr-format madr-4
```

The generated front matter contains only `status: "proposed"`; it does not
record approval or derive a date from the system clock. `minimal` emits the
three required sections. `extended` adds editable guidance for decision
drivers, consequences, confirmation, option analysis, and more information.
`--culture pt-BR` localizes guidance while structural headings remain invariant.

`--preview` and `--dry-run` return the prospective path and exact Markdown
without creating a file, index, temporary artifact, lock file, or ID
reservation. Repository `adr-format: madr-4` applies to `new`; an explicit
`--adr-format canonical` overrides it. Custom `--template-file` content remains
canonical-only and is rejected with MADR generation.

The schema `1.0` `new --form` envelope is unchanged. Format selection remains
outside JSON. To preserve existing clients, MADR reuses published IDs:
`context`, `optionsConsidered`, and `decision` map to the three required MADR
sections; extended also exposes `decisionDrivers`, `consequences`, and
`references` (`More Information`). Distinct Confirmation and option-analysis
fields require a future schema version rather than a silent `1.0` change.

## Required structure

- a level-one title;
- non-empty level-two `Context and Problem Statement`;
- non-empty level-two `Considered Options`;
- non-empty level-two `Decision Outcome`;
- the existing `NNNN-lowercase-kebab-case.md` filename contract.

MADR 4.0 front matter (`status`, `date`, `decision-makers`, `consulted`, `informed`) is optional. If `status` is present, it must not be empty. Optional `Decision Drivers`, `Consequences`, `Confirmation`, `Pros and Cons of the Options`, and `More Information` are accepted. ADR Guard does not require template guidance/placeholders, rewrite files, or migrate canonical ADRs.

Canonical and MADR records should not be mixed in one validation invocation because format selection applies to the validated set. Run separate commands for separate directories. MADR metadata is parsed as inert data; it is not a general YAML execution surface.

For a MADR supersession relationship, `status: "superseded by ADR-0123"` resolves the unique four-digit ID. Ordinary statuses remain MADR-defined prose; ADR Guard only interprets this explicit relationship form for graph integrity.
