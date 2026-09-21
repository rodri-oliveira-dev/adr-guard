# GitHub Action consumer guide

> **Publication status:** the reusable Action and compatibility tag `v1` are published. Examples using `rodri-oliveira-dev/adr-guard@v1` below are ready to use. The Marketplace listing is tracked separately and remains forthcoming until it is manually published and verified.

ADR Guard's composite Action runs the published ADR Guard container. Consumers do not need the .NET SDK, but they do need a Linux runner with Docker and must check out the repository first.

## Inputs

| Input | Default | Accepted values |
| --- | --- | --- |
| `path` | `docs/adr` | Repository-relative ADR directory inside `GITHUB_WORKSPACE`. Absolute paths, missing directories, `..` traversal, and symlink escapes are rejected. |
| `command` | `check` | `check` or `index`. |
| `version` | empty | Optional exact runtime image version in `X.Y.Z` or `vX.Y.Z` form. Required when the Action source is pinned by commit SHA or branch. |

The CLI exit contract is preserved: `0` success, `1` ADR validation failure, `2` usage/input error, and `3` operational error.

## Pull-request validation

**Published `@v1` example:**

```yaml
name: ADR validation

on:
  pull_request:
    branches:
      - main

permissions:
  contents: read

jobs:
  adr-guard:
    name: ADR Guard
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v7
        with:
          persist-credentials: false

      - name: Validate ADRs
        uses: rodri-oliveira-dev/adr-guard@v1
        with:
          path: docs/adr
          command: check
```

A file copy of this workflow lives at [examples/github-action-pr.yml](examples/github-action-pr.yml).

## Main-branch validation

Use the same read-only validation after merges to `main`:

```yaml
name: ADR validation

on:
  push:
    branches:
      - main

permissions:
  contents: read

jobs:
  adr-guard:
    name: ADR Guard
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v7
        with:
          persist-credentials: false

      - name: Validate ADRs
        uses: rodri-oliveira-dev/adr-guard@v1
        with:
          path: docs/adr
          command: check
```

A file copy of this workflow lives at [examples/github-action-main.yml](examples/github-action-main.yml).

## Validation output and annotations

Validation failures keep the CLI diagnostics in the raw log. Recognized `ADR001`–`ADR009` diagnostics are converted into escaped GitHub file annotations when the diagnostic path can be verified inside the selected ADR directory. The CLI does not provide reliable line numbers, so the Action does not invent them.

The Action also writes a compact `GITHUB_STEP_SUMMARY` with the outcome, exit code, total diagnostics, and per-rule counts. At most 50 file annotations are emitted per run; the raw log retains all diagnostics.

## `check` versus `index`

`check` is read-only: the complete checkout is mounted read-only.

`index` intentionally writes the generated `README.md` inside the selected ADR directory. The rest of the checkout remains read-only. A common CI pattern is:

```yaml
- name: Generate ADR index
  uses: rodri-oliveira-dev/adr-guard@v1
  with:
    path: docs/adr
    command: index

- name: Ensure generated index is committed
  run: git diff --exit-code -- docs/adr/README.md
```

The `@v1` reference above uses the published moving major compatibility tag.

## Version pinning

Choose the pinning mode according to your update policy:

- `@v1.2.3`: immutable Action source for that exact release and exact runtime image `:1.2.3`.
- `@v1`: moving compatibility reference that follows newer successful `v1.x.y` releases and runtime image `:1`.
- `@<commit-sha>`: immutable Action source; specify `version: 1.2.3` explicitly because a commit SHA does not encode the runtime image version.

There is never an implicit fallback to `latest`.

See [GitHub Action release policy](github-action-release.md) and [security model](github-action-security.md).

## Permissions and required checks

The Action itself only needs repository contents to have been checked out. A validation workflow can use:

```yaml
permissions:
  contents: read
```

To make ADR validation mandatory before merge, first run the workflow at least once so GitHub knows the check name. Then configure the repository's branch rules or ruleset for `main` and require the status check produced by the `ADR Guard` job. Keep the job name stable so the required-check rule continues to match.

## Troubleshooting

If the Action reports exit code `2`, check the `path`, `command`, and version/ref combination. Paths must remain inside the checkout.

Exit code `3` means an operational failure such as Docker being unavailable or the selected image being unavailable. The supported environment is a Linux runner with a working Docker daemon.

For `index`, the runner must be non-root because the Action deliberately refuses to execute the writable container as UID 0.

If a SHA or branch ref is used, provide an exact `version` input. When `@v1` is used, the Action source follows the published `v1` compatibility tag and resolves the matching `:1` runtime image.

## Releases and Marketplace

Release notes are published on the repository's [Releases](https://github.com/rodri-oliveira-dev/adr-guard/releases) page.

**Marketplace listing: forthcoming.** The `v1` compatibility tag is published. Independent pre-release consumer verification has passed and is recorded in [external verification evidence](github-action-external-verification.md); production Marketplace verification remains pending until the external consumer workflows are rerun against the published `@v1` and the listing is manually published and verified. Publication prerequisites, the proposed listing identity, and the owner-only web UI gates are tracked in the [Marketplace publication checklist](github-marketplace.md). For support and vulnerability reporting, see [../SUPPORT.md](../SUPPORT.md) and [../SECURITY.md](../SECURITY.md).
