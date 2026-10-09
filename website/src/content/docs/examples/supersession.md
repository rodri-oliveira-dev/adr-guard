---
title: Superseding an architecture decision
description: Replace a previously accepted database-per-service rule while preserving rationale and traceability.
sidebar:
  order: 6
---

## Original decision — ADR-0012

**Context:** Independent teams needed autonomous deployment and clear data ownership.

**Decision:** Every service owns a separate PostgreSQL database and exposes data only through APIs or events.

**Status:** Accepted.

This worked for core domains but imposed disproportionate cost on small internal services: separate backups, migrations, credentials, observability, and idle capacity.

## New evidence

After twelve months, three low-volume support services share the same team, release cadence, security boundary, and recovery objective. Incidents show more operational risk from maintaining separate instances than from logical co-location. Core business services still need independent failure and scaling boundaries.

## Superseding decision — ADR-0048

Permit approved low-risk internal services to share a managed PostgreSQL cluster while retaining separate databases, credentials, migrations, and backup verification. Core domain services remain isolated. An architecture review checks workload, compliance, blast radius, and exit plan before co-location.

**ADR-0048 status:** Accepted.<br />
**ADR-0012 status:** Superseded by ADR-0048.

## Consequences

Operational overhead and idle cost fall for qualifying services. The platform must enforce quotas, noisy-neighbor monitoring, credential isolation, and a migration path back to dedicated infrastructure. “Shared by default” is not implied.

## Traceability rules

1. Do not rewrite ADR-0012 as though the exception always existed.
2. Add explicit `Superseded` and `Superseded by` relationships supported by the canonical contract.
3. Link ADR-0048 back to ADR-0012 and explain the changed evidence.
4. Preserve both records in version control and the generated index.

## When this model fits

A short pair of records works when the original context is already complete and the new ADR focuses on changed evidence and scope. Use Extended when the replacement introduces multiple alternatives or broad risk.
