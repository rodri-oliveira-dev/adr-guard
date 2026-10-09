# Adopt Redis for Distributed Product Caching

## Status

Proposed

## Context

The product API repeatedly reads prices from the database, increasing p95 latency and database load during catalog peaks. Prices may be stale by no more than 30 seconds. The API must continue serving reads when the cache is unavailable, and multiple replicas should share cached values.

## Decision Drivers

- Reduce p95 latency and source queries at catalog peaks
- Bound price staleness to 30 seconds
- Share cache entries across API replicas
- Keep database fallback correct during cache outages
- Control operating cost and sensitive-data exposure

## Options Considered

1. No cache: query the database directly
2. In-process caching per replica
3. Managed Redis shared across replicas

## Decision

Adopt managed Redis with cache-aside reads for product prices and a 30-second TTL. Use versioned cache keys, bounded connection timeouts, and database fallback on cache failure. Do not cache authorization data or sensitive personal information.

## Rationale

Database-only reads are simple but preserve peak amplification. In-process caches help each replica but complicate consistency between replicas. Redis makes entries reusable with an explicit staleness window, while cache-aside preserves the database as the source of truth.

## Consequences

Shared entries should reduce repeated database reads and improve p95 latency. The team must own Redis cost, memory, capacity, eviction, invalidation, monitoring, and degraded-mode tests. Cache misses still require well-performing database queries.

## Positive Consequences

- Warm entries reduce database traffic and read latency.
- Shared values improve cache reuse across API replicas.

## Negative Consequences

- Redis introduces new infrastructure cost and operational failure modes.
- Cache invalidation, serialization, capacity, and fallback need ongoing testing.

## Risks

- Hot keys and stampedes require concurrency controls and sensible TTL jitter.
- Redis failure can amplify origin load; exercise bounded timeouts and backpressure.
- A 30-second price TTL is not safe by default for inventory or authorization.

## References

The benchmark, rollout plan and degraded-mode runbook will be linked in the delivery PR.
