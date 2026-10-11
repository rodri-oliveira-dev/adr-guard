# Protect the domain boundary

## Status

Accepted

## Context

Domain code must remain independent from infrastructure details.

## Decision

The Domain project must not reference the Infrastructure project.

## Consequences

The exact project dependency direction is verified as an architecture fitness rule. Conditional or dynamically expanded references are inconclusive and require human review.
