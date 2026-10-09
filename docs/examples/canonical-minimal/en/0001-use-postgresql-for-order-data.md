# Use PostgreSQL as the Order System of Record

## Status

Accepted

## Context

The order service stores order headers, line items, payment references, and state transitions. The current document store requires compensating updates when several records change together, and support staff cannot reliably reconstruct who changed an order and when. New audit requirements take effect before the next peak season. The service team can migrate during two scheduled maintenance windows, and the platform team already operates managed PostgreSQL for other services.

We considered retaining the document store with additional compensation and audit logic, or moving the authoritative order data to a relational database. The decision prioritizes atomic updates, enforceable relationships, auditability, and an operational model the organization already supports.

## Decision

We will use managed PostgreSQL as the system of record for new and migrated order data. Order headers, line items, payment references, and state transitions will share a relational schema and transactional boundary. The service remains the only writer; other systems continue to integrate through its API and events rather than direct database access.

## Consequences

Atomic transactions and relational constraints will protect order consistency, and the existing platform capability will provide backups, patching, monitoring, and recovery procedures. The schema and migration history will make data changes easier to audit.

The team must design and rehearse a migration, run temporary reconciliation during cutover, and maintain rollback criteria. Developers need to manage schema evolution and query performance. PostgreSQL becomes a critical dependency with storage cost and on-call responsibilities, and consumers cannot use the database as a shared integration surface.
