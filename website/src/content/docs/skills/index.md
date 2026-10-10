---
title: Agent Skills for ADR Guard
description: Discover 11 portable Agent Skills that guide architecture decision creation, validation, review, lifecycle stewardship, and team adoption.
sidebar:
  order: 1
---

**Agent Skills** are reusable, task-focused instructions that tell a compatible coding agent *when* and *how* to help with Architecture Decision Records (ADRs). ADR Guard publishes **11 skills** covering the lifecycle from identifying a significant decision to reviewing and maintaining its history.

Skills make the tooling easier to discover and apply consistently. They do **not** replace your architecture team, install the ADR Guard CLI, or grant an agent authority to accept decisions.

## What can you do with them?

| Goal | Start with | Outcome |
| --- | --- | --- |
| Decide whether documentation is warranted | [When to record](./catalog/#adr-guard-when-to-record) | A reasoned recommendation: ADR, lighter documentation or more evidence |
| Discuss viable options | [Trade-off analysis](./catalog/#adr-guard-tradeoff-analysis) | Explicit alternatives, constraints, consequences and uncertainty |
| Start documenting a new decision | [Create an ADR](./catalog/#adr-guard-create) | A human-reviewable **Proposed** ADR |
| Improve an existing record | [Validate](./catalog/#adr-guard-validate) and [Technical review](./catalog/#adr-guard-technical-review) | Structural diagnostics and separate evidence-backed suggestions |
| Change an accepted decision | [Supersede](./catalog/#adr-guard-supersede) | A new proposal and traceable relationship without rewriting history |
| Govern an ADR collection | [Audit](./catalog/#adr-guard-audit) and [Team adoption](./catalog/#adr-guard-team-adoption) | Read-only assessment and a lightweight human-owned process |

## How they work

1. You install the specific skill into a supported agent environment.
2. The agent reads the skill's `SKILL.md` only when the task is relevant.
3. It checks the repository's ADR format, current files and CLI availability.
4. It helps formulate questions or, **with your authorization**, invokes existing ADR Guard commands.
5. You and the people responsible review evidence and make the actual decision.

For example, ask your agent: **“Help me decide whether our switch to asynchronous messaging needs an ADR; compare alternatives without assuming the outcome.”**

The agent may use `adr-guard-when-to-record` and `adr-guard-tradeoff-analysis`. A separate request to create the record can use `adr-guard-create`. A successful `adr-guard check` validates the record's structure—not architectural merit.

## Start here

- [Install skills and prerequisites](./installation/) — discover, install and verify the separate CLI.
- [Browse all 11 skills](./catalog/) — find the right workflow and responsibility boundary.
- [Try end-to-end scenarios](./examples/) — from new decision to audit and supersession.
- [Read the safety and governance rules](./security-and-governance/) — approvals, privacy, untrusted input, formats and limitations.

The [Agent Skills specification](https://agentskills.io/specification) defines the portable `SKILL.md` format. The [Skills CLI](https://github.com/vercel-labs/skills) provides discovery and installation into supported coding agents.
