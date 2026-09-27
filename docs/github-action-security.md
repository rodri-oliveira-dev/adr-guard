# GitHub Action security model

ADR Guard's reusable GitHub Action keeps deterministic ADR validation as its default, supports explicit index generation, and exposes AI-assisted `review` only as an opt-in operation. It invokes the published GHCR container and does not expose the AI-assisted `draft` command.

## Minimal permissions

A consumer workflow only needs repository read access to check out its files:

```yaml
permissions:
  contents: read

jobs:
  adr-guard:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@<pinned-commit>
        with:
          persist-credentials: false

      - uses: rodri-oliveira-dev/adr-guard@vX.Y.Z
        with:
          command: check
          path: docs/adr
```

ADR Guard does not call the GitHub API, request repository write access, or require `GITHUB_TOKEN` inside the container. The Action does not forward the runner environment into Docker.

## Registry access

The release image used by the Action is published at:

```text
ghcr.io/rodri-oliveira-dev/adr-guard
```

The public release image is expected to be anonymously pullable. CI verifies this with an empty Docker configuration and with GitHub/provider token variables removed from the pull process. If an organization routes container pulls through a private mirror or applies registry policy that requires authentication, authenticate Docker explicitly before invoking ADR Guard; the Action does not perform implicit registry login or consume a token input.

## Container sandbox

The Action starts the container with the following restrictions:

- read-only container root filesystem;
- all Linux capabilities dropped with `--cap-drop=ALL`;
- privilege escalation blocked with `no-new-privileges`;
- networking disabled with `--network=none` for deterministic `check` and `index`; provider-backed `review` alone uses outbound networking;
- no `--privileged` mode;
- no implicit environment-variable or secret forwarding; `review` may forward only the credential variable selected for its provider.

The published image declares a non-root runtime user. CI checks the published image metadata and rejects an empty, root, or UID 0 runtime user.

For `check` and `review`, the entire consumer workspace is mounted read-only. For `index`, the workspace remains read-only and only the validated ADR directory is over-mounted as writable. On Linux, `index` runs using the non-root host UID/GID so generated files retain consumer ownership. A root runner is rejected for writable `index` execution rather than running the container as root.

## Inputs and diagnostics

`check`, `index`, and explicit `review` are accepted. Paths are resolved inside `GITHUB_WORKSPACE`, traversal and symlink escapes are rejected, and inputs are passed as discrete process arguments rather than evaluated shell fragments. Review accepts only `push`, `workflow_dispatch`, `schedule`, and same-repository `pull_request`; all other event types, including `pull_request_target` and fork pull requests, are rejected before Docker/provider invocation.

CLI and provider output are treated as untrusted. Raw output is preserved for troubleshooting while GitHub workflow-command interpretation is suspended. Deterministic `ADR001`–`ADR009` diagnostics with verified paths become escaped error annotations. AI follow-up findings become escaped `warning` annotations only when evidence maps to a verified selected local file; the Action does not invent line numbers. Reporting cannot change the original CLI exit code.

## Secrets and AI providers

The default `check`/`index` path does not forward provider credentials. Opt-in `review` exposes provider/model selection but deliberately has no credential or token input: callers supply the provider credential through the Action step environment, and the wrapper forwards only the selected provider variable name to Docker. `GITHUB_TOKEN` and `GH_TOKEN` are never forwarded. Fork PRs and `pull_request_target` review are blocked before provider execution.

AI-assisted drafting remains a separate, explicit CLI/container workflow and is not exposed by the Action. See [GitHub Action AI review](github-action-review.md) for the trusted workflow contract.

## Version and digest trust

Use an exact Action release tag such as `@v1.2.3` for immutable source/runtime pairing, or the moving compatibility tag `@v1` to receive newer successful releases in major version 1. ADR Guard maps `@v1.2.3` to container tag `:1.2.3` and `@v1` to container tag `:1`; it never silently falls back to `latest`. When the Action is pinned by commit SHA, specify the exact `version` input. See the [GitHub Action release policy](github-action-release.md) for publication ordering and verification.

For environments that require immutable container identity, resolve the published image digest and use the direct container workflow documented in [container.md](container.md):

```text
ghcr.io/rodri-oliveira-dev/adr-guard@sha256:<digest>
```

CI also verifies that the exact published SemVer image resolves to a GHCR `sha256` repository digest. Release images are additionally protected by the existing SBOM, provenance, multi-platform manifest verification, Hadolint, smoke tests, Dependabot base-image updates, and Trivy scanning controls.

## Runtime prerequisites

The composite Action requires a Linux runner with a working Docker daemon. Opt-in `review` additionally requires Python 3 because safe `GITHUB_STEP_SUMMARY` and annotation rendering is part of the successful review contract and is validated before provider execution. It is designed for GitHub-hosted Ubuntu runners and compatible non-root Linux runners. Windows, macOS, Docker-less runners, review runners without Python 3, privileged-container requirements, and root execution for writable `index` are outside the supported contract.
