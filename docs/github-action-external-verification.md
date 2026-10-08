# External GitHub Action verification

This document records the independent-consumer evidence for ADR Guard issue #49.

## Status

**Independent pre-release repository verification: passed.**

**Published `@v1` compatibility verification in isolated consumer fixtures: continuously tested in ADR Guard CI.**

**Marketplace URL:** [https://github.com/marketplace/actions/adr-guard-architecture-decision-validator](https://github.com/marketplace/actions/adr-guard-architecture-decision-validator) was supplied by the repository owner on 2026-10-07; anonymous public accessibility still needs independent confirmation.

**Published `@v1` verification in an independent repository: passed on 2026-10-07.**

The `v1` compatibility tag is published and tracks the moving `v1` line; `review` joined that line in `v1.1.6`. The historical evidence below predates the initial `v1` release, while the new independent-repository rerun exercises the public `@v1` ref without a `version` override.

## Published `@v1` external consumer verification (2026-10-07)

Consumer: [`rodri-oliveira-dev/poc-arquitetura`](https://github.com/rodri-oliveira-dev/poc-arquitetura), isolated branch `test/adr-guard-action-49`, commit [`84ac7f4`](https://github.com/rodri-oliveira-dev/poc-arquitetura/commit/84ac7f436091314755294c8df66fcf7097d34382).

- [Valid workflow](https://github.com/rodri-oliveira-dev/poc-arquitetura/actions/runs/37707510792): **success**. Default `docs/adr` and custom `.adr-guard-consumer/custom` jobs succeeded; both `git diff --exit-code` read-only assertions passed.
- [Intentionally invalid workflow](https://github.com/rodri-oliveira-dev/poc-arquitetura/actions/runs/37707510728): **expected failure** (exit code `1`) with `ADR005` and a GitHub error annotation for `.adr-guard-consumer/invalid/0003-missing-decision.md`.
- Both workflows use `rodri-oliveira-dev/adr-guard@v1`, no explicit image `version`, and `permissions: contents: read`.
- Published runtime image `ghcr.io/rodri-oliveira-dev/adr-guard:1` was pulled successfully in the workflow logs.

This is external post-release evidence, not a claim that the Marketplace landing page has been independently checked while logged out.

## Historical pre-release independent consumer

Repository:

- https://github.com/rodri-oliveira-dev/poc-arquitetura

Isolated branch:

- `test/adr-guard-action-49`

The branch does not modify the consumer repository's `main`.

Consumer fixtures cover:

- default `docs/adr` path;
- custom `.adr-guard-consumer/custom` path;
- intentionally invalid `.adr-guard-consumer/invalid` path;
- `permissions: contents: read`;
- checkout with `persist-credentials: false`;
- read-only `check` behavior.

## Pre-release Action reference

The successful pre-release rerun pins Action source commit:

`d1d164b5b70014e8101f7843c7cde13d7a19ae9f`

and explicitly selects runtime image:

`ghcr.io/rodri-oliveira-dev/adr-guard:0.1.12`

This historical run is intentionally **not** presented as production `@v1` verification. It used a commit SHA plus an explicit image version because the `@v1` tag had not been published at the time. The tag is published now; the external consumer workflows still need to be rerun against it.

## Valid consumer evidence

Workflow:

- https://github.com/rodri-oliveira-dev/poc-arquitetura/actions/runs/35645607020

Result: **success**

Verified jobs:

- `ADR Guard default path` — validates `docs/adr` using the Action defaults.
- `ADR Guard custom path` — validates `.adr-guard-consumer/custom`.

Observed evidence:

- both jobs run with `GITHUB_TOKEN` permission `Contents: read`;
- both validate one compliant ADR successfully;
- the custom path is honored;
- each job runs `git diff --exit-code` after `check`, proving validation did not modify the selected ADR content.

## Invalid consumer evidence

Workflow:

- https://github.com/rodri-oliveira-dev/poc-arquitetura/actions/runs/35645606990

Result: **failure**, as intended.

Observed evidence:

- `GITHUB_TOKEN` permission is `Contents: read`;
- the invalid ADR omits the `Decision` section;
- ADR Guard emits `ADR005 ADR must define a non-empty 'Decision' section.`;
- validation exits with code `1`;
- GitHub renders the ADR diagnostic as an error annotation.

This verifies that an independent consumer receives the same validation contract and failure semantics as the ADR Guard repository.

## External-test bug found and fixed

The first invalid consumer run exposed a cold-run defect:

- https://github.com/rodri-oliveira-dev/poc-arquitetura/actions/runs/35645392420

The job failed correctly with `ADR005`, but no file annotation was emitted. On a clean runner, Docker automatically pulled the missing runtime image and wrote pull progress into the same captured stderr stream as ADR Guard diagnostics. The annotation parser correctly rejected that unexpected mixed output.

The Action was hardened to:

1. explicitly `docker pull` the selected image before CLI output capture;
2. fail with operational exit code `3` if that exact image cannot be pulled;
3. execute the validation container with `--pull=never`;
4. capture only ADR Guard stdout/stderr for diagnostic parsing.

The corrected external invalid run then emitted the expected `ADR005` annotation.

This also improves moving-major behavior on self-hosted runners because the published `@v1` actively refreshes image tag `:1` rather than silently using a stale local image.

## Remaining Marketplace closure gate

The owner supplied [the Marketplace URL](https://github.com/marketplace/actions/adr-guard-architecture-decision-validator) and the strict independent repository verification against `@v1` passed (runs above). The documentation now links to the supplied listing. Before closing issue #49 and then roadmap #50, confirm the listing can be accessed while logged out and contains the expected repository and version; record that confirmation in #49. A successful workflow alone does not validate the Marketplace landing page.
