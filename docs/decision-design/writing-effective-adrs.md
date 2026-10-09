# Write effective ADRs

[Português (Brasil)](writing-effective-adrs.pt-BR.md) · [Documentation home](../index.md) · [Previous](choosing-a-template.md) · [Next](anti-patterns.md)

A strong ADR lets a future reader reconstruct the decision without attending the original meeting. It is specific enough to guide implementation while staying focused on one outcome.

## Build the argument

1. **Name the outcome.** Prefer “Use PostgreSQL as the order system of record” to “Database decision.”
2. **Describe the context neutrally.** State the problem, stakeholders, constraints, current conditions, and relevant evidence before arguing for an option.
3. **Identify real alternatives.** Include the status quo when it is viable. For consequential choices, explain why each serious option succeeds or fails against the drivers.
4. **State the decision actively.** Define what is chosen, its scope, and important boundaries. Avoid vague language such as “consider using.”
5. **Record balanced consequences.** Include benefits, costs, risks, operational ownership, migration, and what becomes harder. Consequences are not only advantages.
6. **Make uncertainty visible.** Record assumptions, missing evidence, follow-up experiments, and conditions that would trigger reconsideration.
7. **Invite the right review.** Include people affected by security, operations, data, product, cost, or cross-team contracts.

## Good and weak statements

| Weak | Stronger |
| --- | --- |
| “Use PostgreSQL because it is popular.” | “Use PostgreSQL as the order system of record because atomic updates and relational constraints satisfy the transaction and auditability drivers; the team accepts migration and operational ownership.” |
| “Kafka is scalable.” | “Use Kafka for durable domain-event distribution where consumers need independent replay; do not use it for synchronous request/response.” |
| “There are no downsides.” | “The choice adds backup, patching, monitoring, and on-call responsibilities; the platform team will own the managed instance.” |
| “Approved.” | “Accepted by the service and platform owners after the migration rehearsal demonstrated rollback within the agreed window.” |

Keep links to evidence stable and summarize the decision-critical fact in the ADR so the reasoning survives if an external page disappears. Remove authoring instructions and unresolved placeholders before seeking acceptance.

ADR Guard validation can confirm the expected structure and relationships. AI-assisted `draft` and `review` can propose text or findings, but neither supplies evidence, stakeholder consent, nor approval.
