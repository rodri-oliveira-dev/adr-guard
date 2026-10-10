# Enriched governance catalog

`adr-guard index --catalog enriched` produces an opt-in Markdown catalog with decision date, owner, category, successor, and review date. The normal `index` output remains unchanged. Missing values are rendered as `unknown`; ADR Guard does not infer owners or query external systems. Rows are sorted deterministically and table content and links are escaped.

