---
title: Database technology selection
description: Select persistence for a tenant billing ledger with objective criteria and explicit operational consequences.
sidebar:
  order: 4
---

## Context and problem

A billing service needs transactional writes, immutable financial entries, tenant isolation, ad-hoc reconciliation queries, and five-year retention. The team already operates PostgreSQL; a document database and a cloud key-value store are also available.

## Objective criteria

| Criterion | Weight | PostgreSQL | Document DB | Key-value store |
| --- | ---: | --- | --- | --- |
| Multi-row transactions | High | Native | Available with caveats | Model-dependent |
| Reconciliation queries | High | Strong SQL support | Aggregation-specific | Requires secondary pipelines |
| Operational familiarity | Medium | High | Medium | Medium |
| Horizontal write scale | Medium | Requires planning | Strong | Strong |
| Schema evolution | Medium | Explicit migrations | Flexible documents | Application-owned |

## Decision

Use managed PostgreSQL with append-only ledger tables, tenant-keyed row-level access controls, tested migrations, encryption, point-in-time recovery, and partitioning reviewed as volume grows.

## Consequences

Transactions and reconciliation remain in one well-understood system. The team accepts migration discipline, capacity planning, connection pooling, backup verification, and possible future partitioning. Flexible payloads are stored only where query and integrity requirements allow them.

## Risks and limits

Tenant isolation depends on both application tests and database policies. Long retention can make indexes and backups expensive. The choice should be revisited if measured write volume or regional availability requirements exceed the design envelope.

## When Extended fits

Data technology is expensive to reverse and affects security, operations, cost, and downstream analytics. A criterion table prevents “we prefer this tool” from masquerading as rationale.
