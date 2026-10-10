---
name: adr-guard-when-to-record
description: Decide whether a software architecture choice deserves an Architecture Decision Record, and recommend an appropriate documentation depth. Use when deciding between an ADR, an issue, a design note, an experiment, or no additional record.
license: MIT
compatibility: No CLI required for significance assessment; ADR Guard CLI is optional for subsequent authoring.
---

# Decide when to record an ADR

Determine **whether reasoning needs durable architectural history**. Do not create an ADR merely because a tool, package, or implementation choice was mentioned.

## Assessment workflow

1. Describe the **actual decision** in one sentence, without assuming the proposed solution is correct. Identify its scope and who owns the outcome.
2. Look for existing records, established standards and policies that already cover the choice. If a new choice supersedes an existing ADR, route the user to `adr-guard-supersede`.
3. Evaluate these significance signals using known evidence, not guesses:
   - **Structural impact:** boundaries, responsibilities, public contracts, shared dependencies or system of record.
   - **Reversibility:** migration, downtime, breaking clients or substantial rework to undo it.
   - **Reach:** multiple teams, services, security domains or stakeholders.
   - **Risk:** security, privacy, compliance, reliability, data integrity or recovery.
   - **Cost/ownership:** material operational, licensing, staffing or vendor commitments.
   - **Longevity:** future maintainers are likely to ask *why*.
   - **Trade-offs:** credible alternatives have materially different consequences.
4. Classify the outcome, with a brief evidence-based justification:
   - **ADR recommended** for a consequential decision that should remain discoverable.
   - **ADR optional** if scope is narrow but important architectural trade-offs remain.
   - **ADR not needed** for a reversible implementation detail, routine refactor, task update or meeting note.
   - **Decision not ready** if missing measurements, stakeholders or an experiment could reverse the conclusion.
5. For non-ADRs, suggest the **smallest adequate artifact**: pull-request rationale, issue, design note, experiment report or existing standard reference. Never require an ADR for every ticket.
6. If an ADR is appropriate, distinguish **category** from **format**. Recommend Minimal for a focused choice or Extended for high-impact decisions with alternatives, risks or cross-team consequences. Custom means a policy-approved canonical template. MADR 4.0 is a distinct, opt-in validation format; it is not a built-in `new` template.
7. Hand off record creation to `adr-guard-create` only after the user chooses to proceed.

## Boundaries

Do not invent security mandates, team approval authorities, costs or metrics. Do not infer acceptance from a PR merge, tests or a passing `adr-guard check`. A historically significant decision already implemented can be documented transparently, but should not be represented as an earlier approval.

## Output

Summarize the decision, significance signals (known/unknown), recommendation and rationale, next evidence needed, proposed owner, and appropriate documentation option. Never create files as a side effect of an assessment.

Product reference: [When to write an ADR](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/docs/fundamentals/when-to-write-an-adr.md) and [choosing a format](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/docs/decision-design/choosing-a-template.md).
