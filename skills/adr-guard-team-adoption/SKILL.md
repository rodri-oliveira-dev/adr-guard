---
name: adr-guard-team-adoption
description: Design a lightweight human-governed ADR adoption process covering ownership, review, templates, rollout, and CI expectations. Use when introducing ADRs to an engineering team, standardizing how decisions are documented, or improving existing architecture governance.
license: MIT
compatibility: The process can be proposed without CLI; adoption implementation uses ADR Guard CLI and optional GitHub Actions.
---

# Introduce ADRs as a team practice

Adoption should improve **shared architectural memory**, not create a document quota or an unapproved bureaucracy.

## Workflow

1. Understand the team landscape: repositories, services, existing decisions, stakeholder access, regulatory constraints **if documented**, delivery cadence and current review practices. Identify which information is missing.
2. Agree with humans on the **decision significance threshold**. Use impact, reversibility, reach, risk, cost and long-lived trade-offs; direct edge cases to `adr-guard-when-to-record`.
3. Propose a small, explicit governance policy:

   | Concern | Questions to settle |
   | --- | --- |
   | Record ownership | Who can propose, who maintains links and context? |
   | Decision authority | Who must be consulted, and who can approve or reject? |
   | Review | Which security, data, operations or product owners need review for each risk level? |
   | Lifecycle | How are Proposed, Accepted, Deprecated and Superseded documented, including locally mapped custom statuses? |
   | Format | Canonical Minimal/Extended/Custom, or a deliberately separate MADR 4.0 directory? |
   | Delivery | When are check, index and CI used? Who owns failures and exceptions? |
   | Discoverability | Where will ADRs and decision indexes be referenced? |

4. Choose the smallest default: **Minimal** for everyday significant decisions; Extended when options, cross-team constraints or risk merit it. A custom template must satisfy ADR Guard's canonical contract. MADR 4.0 is separately authored; `new --template madr-4` does not exist.
5. Plan a progressive rollout: choose 1–3 consequential new decisions, write **Proposed** records, hold human review, document approval authority, validate and add a discoverable index. Do **not** backfill all old choices automatically.
6. Pilot quality with affected stakeholders: can a new maintainer explain why the choice was made, what was rejected, who owns follow-up and when to reconsider? Adjust ceremony based on feedback, not ADR count targets.
7. Once the team approves the policy, hand off to `adr-guard-init` and `adr-guard-ci-setup` for authorized changes. Example commands (only after directory and version checks):

   ```bash
   adr-guard init . --adr-directory docs/adr --template minimal --dry-run
   adr-guard check docs/adr
   ```

8. Define a periodic *human-owned* review of decision freshness and relationships. Use `adr-guard-audit` for evidence gathering; never make periodic status changes without approved decisions.

## Safety and scope

- Do not write an organization-wide policy without agreement from the actual authority. Repository maintainers may propose but not necessarily authorize cross-team standards.
- A passing validation check confirms structural compliance only. An AI review cannot confer approval.
- Support stakeholders without Git access; document how their review is recorded without pretending a Git identity proves consent.
- Do not invent an SLA, adoption ROI, compliance mandate, or approval workflow. If no authority is identified, propose an option and mark it **pending agreement**.

## Output

Produce a lightweight adoption plan with decision triggers, roles and authority, format/template choices, PR lifecycle, opt-in CI, pilot scope, training/reference material, ownership and adoption feedback checkpoints. Clearly label proposed versus agreed governance.

Product references: [Team adoption](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/docs/guides/team-adoption.md), [When to write ADRs](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/docs/fundamentals/when-to-write-an-adr.md), and [MADR support](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/docs/madr-4.md).
