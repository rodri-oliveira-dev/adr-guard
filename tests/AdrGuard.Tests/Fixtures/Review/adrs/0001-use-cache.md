# Use Redis Cache

## Status
Proposed

## Context
The service needs low-latency repeated reads without making architecture review authoritative.

## Decision
Use Redis for bounded application caching.

## Consequences
Cache invalidation, observability, and fallback behavior require human review.
