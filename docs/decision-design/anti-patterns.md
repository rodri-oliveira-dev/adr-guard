# ADR anti-patterns

[Português (Brasil)](anti-patterns.pt-BR.md) · [Documentation home](../index.md) · [Previous](writing-effective-adrs.md)

## Tool choice without a problem

“Adopt Redis” is not enough. Describe the latency, consistency, load, or operational problem first, then show why the tool fits it.

## No alternatives for a high-impact choice

A consequential record that never considers the status quo or a viable alternative hides the trade-off. Use Extended or MADR when explicit comparison improves the review.

## One-sided consequences

Benefits without costs read like advocacy. Include complexity, migration, operations, failure modes, lock-in, and skills alongside the expected gain.

## Premature acceptance

Marking a draft `Accepted` before affected people review it turns status into a claim without governance. `new` and AI-assisted `draft` intentionally produce `Proposed`; acceptance is a human action.

## Silent history rewriting

Editing an accepted ADR to describe a replacement erases what was true. Create a new ADR and supersede the old record with an explicit relationship.

## A template graveyard

Leaving `[EDIT]`, empty analysis, or generic boilerplate creates the appearance of documentation without knowledge. Delete prompts, write decision-specific content, and use a smaller template if sections add no value.

## One ADR for many independent decisions

A record covering storage, authentication, deployment, and observability becomes difficult to review and supersede. Split choices that can evolve independently and link them when context overlaps.

## Validation as approval

A clean `adr-guard check` proves that deterministic structural rules passed. It does not prove the choice is secure, feasible, economical, implemented, or accepted.

## ADRs as permanent law

An ADR records the best decision for a stated context. Monitor assumptions and replace the decision when the context changes; preserve the history instead of treating it as immutable policy.
