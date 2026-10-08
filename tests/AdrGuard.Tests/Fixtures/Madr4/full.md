---
status: "accepted"
date: 2026-10-08
decision-makers: Architecture team
consulted: Operations team
informed: Product team
---

# Use PostgreSQL

## Context and Problem Statement

The service requires durable storage.

## Decision Drivers

* Durability

## Considered Options

* PostgreSQL
* SQLite

## Decision Outcome

Chosen option: "PostgreSQL", because it satisfies the decision drivers.

### Consequences

* Good, because it supports the required isolation.
* Bad, because it requires operational ownership.

### Confirmation

An integration test confirms transaction behavior.

## Pros and Cons of the Options

### PostgreSQL

* Good, because it supports the required isolation.
* Neutral, because both options use SQL.
* Bad, because it requires a separate service.

## More Information

See the database runbook.
