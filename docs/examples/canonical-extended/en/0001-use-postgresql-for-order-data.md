# Use PostgreSQL as the Order System of Record

## Status

Accepted

## Context

The order service stores order headers, line items, payment references, and state transitions. The current document store requires compensating updates when records change together and does not provide the audit trail required before the next peak season. Migration is limited to two maintenance windows. The platform team already operates managed PostgreSQL, while adding a new database technology would require a new support model.

## Decision Drivers

- Preserve atomic consistency across an order aggregate.
- Enforce relationships and retain an auditable change history.
- Complete migration with rehearsed rollback before the compliance deadline.
- Reuse operational capabilities for backup, monitoring, recovery, and on-call support.
- Keep the service API and events as the integration boundary.

## Options Considered

1. Keep the document store and add compensation, reconciliation, and a separate audit log.
2. Adopt managed PostgreSQL for authoritative order data.
3. Adopt a distributed SQL database managed by the service team.

## Decision

We will adopt managed PostgreSQL for new and migrated order data. Order headers, line items, payment references, and state transitions will share a relational schema and transactional boundary. Only the order service may write directly; consumers continue through its API and events.

## Rationale

PostgreSQL meets the transaction and integrity drivers without introducing an unfamiliar operational platform. Keeping the document store would require the team to build and prove compensation and audit mechanisms under a fixed deadline. Distributed SQL could satisfy the data requirements but adds operational complexity that the current regional availability target does not justify.

## Consequences

The decision improves consistency and auditability but requires migration, schema governance, capacity planning, and explicit operational ownership.

## Positive Consequences

- Transactions and constraints protect relationships within an order.
- Existing managed-service procedures cover backup, monitoring, patching, and recovery.
- Schema migrations create a reviewable history of structural changes.
- The organization does not add another database support model.

## Negative Consequences

- Two database systems must run during migration and reconciliation.
- The team must learn relational query tuning and safe schema evolution.
- Storage, replicas, and migration work add cost.
- Direct database access remains unavailable to analytics and other services; they need supported events or exports.

## Risks

- A long cutover could exceed the maintenance window. The service owner will rehearse production-sized migration and define rollback thresholds.
- Dual writes could diverge. The migration lead will run reconciliation and block cutover on unexplained differences.
- Poor indexes could harm latency. The service team will test representative queries and monitor slow-query signals before and after launch.

## References

The implementation plan, performance evidence, and runbook will be linked in the delivery pull request after their repository paths are finalized.
