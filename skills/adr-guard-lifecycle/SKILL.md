---
name: adr-guard-lifecycle
description: Maintain Architecture Decision Record lifecycle states, audit their approval evidence, and preserve decision history with ADR Guard. Use when a proposal is accepted or rejected by humans, a decision is deprecated, or a new ADR replaces an existing decision.
license: MIT
compatibility: ADR Guard CLI is required for final structural and relationship checks.
---

# Manage ADR lifecycle without rewriting history

The agent may **prepare or verify** a transition, but only authorized people approve a decision.

## Workflow

1. Identify the ADR ID, current status, documented authority, affected teams, and the event that triggered reconsideration. Confirm a human decision before applying a status transition.
2. Recognize default canonical states: `Proposed`, `Accepted`, `Deprecated`, and `Superseded`. Additional labels such as `Rejected` or `Under Review` exist only when the repository explicitly configures their semantic mappings; do not assume they are allowed.
3. For a proposal, capture stakeholder feedback and objections before recording the outcome. Never equate a passing `check` or `review` with acceptance.
4. For deprecation, record why a decision is no longer recommended; do not invent a replacement.
5. For supersession, **create a separate new Proposed ADR first**, with its alternatives and implications. Link the new ADR's `Supersedes` reference as appropriate. The predecessor may stay `Accepted` while the successor is only Proposed.
6. **After** authorized acceptance of the replacement, update the predecessor's status and explicit `Superseded by` link to the active successor. Do not introduce self-links, cycles, conflicting targets or dependencies on inactive decisions.
7. Preserve all old records and original rationale. A typo/link correction may edit an existing ADR; a material change needs a new decision record.
8. Verify the graph and refresh the index only after success:

   ```bash
   adr-guard check docs/adr
   adr-guard index docs/adr
   ```

## Edge cases

- Mixed canonical and MADR records require separate format-aware validation. MADR supersession uses its own documented status convention, not the canonical headings blindly.
- Rejected is not a default canonical status; treat rejection/approval as a human outcome and check the configured lifecycle mapping before editing.
- If authorization, target ID or evidence is missing, propose a transition plan and stop short of the status edit.
- Relationships reflect explicit declarations, not inferred text or proximity in the repository.

## Output

Provide before/after state (if authorized), human decision reference, new ADR ID, explicit links updated, `check` result, and historical records preserved.

Product references: [lifecycle](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/docs/fundamentals/lifecycle.md) and [relationship governance](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/docs/relationship-governance.md).
