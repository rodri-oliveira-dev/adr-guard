# ADR formats and templates

[Português (Brasil)](formats-and-templates.pt-BR.md) · [Documentation home](../index.md) · [Previous](decision-categories.md) · [Next](choosing-a-template.md)

Three ideas are easy to mix up:

- A **decision category** is the subject, such as data or security.
- A **template** is an authoring starting point with sections and guidance.
- A **validation format** is the structural contract applied to a directory of ADR files.

## What ADR Guard supports

ADR Guard's default **canonical** validation requires a title and non-empty level-two `Status`, `Context`, `Decision`, and `Consequences` sections. Canonical statuses are `Proposed`, `Accepted`, `Deprecated`, and `Superseded`.

The `new` command can generate canonical records from:

- built-in `minimal` (the default);
- built-in `extended`;
- one local custom Markdown file selected with `--template-file`.

Extended is an ADR Guard canonical template with additional decision drivers, options, rationale, consequences, risks, and references. **It is not MADR.** Custom templates remain canonical and must preserve the required headings, initial `Proposed` status, and strict placeholder contract. Arbitrary Markdown is not accepted.

**MADR 4.0** is a separate, opt-in validation format selected with `--adr-format madr-4`. ADR Guard validates an explicit subset of the external structure. The CLI does not offer an equivalent `new --template madr-4` generator; author or obtain a MADR record separately, then validate it in MADR mode.

## Educational approaches versus product contracts

Y-Statements (“In the context of…, facing…, we decided…”) can help a team formulate a concise decision. Other ADR formats can also provide useful ideas. They are educational approaches, not ADR Guard validator formats unless their rendered file independently satisfies the selected canonical or MADR 4.0 contract.

Format selection applies to the entire validation set. Keep canonical and MADR records in separate directories and run separate commands; ADR Guard does not guess a format file by file.
