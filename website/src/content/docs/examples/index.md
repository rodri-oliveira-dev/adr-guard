---
title: ADR examples
description: Complete engineering decisions with context, alternatives, criteria, consequences, risks, and traceable evolution.
sidebar:
  order: 1
---

These examples are learning material, not universal recommendations. Each scenario keeps the problem and reasoning concrete while showing how format depth should follow decision impact.

| Scenario | What it teaches | Suggested depth |
| --- | --- | --- |
| [Distributed caching with Redis](./redis-cache/) | The same reasoning in Minimal, Extended, and MADR 4.0 | Compare all three |
| [Synchronous vs asynchronous communication](./service-communication/) | Coupling, resilience, latency, consistency, operations | Extended |
| [Database technology selection](./database-selection/) | Weighted criteria and operational ownership | Extended |
| [Authentication and authorization](./authentication/) | Security requirements, risks, and responsibility boundaries | Extended |
| [ADR supersession](./supersession/) | Evolving a decision without erasing history | Minimal pair with explicit relationships |

Examples declared compatible with ADR Guard are also maintained as source fixtures under `docs/examples/` and checked by the repository’s documentation tests. The portal adds explanation; the source Markdown remains authoritative.
