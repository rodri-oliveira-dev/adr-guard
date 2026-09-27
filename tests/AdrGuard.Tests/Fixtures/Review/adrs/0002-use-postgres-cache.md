# Use PostgreSQL Cache

## Status
Accepted

## Context
A prior active decision keeps transient shared state in the primary database.

## Decision
Do not use Redis; store transient shared cache state in PostgreSQL.

## Consequences
Database load and cache expiration semantics must be monitored.
