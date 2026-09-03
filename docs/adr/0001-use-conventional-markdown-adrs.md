# Use conventional Markdown ADRs

## Status

Accepted

## Context

ADR Guard needs a predictable structure to parse without turning Markdown into a configuration language. The files should stay readable in GitHub and useful even when the tool is not installed.

## Decision

Use a four-digit ADR ID in the filename followed by a lowercase kebab-case slug.

Each ADR uses one level-one heading for the title and level-two sections for Status, Context, Decision, and Consequences.

## Consequences

The default format is intentionally stricter than some ADR templates. That keeps validation deterministic and the parser small, but repositories using a different structure will need future configuration support before they can adopt ADR Guard unchanged.
