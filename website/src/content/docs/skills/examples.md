---
title: Agent Skills examples and end-to-end workflows
description: Follow copyable prompts and verified ADR Guard commands for proposing, reviewing, superseding, auditing, and adopting architecture decisions.
sidebar:
  order: 4
---

These are **task examples**, not commands automatically executed by visiting this page. Use them in a supported agent after reading the relevant skill and obtaining any required permission to change files.

## Scenario 1 — Is a new ADR needed?

**Agent prompt:**

> Evaluate whether moving checkout calls from synchronous HTTP to asynchronous events needs an ADR. Consider system boundaries, consistency, reversibility, operational ownership and risk. If the choice is not yet ready, identify the evidence we should collect. Do not write files.

**Skills:** [`adr-guard-when-to-record`](../catalog/#adr-guard-when-to-record) and [`adr-guard-tradeoff-analysis`](../catalog/#adr-guard-tradeoff-analysis).

**Expected result:** a significance assessment, an options matrix, known constraints, missing benchmarks and stakeholders to consult. The agent must not assume messaging is inherently better.

If a record is warranted, proceed to [the canonical ADR authoring guide](../../product/creation/).

## Scenario 2 — Create and validate a proposed decision

**Agent prompt:**

> Help me create an Extended ADR about our cache strategy. First examine existing decisions, capture real alternatives and the operational downsides, then preview the generated file. Only write after I approve the proposed content. Never mark it Accepted.

**Skill:** [`adr-guard-create`](../catalog/#adr-guard-create).

**CLI workflow** (assuming `docs/adr` already exists):

```bash
adr-guard new docs/adr --title "Use managed Redis as a shared cache" --template extended --preview
# After human confirmation:
adr-guard new docs/adr --title "Use managed Redis as a shared cache" --template extended
# Edit the generated Proposed ADR with actual context, alternatives and consequences.
adr-guard check docs/adr
adr-guard index docs/adr
```

**Expected result:** a uniquely named `Proposed` ADR and an index created only after successful validation. The preview does **not** reserve an ID; inspect the actual generated path after the write.

Read the [Redis format examples](/adr-guard/examples/redis-cache/) if you need a model for trade-off depth.

## Scenario 3 — Review an ADR without granting approval

**Agent prompt:**

> Review ADR 0007 for rationale, viable alternatives, nonfunctional requirements, security, operations, migration and verification criteria. Cite the exact evidence and distinguish gaps from violations. Do not change status or transmit repository context to an AI provider without my consent.

**Skills:** [`adr-guard-validate`](../catalog/#adr-guard-validate) and [`adr-guard-technical-review`](../catalog/#adr-guard-technical-review).

Offline structural check:

```bash
adr-guard check docs/adr --format json
```

An explicitly authorized provider-backed review is separate:

```bash
adr-guard review docs/adr/0007-cache-strategy.md \
  --provider openai --model YOUR_CHOSEN_MODEL \
  --policy advisory --format json
```

The example filename must exist, and the model is chosen by the user. Review findings are advisory; a `0` exit code does **not** mean the decision was approved. See [AI review and privacy](../../product/ai-review/).

## Scenario 4 — Replace a previously accepted decision

**Agent prompt:**

> Our accepted ADR 0007 is no longer appropriate. Outline a new decision, draft an explicitly linked successor, and leave the earlier ADR Accepted until the responsible reviewers accept the replacement. Preserve the original rationale and never rewrite history.

**Skill:** [`adr-guard-supersede`](../catalog/#adr-guard-supersede).

A proposed successor can declare:

```md
## Supersedes
[ADR 0007](0007-old-approach.md)
```

**Only after human acceptance**, update the old record to `Superseded` with `## Superseded by` linking to the accepted successor. Use the **actual filenames**, run `adr-guard check docs/adr`, and then `adr-guard index docs/adr`. See the [complete supersession learning example](/adr-guard/examples/supersession/).

## Scenario 5 — Audit or adopt with your team

**Agent prompt:**

> Perform a read-only audit of our ADR directory. Separate deterministic validation errors from potentially outdated reasoning or missing ownership. Cite evidence and propose a prioritized remediation plan. Do not change files or claim that the implementation violates documented architecture without proof.

**Skill:** [`adr-guard-audit`](../catalog/#adr-guard-audit).

```bash
adr-guard check docs/adr --format json
```

Follow with [`adr-guard-team-adoption`](../catalog/#adr-guard-team-adoption) to propose explicit reviewers, decision authority, template choice, a small pilot and a policy for maintaining history.

## Next steps

[Install a skill](../installation/) · [Browse all 11](../catalog/) · [Read security and governance boundaries](../security-and-governance/)
