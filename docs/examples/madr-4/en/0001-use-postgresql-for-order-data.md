---
status: "accepted"
decision-makers: Order service and platform owners
consulted: Security, data governance, and support teams
informed: Product and analytics teams
---

# Use PostgreSQL as the Order System of Record

## Context and Problem Statement

The order service must update order headers, line items, payment references, and state transitions atomically and provide an auditable history before the next peak season. The current document store depends on compensating updates and cannot meet that need without new mechanisms. Migration is limited to two maintenance windows, and the platform team already operates managed PostgreSQL.

## Decision Drivers

- Atomic consistency and enforceable relationships
- Auditable data changes
- Rehearsed migration and rollback before the deadline
- Existing backup, monitoring, recovery, and on-call capability
- API and events remain the service integration boundary

## Considered Options

- Keep the document store and add compensation, reconciliation, and a separate audit log.
- Use managed PostgreSQL for authoritative order data.
- Use a distributed SQL database operated by the service team.

## Decision Outcome

Chosen option: use managed PostgreSQL, because it satisfies the transaction and audit drivers while reusing an established operational model. Order data will share a relational schema and transactional boundary. Only the order service may write directly.

### Consequences

- Good, because transactions and constraints protect order consistency.
- Good, because existing platform procedures cover backup, patching, monitoring, and recovery.
- Bad, because migration requires dual operation, reconciliation, and rehearsed rollback.
- Bad, because the team assumes schema-evolution, query-performance, cost, and on-call responsibilities.

### Confirmation

Before cutover, production-sized migration rehearsal must meet the maintenance window, reconciliation must show no unexplained differences, and transaction and recovery tests must pass. The service owner will review these checks after launch.

## Pros and Cons of the Options

### Keep the document store

- Good, because it avoids an immediate data migration.
- Bad, because the team must build and prove compensation and auditing under a fixed deadline.

### Managed PostgreSQL

- Good, because it meets consistency and audit needs with existing platform support.
- Bad, because it introduces migration and relational-database ownership for the service team.

### Distributed SQL

- Good, because it provides relational semantics with additional distribution options.
- Bad, because its new operational model and complexity are not justified by the current regional availability target.

## More Information

Implementation plans, performance evidence, and the operating runbook will be attached to the delivery work when finalized.
