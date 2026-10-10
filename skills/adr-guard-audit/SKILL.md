---
name: adr-guard-audit
description: Audit an existing Architecture Decision Record collection for structural health, discoverability, lifecycle clarity, potentially outdated assumptions, and governance gaps. Use when assessing ADR documentation quality, onboarding into a mature repository, or planning a periodic review.
license: MIT
compatibility: Uses installed ADR Guard CLI for deterministic checks; qualitative staleness and implementation claims require supplied evidence and human verification.
---

# Audit ADR quality and governance

This skill produces a **read-only, evidence-based assessment**. It does not infer architectural conformance from a Markdown file or turn missing context into a violation.

## Workflow

1. Confirm repository scope, selected ADR directory, canonical vs MADR format, review period (if applicable), permissions and known ownership. Do not crawl unapproved folders, network sites or private sources.
2. Determine the current ADR Guard CLI version and run a **read-only** structural check:

   ```bash
   adr-guard check docs/adr --format json
   ```

   For a separately maintained MADR directory instead use `adr-guard check docs/decisions --adr-format madr-4 --format json`. Check format once per directory; do not mix structures.
3. Record objective evidence: duplicate/invalid IDs, malformed status, required sections, broken local links, inconsistent supersession relationships, disabled/changed validation profiles, diagnostic counts and affected files. Cite actual diagnostics and paths rather than reimplementing checker rules.
4. Review documentation quality **qualitatively**, distinguishing **observed**, **potential risk**, and **unknown**:
   - Traceable problem/context, ownership, rationale and meaningful alternatives.
   - Balanced operational, security, financial and migration consequences where relevant.
   - Evidence references, assumptions, verification criteria and revisit triggers.
   - Proposal age, stalled human review, or unclear approval evidence **only when chronology/evidence exists**.
   - Potentially stale assumptions or obsolete dependencies **only when compared with authorized, recent evidence**.
5. Examine ADR discoverability and index presence. If the index is missing or stale, recommend `adr-guard index docs/adr` but **do not run it during a read-only audit** because it writes `README.md`.
6. If supplied, inspect repository/CI documentation as *corroborating evidence*. Never claim the CLI currently scans source code for architectural drift. Code-to-ADR impact analysis and drift detection are separate future roadmap capabilities, not P1 audit behavior.
7. Group findings by severity of **documented risk and uncertainty**, not by dramatic model language. Distinguish CLI validation failures, editorial quality opportunities and questions needing authorized human judgment.
8. Offer a prioritized remediation plan with owners, evidence needed, and a separate approval gate for status changes, template migrations or repository writes. Do not alter the ADRs, policies or CI as part of the audit.

## Safety

Never fabricate obsolete status, audit compliance, team decisions, violated standards or code evidence. An `Accepted` ADR is not necessarily implemented, and a clean structural check is neither technical validation nor a compliance certificate. If the tool or evidence is unavailable, state what was not verified.

## Output

Provide scope, tool version/format, CLI result, counts by category, concrete findings (ADR ID + evidence + confidence), unknowns, remediation priority and explicitly **no files changed**. Avoid treating high ADR count as a quality metric.

Product references: [ADR anti-patterns](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/docs/decision-design/anti-patterns.md), [CLI reports](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/docs/check-reports.md) and [Team adoption](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/docs/guides/team-adoption.md).
