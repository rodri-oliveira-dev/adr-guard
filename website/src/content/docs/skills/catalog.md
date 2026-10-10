---
title: Agent Skills catalog — all 11 workflows
description: Explore the six essential and five advanced ADR Guard Agent Skills, their triggers, scope, and human-approval boundaries.
sidebar:
  order: 3
---

This catalog contains **six essential (P0)** and **five advanced (P1)** Agent Skills. Use the smallest number of skills needed for a task. Each is portable and can be installed independently.

The executable skill definitions live in the [ADR Guard repository](https://github.com/rodri-oliveira-dev/adr-guard/tree/main/skills). Each entry is an agent workflow, **not a new ADR Guard CLI command**.

## P0 — Essential workflows

### adr-guard-init

**Use when:** introducing ADR documentation to an existing repository, choosing directories, formats and templates.

**Does:** inspect existing decisions and configuration, preview `adr-guard init ... --dry-run`, then initialize only with authorization. **Does not:** overwrite an existing governance policy or migrate ADRs silently.

[Read the skill](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/skills/adr-guard-init/SKILL.md) · [Initialization guide](../../product/cli/)

### adr-guard-create

**Use when:** a meaningful architectural choice is ready to be documented.

**Does:** gather constraints and alternatives, select canonical Minimal/Extended/Custom, preview `adr-guard new`, and generate a **Proposed** record with authorization. Optional `draft` with AI requires explicit provider/model and permission to transmit context. **Does not:** accept decisions or generate MADR with `new`.

[Read the skill](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/skills/adr-guard-create/SKILL.md) · [Creation guide](../../product/creation/)

### adr-guard-validate

**Use when:** checking ADR structure, links, relationships or failed CI diagnostics.

**Does:** run `adr-guard check` and explain the exact diagnostics; optionally update the index after a successful check. **Does not:** treat passing validation as technical approval.

[Read the skill](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/skills/adr-guard-validate/SKILL.md) · [Reports](../../product/reports/)

### adr-guard-technical-review

**Use when:** evaluating the justification, alternatives, quality attributes, risks, security and implementation feasibility of a proposed decision.

**Does:** provide evidence-backed advisory findings, or explicitly call `adr-guard review` with user-chosen provider/model and allowed context. **Does not:** approve architecture, certify compliance or silently send repository files to an AI provider.

[Read the skill](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/skills/adr-guard-technical-review/SKILL.md) · [AI review](../../product/ai-review/)

### adr-guard-lifecycle

**Use when:** maintaining human-authorized lifecycle transitions and decision history.

**Does:** verify `Proposed`, `Accepted`, `Deprecated`, `Superseded` and configured custom statuses, preserve historical context and validate declared links. **Does not:** infer stakeholder approval from a merge or passing tests.

[Read the skill](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/skills/adr-guard-lifecycle/SKILL.md) · [Lifecycle concepts](../../learn/lifecycle/)

### adr-guard-ci-setup

**Use when:** adding ADR validation to GitHub Actions.

**Does:** guide a least-privilege `command: check` workflow, stable required check names, explicit index verification and optional SARIF. **Does not:** turn untrusted fork PRs into privileged AI-provider workflows.

[Read the skill](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/skills/adr-guard-ci-setup/SKILL.md) · [Action guide](../../product/github-action/)

## P1 — Analysis and stewardship

### adr-guard-when-to-record

**Use when:** deciding whether the impact, reversibility, reach or risk of a choice justifies an ADR.

**Does:** evaluate significance and recommend an ADR, a lighter artifact or an experiment. **Does not:** create a document for every implementation ticket.

[Read the skill](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/skills/adr-guard-when-to-record/SKILL.md) · [Decision significance](../../learn/when-to-write-an-adr/)

### adr-guard-tradeoff-analysis

**Use when:** comparing technically viable options before proposing a decision.

**Does:** distinguish constraints, assumptions, evidence, alternatives and risk; it may prepare material for an Extended ADR. **Does not:** fabricate scores, benchmarks or an authoritative winner.

[Read the skill](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/skills/adr-guard-tradeoff-analysis/SKILL.md) · [Writing effective decisions](../../learn/writing-effective-adrs/)

### adr-guard-supersede

**Use when:** replacing an earlier accepted decision rather than merely updating editorial text.

**Does:** create a separately proposed successor, preserve the predecessor and use explicit `Supersedes` / `Superseded by` relationships after appropriate human approval. **Does not:** deactivate the predecessor while the successor remains unaccepted.

[Read the skill](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/skills/adr-guard-supersede/SKILL.md) · [Supersession example](../examples/)

### adr-guard-audit

**Use when:** examining an existing ADR collection for documentation quality and governance gaps.

**Does:** run read-only `check --format json`, inspect evidence, and prioritize corrective actions. **Does not:** claim to detect implemented architecture drift or change ADR statuses automatically.

[Read the skill](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/skills/adr-guard-audit/SKILL.md) · [Safety boundaries](../security-and-governance/)

### adr-guard-team-adoption

**Use when:** developing a sustainable team practice for proposing, reviewing, approving and maintaining ADRs.

**Does:** recommend decision thresholds, ownership, templates, pilot cadence and CI adoption. **Does not:** impose organizational policy without authorized human agreement.

[Read the skill](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/skills/adr-guard-team-adoption/SKILL.md) · [Team playbook](../../adoption/)

## Pick by outcome, not by keyword

- **Before writing:** `when-to-record` → `tradeoff-analysis` → `create`.
- **Improve an existing ADR:** `validate` for deterministic rules; `technical-review` for architectural advice.
- **Replace an accepted decision:** `supersede`, supported by `lifecycle`.
- **Operate the practice:** `audit`, `team-adoption`, `init` and `ci-setup`.

[Install a skill](../installation/) · [Read practical examples](../examples/) · [Understand governance limits](../security-and-governance/)
