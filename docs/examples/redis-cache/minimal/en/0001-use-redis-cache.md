# Adopt Redis for Distributed Product Caching

## Status

Proposed

## Context

The product API repeatedly reads prices from the database, increasing p95 latency and database load during catalog peaks. Prices may be stale by no more than 30 seconds. The API must continue serving reads when the cache is unavailable, and multiple replicas should share cached values.

## Decision

Adopt managed Redis with cache-aside reads for product prices and a 30-second TTL. Use versioned cache keys, bounded connection timeouts, and database fallback on cache failure. Do not cache authorization data or sensitive personal information.

## Consequences

Shared entries should reduce repeated database reads and improve p95 latency. The team must own Redis cost, memory, capacity, eviction, invalidation, monitoring, and degraded-mode tests. Cache misses still require well-performing database queries.
