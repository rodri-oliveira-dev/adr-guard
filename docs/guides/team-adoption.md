# Adopt ADRs as a team

[Português (Brasil)](team-adoption.pt-BR.md) · [Documentation home](../index.md) · [First ADR tutorial](getting-started.md)

Adopt ADRs as a decision practice, not a document quota. Begin with a small set of consequential choices, agree on ownership and review, and improve the process from experience.

## Establish a lightweight policy

Agree on:

- what significance signals trigger an ADR;
- where canonical or MADR records live and which format each directory uses;
- the default template and when additional detail is expected;
- who proposes, who must be consulted, and who can accept a decision;
- how `Deprecated` and `Superseded` records are maintained;
- when validation runs locally and in CI;
- how stakeholders without repository access participate.

Avoid a single universal approval flow if decision risk varies. A reversible service-local choice may need a service owner; a security boundary or cross-team contract may require broader review.

## Suggested pull-request workflow

1. Open a `Proposed` ADR before implementation makes the choice expensive to change.
2. Request review from affected owners and specialists.
3. Resolve material disagreement in the ADR by updating context, options, or consequences—not by deleting the dissenting trade-off.
4. Record the human outcome and change status according to team authority.
5. Validate the set and regenerate the index.
6. Link implementation work when helpful, then observe whether assumptions remain true.

ADR Guard can enforce deterministic structure locally, in the [GitHub Action](../github-action.md), or through the [VS Code extension](../../extensions/vscode/README.md). It can create offline proposals and provide opt-in AI drafting or review. None of these mechanisms decides on behalf of the team.

## Roll out progressively

- Start with new decisions; do not attempt to reconstruct every historical choice.
- Backfill only current decisions whose missing rationale causes real risk or repeated work.
- Review a few early ADRs together to calibrate useful depth.
- Track discoverability and stale statuses as qualitative signals. Avoid unsupported ROI claims or document-count targets.
- Periodically sample accepted ADRs: are assumptions still true, owners known, and replacement links accurate?

## Governance without bureaucracy

Make the default path fast: Minimal template, focused review, and clear ownership. Add Extended or MADR detail when risk and complexity justify it. Validation prevents structural drift; review evaluates technical merit; the team's authority accepts the decision. Keeping these responsibilities separate makes the practice easier to trust.
