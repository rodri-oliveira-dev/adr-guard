# MADR 4.0 compatibility

> Preparation status: this support is implemented on the chained v1.3 branch and is not published until the manual v1.3.0 release completes.

ADR Guard implements an explicit subset aligned with the official [MADR 4.0.0 templates](https://github.com/adr/madr/tree/4.0.0/template). It never guesses a format:

```bash
adr-guard check docs/decisions --adr-format madr-4
adr-guard index docs/decisions --adr-format madr-4
```

The equivalent configuration is `adr-format: madr-4`. An explicit CLI option takes precedence. Omitting both keeps the ADR Guard canonical format unchanged.

## Required structure

- a level-one title;
- non-empty level-two `Context and Problem Statement`;
- non-empty level-two `Considered Options`;
- non-empty level-two `Decision Outcome`;
- the existing `NNNN-lowercase-kebab-case.md` filename contract.

MADR 4.0 front matter (`status`, `date`, `decision-makers`, `consulted`, `informed`) is optional. If `status` is present, it must not be empty. Optional `Decision Drivers`, `Consequences`, `Confirmation`, `Pros and Cons of the Options`, and `More Information` are accepted. ADR Guard does not require template guidance/placeholders, rewrite files, or migrate canonical ADRs.

Canonical and MADR records should not be mixed in one validation invocation because format selection applies to the validated set. Run separate commands for separate directories. MADR metadata is parsed as inert data; it is not a general YAML execution surface.

For a MADR supersession relationship, `status: "superseded by ADR-0123"` resolves the unique four-digit ID. Ordinary statuses remain MADR-defined prose; ADR Guard only interprets this explicit relationship form for graph integrity.
