---
name: adr-guard-create
description: Create an Architecture Decision Record with ADR Guard, using Minimal, Extended, or Custom templates and human-owned architectural reasoning. Use when documenting a new significant decision, drafting alternatives, or recording consequences before implementation.
license: MIT
compatibility: Requires ADR Guard CLI for creating files; optional AI drafting requires an explicit provider, model, and approved context.
---

# Create an ADR

Create **one consequential decision per ADR**. The agent helps organize evidence; the team owns the decision.

## Workflow

1. Establish the problem, decision owner, scope, constraints, stakeholders, options (including status quo when viable), decision drivers, risks, validation criteria, and unknowns. Do not invent data, SLAs, approvals or legal requirements.
2. Review existing ADRs and possible conflicts. Keep independent choices in separate records.
3. Select Minimal for focused choices, Extended for complex/multi-team trade-offs, or one local constrained Custom template when mandated by policy. Do not confuse Extended with MADR 4.0.
4. Confirm the CLI and ADR directory are available. First inspect the candidate without writing:

   ```bash
   adr-guard new docs/adr --title "Use PostgreSQL for order records" --template extended --preview
   ```

5. With authorization, generate the **Proposed** ADR:

   ```bash
   adr-guard new docs/adr --title "Use PostgreSQL for order records" --template extended
   ```

6. Replace instructional placeholders with specific, evidenced context, rationale, considered alternatives and **positive and negative** consequences. Include risk owners and follow-ups when known; label uncertainty explicitly.
7. Validate, and only after success update the index:

   ```bash
   adr-guard check docs/adr
   adr-guard index docs/adr
   ```

8. Request human feedback from affected owners; never change the status to `Accepted` on your own.

## Optional provider-assisted drafting

Only with explicit authorization for third-party data transfer, and with a selected model/provider:

```bash
adr-guard draft docs/adr --title "Use PostgreSQL for order records" \
  --context "Documented business and technical decision context" \
  --provider openai --model YOUR_CHOSEN_MODEL --template extended --preview
```

Never silently attach source code, sensitive data or existing ADRs. `--context-file` and `--include-existing-adrs` expand transmitted context only when individually authorized. A preview reserves no ID. `new` and `draft` generate canonical Proposed ADRs, **not** MADR documents or accepted decisions.

## Output

Provide the ADR path, ID, chosen format, rationale and trade-offs captured, evidence gaps, validation outcome and reviewers needed. Do not claim correctness from a successful structural check.

Product references: [creation](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/docs/creation.md), [template selection](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/docs/decision-design/choosing-a-template.md), and [AI drafting privacy](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/docs/draft-templates.md).
