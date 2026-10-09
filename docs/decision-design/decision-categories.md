# Decision categories

[Português (Brasil)](decision-categories.pt-BR.md) · [Documentation home](../index.md) · [Next](formats-and-templates.md)

A decision category describes **what the choice affects**. A template or format describes **how the record is organized**. Do not confuse them: any category can use Minimal, Extended, Custom, or an independently authored MADR record when its validation mode is selected.

| Category | Example questions |
| --- | --- |
| System architecture | Where are component boundaries? Which responsibilities belong together? |
| Data | What is the system of record? How are data ownership, consistency, retention, and migration handled? |
| Integration | Which API, event, messaging, or compatibility contract connects systems? |
| Security and privacy | How are identity, authorization, secrets, threat controls, and sensitive data handled? |
| Infrastructure and deployment | Which runtime topology, platform, region, or delivery approach is used? |
| Resilience | What availability target, failure isolation, retry, recovery, or continuity strategy applies? |
| Observability | Which signals, correlation model, retention, and operational ownership make behavior diagnosable? |
| Development and delivery | Which build, testing, branching, dependency, or release convention has architectural impact? |
| Governance | Who owns a contract, which standard applies, or how is a cross-team decision controlled? |

Categories are discovery aids, not mandatory labels in ADR Guard. One decision may span several domains; choose a title and scope around the single outcome the team needs to decide. If two choices can change independently, they usually deserve separate ADRs.

Category can help identify reviewers. A data-retention choice may need security and legal input; a resilience choice may need operations and product input. It should not automatically determine status, approval authority, or template depth—those belong to the team's governance and the decision's risk.
