---
title: Synchronous vs asynchronous communication
description: Decide how order processing services communicate across latency, coupling, resilience, consistency, and operations.
sidebar:
  order: 3
---

## Context and problem

Checkout must request fulfillment after payment authorization. A synchronous HTTP chain is simple to trace but extends user-facing latency and couples availability. Messaging isolates outages but introduces eventual consistency, retries, duplicate delivery, and operational machinery.

## Criteria and alternatives

| Criterion | Synchronous HTTP | Asynchronous message |
| --- | --- | --- |
| Coupling | Temporal and availability coupling | Contract coupling without simultaneous availability |
| Resilience | Failure propagates unless isolated | Broker buffers temporary consumer failure |
| User latency | Includes downstream work | Stops after durable publication |
| Consistency | Immediate response is easier to reason about | Explicit eventual consistency |
| Operations | Familiar request telemetry | Broker, dead letters, replay, idempotency |
| Observability | Trace one request chain | Correlate publish, consume, retry, and outcome |

## Decision

Publish an `OrderPaid` event after durable payment state is recorded. Fulfillment consumes it idempotently. Checkout returns after successful publication through an outbox-backed path. A synchronous query remains available for status reads; it is not part of command execution.

## Consequences

Checkout no longer waits for fulfillment and short outages are buffered. The platform must operate the broker, schema evolution, idempotency keys, retry limits, dead-letter handling, and end-to-end correlation. Product behavior must communicate “processing” honestly.

## Risks and limits

Duplicate or out-of-order delivery can corrupt state if consumers are not idempotent. A broker is unjustified for a small system without the operational capacity to support it. This decision does not mandate asynchronous communication for every service interaction.

## When Extended fits

The choice crosses team and service boundaries and introduces failure modes that are easy to hide in a short record. Extended makes criteria, rejected options, and operational ownership reviewable.
