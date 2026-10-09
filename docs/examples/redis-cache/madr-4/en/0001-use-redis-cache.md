---
status: "proposed"
decision-makers: Product API and platform owners
consulted: Security and data governance teams
informed: Product and support teams
---

# Adopt Redis for Distributed Product Caching

## Context and Problem Statement

The product API repeatedly reads prices from the database, increasing p95 latency and database load during catalog peaks. Prices may be stale by no more than 30 seconds. The API must continue serving reads when the cache is unavailable, and multiple replicas should share cached values.

## Decision Drivers

- Reduce p95 latency and source queries at catalog peaks
- Bound price staleness to 30 seconds
- Share cache entries across API replicas
- Keep database fallback correct during cache outages
- Control operating cost and sensitive-data exposure

## Considered Options

- No cache: query the database directly
- In-process caching per replica
- Managed Redis shared across replicas

## Decision Outcome

Chosen option: Adopt managed Redis with cache-aside reads for product prices and a 30-second TTL. Use versioned cache keys, bounded connection timeouts, and database fallback on cache failure. Do not cache authorization data or sensitive personal information. Database-only reads are simple but preserve peak amplification. In-process caches help each replica but complicate consistency between replicas. Redis makes entries reusable with an explicit staleness window, while cache-aside preserves the database as the source of truth.

### Consequences

- Good, because warm entries reduce database traffic and read latency.
- Good, because shared values improve cache reuse across API replicas.
- Bad, because redis introduces new infrastructure cost and operational failure modes.
- Bad, because cache invalidation, serialization, capacity, and fallback need ongoing testing.

### Confirmation

Before rollout expansion, load tests must show improved p95 and hit rate, freshness must stay within 30 seconds, and fault-injection tests must show correct database fallback. Product API owners will evaluate the results.

## Pros and Cons of the Options

### No cache

- Good, because operations remain simple.
- Bad, because peak load still pressures the database.

### In-process cache

- Good, because local reads are fast.
- Bad, because entries can diverge between replicas.

### Managed Redis

- Good, because it shares entries with explicit TTLs.
- Bad, because it adds cost and stampede risk.

## More Information

The benchmark, rollout plan and degraded-mode runbook will be linked in the delivery PR.
