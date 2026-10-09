# When to write an ADR

[Português (Brasil)](when-to-write-an-adr.pt-BR.md) · [Documentation home](../index.md) · [Previous](why-use-adrs.md) · [Next](lifecycle.md)

Write an ADR when a choice will matter to people beyond the immediate implementation or will be expensive to rediscover. The goal is to capture architecturally significant reasoning, not every engineering action.

## A practical test

Consider an ADR when one or more answers are “yes”:

| Signal | Question |
| --- | --- |
| Impact | Does this change system structure, a quality attribute, a public interface, or a major dependency? |
| Reversibility | Would reversing the choice require migration, downtime, contract changes, or significant rework? |
| Reach | Will several teams, services, or stakeholder groups need to align? |
| Risk | Does the choice affect security, privacy, compliance, resilience, or data integrity? |
| Cost | Does it create material operational, licensing, staffing, or support obligations? |
| Longevity | Will future maintainers reasonably ask why this was chosen? |
| Contention | Are there viable alternatives with meaningfully different trade-offs? |

Examples include selecting a system of record, defining service boundaries, changing an authentication model, adopting an event schema, choosing a deployment topology, or setting a resilience strategy.

## When not to write one

An ADR is usually unnecessary for:

- a local refactoring with no externally meaningful trade-off;
- a reversible implementation detail covered by normal code review;
- a task status, meeting note, or step-by-step procedure;
- a decision already governed by an unchanged organization-wide standard, unless the local application or exception is itself significant;
- a hypothesis that has not reached a decision point—record the experiment first, then capture the choice and evidence.

Do not use ADRs to bypass the people authorized to decide, to justify a conclusion after implementation without disclosing that history, or to freeze an option permanently.

## Choose the depth after choosing to record

A small but significant decision may need only the [Minimal template](../decision-design/choosing-a-template.md). A cross-team or high-risk choice benefits from Extended or MADR detail. Decision category and document format are separate choices: a security ADR can be Minimal, while a data ADR can use MADR.
