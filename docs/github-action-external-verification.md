# External GitHub Action verification

This document records the independent-consumer evidence for ADR Guard issue #49.

## Status

**Pre-release external verification: passed.**

**Production Marketplace verification: pending.**

The technical Action implementation has been exercised from a repository other than `adr-guard`, but the final #49 acceptance criteria cannot be marked complete until this branch is merged, a real Action release/tag exists, the Marketplace listing is published by the repository owner, and the same external test is rerun using the published tag.

## Independent consumer

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

This is intentionally **not** presented as production adoption. A commit SHA plus explicit image version is used because `@v1` does not exist yet.

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

This also improves moving-major behavior on self-hosted runners because `@v1` will actively refresh image tag `:1` rather than silently using a stale local image.

## Final production verification gate

After this branch is merged and the release pipeline publishes the first production Action release:

1. verify the immutable release tag and moving `v1` tag exist;
2. verify the corresponding exact and major GHCR images are publicly pullable;
3. publish the Marketplace listing through the authorized owner flow described in [github-marketplace.md](github-marketplace.md);
4. record the real Marketplace URL and released Action ref in issue #49;
5. update the external consumer workflows from the pre-release SHA + `version: 0.1.12` to:
   `uses: rodri-oliveira-dev/adr-guard@v1`;
6. remove the explicit `version` input;
7. rerun both external workflows;
8. require the valid workflow to pass and the invalid workflow to fail with a file annotation;
9. verify the published major image/ref compatibility;
10. replace all "Marketplace forthcoming" notices with the verified listing URL;
11. only then close #49 and roadmap #50.

The final Marketplace URL and published Action reference must be copied from GitHub after publication; they must never be inferred or invented.
