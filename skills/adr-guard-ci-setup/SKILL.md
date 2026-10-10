---
name: adr-guard-ci-setup
description: Configure read-only Architecture Decision Record validation with ADR Guard in GitHub Actions. Use when adding ADR checks to pull requests, indexing policy, JSON/SARIF reporting, or troubleshooting ADR validation CI.
license: MIT
compatibility: Requires a GitHub Actions repository; ADR Guard's published Action runs in Linux with Docker.
---

# Set up ADR Guard CI

Prefer a **read-only deterministic gate**. An AI-based review is a separate, explicit and optional workflow.

## Workflow

1. Inspect existing workflows, the ADR directory, selected format, branch protection, and runner capabilities before editing.
2. Establish a minimum-permission PR check with checkout and the published Action. Review and pin third-party Action revisions in production; `@v1` is a **moving** ADR Guard compatibility tag.

   ```yaml
   name: ADR validation
   on:
     pull_request:
       branches: [main]
   permissions:
     contents: read
   jobs:
     adr-guard:
       name: ADR Guard
       runs-on: ubuntu-latest
       steps:
         - uses: actions/checkout@v7
           with:
             persist-credentials: false
         - uses: rodri-oliveira-dev/adr-guard@v1
           with:
             path: docs/adr
             command: check
   ```

3. Do not assume the target path exists: validate the configured directory and the runner's Docker availability. For MADR use the documented repository configuration; do not mix formats.
4. Run the workflow on a PR. Distinguish actual validation failures from missing runtimes or configuration errors.
5. If required checks are wanted, configure branch rules only after the job has run and its stable check name exists. Do not change repository protection without authorization.
6. If a committed generated index must be kept fresh, run `command: index` only in an explicitly writable index step, then `git diff --exit-code -- docs/adr/README.md`. Do not commit index output from CI automatically.
7. Optional SARIF: `adr-guard check docs/adr --format sarif` emits a report; a separate explicitly trusted upload step/job needs `security-events: write`. Keep ordinary validation as `contents: read`.
8. Optional AI review: requires `command: review`, explicit provider/model, carefully scoped context, trusted events and credentials. Never use untrusted fork PRs or `pull_request_target` to transmit secrets to providers.

## Guardrails

- The Action `@v1` supports `check`, `index`, and opt-in `review`; it does **not** support `new` or `draft`.
- A source pinned by commit SHA requires an exact `version:` input for the runtime image. Prefer exact published release tags or reviewed SHA in controlled environments.
- Do not weaken permissions, skip failing checks, or claim CI validation approves a technical decision.

## Output

Give workflow path, trigger, least-privilege permissions, target directory, pinning strategy, observed run status and outstanding configuration/security decisions.

Product reference: [GitHub Action consumer guide](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/docs/github-action.md).
