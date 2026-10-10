---
name: adr-guard-tradeoff-analysis
description: Compare architecture alternatives, decision drivers, risks, and evidence before drafting an Architecture Decision Record. Use when selecting between credible technical approaches, facilitating decision workshops, or strengthening a Proposed ADR's rationale.
license: MIT
compatibility: CLI is optional for analysis; ADR Guard's canonical Extended template supports explicit alternatives but never chooses on behalf of the team.
---

# Structure architecture trade-off analysis

The objective is an **auditable decision argument**, not a scoring algorithm that silently selects architecture. This skill prepares information for `adr-guard-create` or human review.

## Workflow

1. State the **problem and decision scope**, not a preferred technology. Identify constraints, stakeholders, owners, decision deadline if known, and what changes if no action is taken.
2. Identify 2 or more *credible* options when they exist. Include the status quo when viable; explicitly state why an option is not viable rather than inventing token alternatives.
3. Record decision drivers and measurable quality concerns: functional requirements, latency, throughput, correctness, consistency, resilience, security, privacy, operability, cost, lock-in, migration, team experience and time-to-deliver **only where relevant**.
4. Distinguish **hard constraints** from **preferences** and assign each observation an evidence level: documented/observed, assumption, experiment required, or not enough information. Attach source references or decision-owner confirmation to factual claims.
5. Build a comparison table with one row per driver and one column per option. Use concise evidence + trade-off, not ungrounded numbers.

   | Driver | Option A | Option B | Evidence / uncertainty |
   | --- | --- | --- | --- |
   | Availability requirement | Known impact or unknown | Known impact or unknown | Measured source or open question |
   | Operational ownership | Known support burden | Known support burden | Owner confirmation |

6. Analyze the **downside** of each option (operational complexity, failure modes, security exposure, migration effort and reversal cost), not just benefits.
7. If quantified scores or weights are **requested**, ask owners to authorize criteria, weights and values; label uncertain values, show sensitivity to changed weights and avoid presenting totals as objective facts. Qualitative comparisons are acceptable and often more truthful.
8. Identify missing experiments, time-boxed spikes, load tests, cost estimates, security reviews or business inputs that could change the recommendation.
9. Offer a reasoned *candidate recommendation* only with supporting evidence and uncertainty, clearly subject to human decision and stakeholder review. Do not auto-set `Accepted`.
10. When the decision is ready for recording, recommend ADR Guard's canonical `extended` template:

    ```bash
    adr-guard new docs/adr --title "Choose the order event transport" --template extended --preview
    ```

    `--preview` has no write side effects; an authorized subsequent `new` creates a **Proposed** ADR. The agent must not claim that MADR 4.0 generation is supported.

## Output

Return: decision statement; options and status quo; constraints; criteria; evidence-linked comparison; upside/downside; recommendation or explicit indecision; experiments; review owners. Transfer this analysis to an ADR only with authorization.

Product references: [Writing effective ADRs](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/docs/decision-design/writing-effective-adrs.md) and [decision categories](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/docs/decision-design/decision-categories.md).
