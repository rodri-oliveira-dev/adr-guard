---
title: Adopting Redis for distributed caching
description: Compare a consistent caching decision in Minimal, Extended, and MADR 4.0 formats.
sidebar:
  order: 2
---

## Scenario

A product API repeats read-heavy queries. p95 latency rises during catalog traffic peaks, while price data may be stale for no more than 30 seconds. The team can operate a managed service but does not want cache availability to become a prerequisite for correct reads.

## Alternatives and criteria

| Option | Latency | Consistency control | Cross-instance reuse | Operational cost |
| --- | --- | --- | --- | --- |
| No cache | Weak at peak | Strong | N/A | Low |
| In-process cache | Good per instance | Harder to coordinate | No | Low–medium |
| Managed Redis | Good | Explicit TTL and invalidation | Yes | Medium |

## Decision

Adopt managed Redis with cache-aside reads, a 30-second price TTL, bounded connection timeouts, and fallback to the database. Cache keys are versioned. Hit rate, latency, errors, and stale-read indicators are observable before rollout expands.

## Consequences

**Positive:** repeated reads leave the database, response latency becomes less sensitive to catalog peaks, and replicas share warm values.

**Negative:** the team owns invalidation behavior, cost, capacity, connection management, and degraded-mode testing. A cache does not repair slow or incorrect source queries.

## Risks and limits

Stampedes, hot keys, serialization incompatibility, and accidental caching of sensitive data require controls. The 30-second tolerance is domain-specific and must not be copied to inventory or authorization data.

## Three complete representations

- [Canonical Minimal source](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/docs/examples/redis-cache/minimal/en/0001-use-redis-cache.md) — enough for a bounded pilot whose constraints are already understood.
- [Canonical Extended source](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/docs/examples/redis-cache/extended/en/0001-use-redis-cache.md) — best when reviewers need explicit drivers and options.
- [MADR 4.0 source](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/docs/examples/redis-cache/madr-4/en/0001-use-redis-cache.md) — appropriate when the repository standardizes on MADR and validates separately with `--adr-format madr-4`.

Use the [interactive format comparison](/adr-guard/templates/#compare-the-same-decision) to switch between the three structures.
