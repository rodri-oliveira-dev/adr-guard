# GitHub Action release policy

ADR Guard publishes its GitHub Action from the same validated commit and release version used for the .NET Tool and container images.

## Published Action references

Each successful release publishes two Action references:

- `vMAJOR.MINOR.PATCH` — immutable. It is created once and must always point to the validated commit for that exact release.
- `vMAJOR` — movable compatibility reference. It advances to the newest successful release in that major line and never moves backwards during a rerun of an older release.

Examples:

```yaml
# Reproducible Action source and runtime version.
uses: rodri-oliveira-dev/adr-guard@v1.2.3

# Receive compatible updates inside major version 1.
uses: rodri-oliveira-dev/adr-guard@v1
```

The Action never falls back to `latest`. An exact Action tag selects the matching exact GHCR image tag (`@v1.2.3` -> `:1.2.3`). A moving major Action tag selects the matching moving major image tag (`@v1` -> `:1`).

A commit SHA can also pin the Action source immutably, but the SHA does not encode a container version. Therefore SHA pinning must include an explicit exact `version` input:

```yaml
uses: rodri-oliveira-dev/adr-guard@<commit-sha>
with:
  path: docs/adr
  command: check
  version: 1.2.3
```

## Release ordering

The post-CI release workflow preserves the validated commit from the successful `CI` run and performs the release in this order:

1. build, test, and package the validated commit;
2. reserve the resolved SemVer with an internal `release-reservation/vMAJOR.MINOR.PATCH` tag tied to that validated commit;
3. publish the .NET Tool to NuGet.org;
4. publish the package to GitHub Packages;
5. publish the multi-platform container to GHCR and Docker Hub, including exact, minor, major, and `latest` image tags plus SBOM/provenance attestations;
6. verify the exact and major GHCR image references resolve to the OCI digest produced by that release;
7. smoke-test Action runtime resolution for both the exact and major references;
8. create or verify the immutable `vMAJOR.MINOR.PATCH` Git tag, update `vMAJOR`, and remove the completed reservation;
9. create the GitHub Release.

The Action tags are intentionally published after the container job succeeds. A failed container publication therefore cannot expose a new Action tag whose runtime artifact is missing. The internal reservation tag is not a supported Action reference and prevents a later commit from reusing a SemVer that may already have partially published artifacts.

## Idempotency and conflicts

The exact SemVer tag is immutable. On a rerun:

- if `vMAJOR.MINOR.PATCH` already points to the validated commit, it is reused;
- if it points anywhere else, the release fails instead of repointing it;
- if `vMAJOR` already points to the same release, no update occurs;
- if `vMAJOR` points to an older release in the same major line, it advances;
- if an older release workflow is rerun after a newer release, `vMAJOR` remains on the newer release;
- if the existing major tag cannot be traced to an immutable release tag, the workflow refuses to overwrite it.

An existing GitHub Release is also treated as immutable. A missing package asset may be completed, but an existing asset is not overwritten with `--clobber`.

## Verify a release

Given release `v1.2.3`, first confirm the Git tags:

```bash
git ls-remote --tags https://github.com/rodri-oliveira-dev/adr-guard.git \
  refs/tags/v1.2.3 refs/tags/v1
```

Both should resolve to the same commit immediately after the release. The exact tag must continue pointing to that commit forever; the major tag may advance on later `v1.x.y` releases.

Verify the runtime images:

```bash
docker buildx imagetools inspect ghcr.io/rodri-oliveira-dev/adr-guard:1.2.3
docker buildx imagetools inspect ghcr.io/rodri-oliveira-dev/adr-guard:1
```

Immediately after publication, both references should report the same top-level OCI digest. Later releases may move `:1` while `:1.2.3` remains immutable.

Finally, validate the Action from a consumer repository:

```yaml
permissions:
  contents: read

steps:
  - uses: actions/checkout@<pinned-commit>
    with:
      persist-credentials: false

  - uses: rodri-oliveira-dev/adr-guard@v1.2.3
    with:
      path: docs/adr
      command: check
```

For supply-chain and runtime isolation details, see [GitHub Action security model](github-action-security.md) and [container image and supply-chain guide](container.md).
