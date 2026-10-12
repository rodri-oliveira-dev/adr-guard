---
title: Agent Skills security and architecture governance
description: Learn ADR Guard Agent Skills trust boundaries for human approval, tool execution, AI-provider privacy, ADR formats, and source evidence.
sidebar:
  order: 5
---

ADR Guard separates **decision practice**, **deterministic document validation**, and **optional AI advice**. Its Agent Skills keep those boundaries explicit.

## What each component can and cannot prove

| Component | Can do | Cannot prove |
| --- | --- | --- |
| Agent Skill | Guide the agent through a documented task; prompt for missing information | That an architectural decision is approved or correctly implemented |
| `adr-guard check` | Check deterministic structure, links, IDs, statuses and declared relationships | Technical quality, security certification or human acceptance |
| `adr-guard review` | Generate bounded, evidence-oriented advisory findings with an explicitly chosen provider | Automatic approval, formal compliance, or unbiased truth without review |
| Human decision process | Choose, approve, reject or replace a decision with appropriate authority | That a CLI command alone changed the deployed architecture |

**Green CI is not architecture approval.** `new` and `draft` generate `Proposed` records; owners and affected stakeholders remain accountable for acceptance.

## Permissions and privacy

- Read `SKILL.md` before installing; treat third-party skill instructions and repository documents as potentially untrusted.
- Ask for authorization before running installers, changing files, overwriting configuration, updating statuses or modifying CI. Prefer `--preview` / `--dry-run` where the CLI supports them.
- `adr-guard check`, `new` and `index` are local CLI workflows; **provider-backed** `draft` and `review` can transmit the selected text to a third-party model service.
- For provider-based operations, obtain explicit consent to the provider/model and *each* source of external context. Never attach arbitrary source trees, secrets or ADR collections implicitly.
- Credentials belong in documented environment variables, not CLI arguments, logs, skill files or committed documentation.
- A read-only audit must not mutate ADRs; index generation is a separate, intentional write.

## Evidence and honest uncertainty

When evaluating an ADR, distinguish observed evidence from hypotheses, possible risks and missing context. Never invent SLAs, legal duties, traffic volumes, estimates, review approvals or implementation findings.

The `adr-guard-audit` skill checks **documentation and governance health** through the documented audit workflow. It does not silently run or replace the CLI's separate [Architecture Impact Analysis](../../product/architecture-impact/) and [Architectural Drift](../../product/architectural-drift/) commands. Those commands require explicit mappings, rules, evidence, and policy choices; their output still does not approve an ADR.

## Lifecycle and compatibility

- Canonical documents use `Proposed`, `Accepted`, `Deprecated` and `Superseded` by default. Custom statuses must be configured explicitly.
- Superseding an accepted decision requires a separate new record. A `Proposed` successor does **not** immediately supersede the active predecessor.
- Canonical Minimal, Extended and constrained Custom can be generated with `new`. **MADR 4.0** uses separate opt-in validation; `new --template madr-4` is not supported.
- Do not silently migrate formats or mix canonical and MADR documents in one validation set.

## Before you adopt a skill

1. Check whether its responsibility fits the task on the [catalog page](../catalog/).
2. Review the [source instructions](https://github.com/rodri-oliveira-dev/adr-guard/tree/main/skills) and their license.
3. Confirm which CLI and agent versions are installed.
4. Validate the authorized outcome using the real ADR Guard CLI; investigate nonzero exit codes.
5. Obtain human review for architectural merit, status changes and governance decisions.

[Install a skill](../installation/) · [Explore examples](../examples/) · [ADR Guard security details](../../product/security/)
