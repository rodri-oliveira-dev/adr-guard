# Use PostgreSQL for transactional data

## Context and Problem Statement

The service requires transactional persistence.

## Considered Options

* PostgreSQL
* SQLite

## Decision Outcome

Chosen option: "PostgreSQL", because it meets the concurrency and durability requirements.

### Consequences

* Good, because transactions are supported.
* Bad, because operations require a managed database.
