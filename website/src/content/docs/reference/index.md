---
title: Contracts and reference
description: Versioned report schemas, security boundaries, Action behavior, and maintenance contracts.
sidebar:
  order: 1
---

Reference pages describe contracts and operational boundaries rather than the learning path.

- [Implementation-to-documentation traceability](./generated/documentation-audit/)
- [AI review policy v1](./generated/review-policy-v1/)
- [GitHub Action security](./generated/github-action-security/)
- [GitHub Action review mode](./generated/github-action-review/)
- [GitHub Action release process](./generated/github-action-release/)
- [External verification](./generated/github-action-external-verification/)

The [AI review report v1 contract](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/docs/adr-review-report-v1.md) and JSON schemas remain versioned in the repository. Treat the source and installed CLI version as authoritative when integrating structured output.

## Architecture impact contracts

- [ADR-to-code mapping manifest v1 schema](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/docs/schemas/adr-impact-map-v1.schema.json)
- [Architecture impact report v1 schema](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/docs/schemas/adr-impact-report-v1.schema.json)
- [Architecture Impact Analysis guide](../product/architecture-impact/)

## Architectural drift contracts

- [Fitness rules v1 contract](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/docs/fitness-rules-v1.md) and [schema](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/docs/schemas/adr-fitness-rules-v1.schema.json)
- [External fitness evidence v1 schema](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/docs/schemas/adr-fitness-evidence-v1.schema.json)
- [Drift snapshot/baseline v1 schema](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/docs/schemas/adr-drift-snapshot-v1.schema.json)
- [Drift report v1 schema](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/docs/schemas/adr-drift-report-v1.schema.json)
- [Architectural Drift guide](../product/architectural-drift/)

These contracts are distinct. Impact reports identify review candidates from explicit file mappings; drift reports evaluate declared fitness rules from bounded evidence. Neither contract records human approval.
