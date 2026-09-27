# GitHub Action AI review

AI-assisted ADR review is an explicit opt-in extension of the reusable ADR Guard Action. The default remains deterministic `check`; enabling `review` does not change existing validation workflows.

> **Availability:** `command: review` is implemented and CI-validated in PR #85 on this branch. The already-published remote `rodri-oliveira-dev/adr-guard@v1` still exposes `check`/`index` only until the release after PR #85 is merged advances the `v1` tag. The `@v1` examples below are the post-release consumer form, not a claim that the current remote tag already supports review.

## Trust boundary

Use provider-backed review only in trusted workflows. Review requires Linux, Docker, and Python 3 on the runner because safe summary/annotation rendering is part of the successful review contract. The Action enforces the following boundaries:

- `pull_request_target` is rejected for `review`;
- fork `pull_request` events are rejected before Docker/provider execution;
- only `push`, `workflow_dispatch`, `schedule`, and same-repository `pull_request` events may opt in; all other event types are rejected before Docker/provider execution;
- the checkout is mounted read-only for `review`;
- `GITHUB_TOKEN` and `GH_TOKEN` are never forwarded to the review container;
- only the credential environment variable associated with the selected provider is forwarded, and only by variable name;
- no PR comments or write-token operations are performed;
- workflows need only `permissions: contents: read`.

For untrusted contributions, run deterministic `check` on the pull request and perform provider-backed review after merge or from a separately trusted/manual workflow.

## Inputs

Set `command: review` and provide:

| Input | Required | Default | Purpose |
| --- | --- | --- | --- |
| `review-target` | yes | empty | Repository-relative ADR Markdown file. |
| `provider` | yes | empty | `openai`, `anthropic`, `gemini`, or `openai-compatible`. |
| `model` | yes | empty | Provider model identifier. |
| `endpoint` | only for compatible providers when required by the CLI | empty | Explicit OpenAI-compatible endpoint. |
| `context-files` | no | empty | Newline-delimited repository-relative `.md`/`.txt` files. |
| `include-existing-adrs` | no | `false` | Opt in to bounded existing-ADR context. |
| `policy` | no | `advisory` | `advisory` or deterministic `enforce`. |
| `policy-file` | with `policy: enforce` | empty | Repository-relative deterministic policy JSON. |

Provider credentials are not Action inputs. Supply the selected credential as an environment variable on the Action step, for example `OPENAI_API_KEY: ${{ secrets.OPENAI_API_KEY }}`.

## Trusted example

```yaml
name: Trusted ADR AI review

on:
  workflow_dispatch:

permissions:
  contents: read

jobs:
  review:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v7
        with:
          persist-credentials: false

      - name: Review selected ADR
        uses: rodri-oliveira-dev/adr-guard@v1
        env:
          OPENAI_API_KEY: ${{ secrets.OPENAI_API_KEY }}
        with:
          command: review
          review-target: docs/adr/0007-cache-strategy.md
          provider: openai
          model: gpt-5
          context-files: |
            docs/architecture/security.md
            docs/architecture/availability.txt
          policy: advisory
```

A same-repository `pull_request` may also run review when the workflow intentionally provides a provider credential. Fork PRs are blocked even if the workflow is misconfigured to expose review inputs.

## Network and filesystem behavior

`check` and `index` preserve the existing `--network=none` behavior. Provider-backed `review` requires outbound network access, so only that command runs without the Docker `--network=none` restriction.

The review container root filesystem and entire checkout remain read-only. Review does not write ADRs, status changes, indexes, or report files.

## Reporting and exit behavior

Successful review is requested from the CLI as versioned JSON and converted into a `GITHUB_STEP_SUMMARY`.

Follow-up findings are emitted as GitHub `warning` annotations only when their evidence source maps to a verified selected local file. The Action does not invent line numbers. Findings without a verified local path, missing context, and uncertainty remain advisory summary text.

The Action preserves the CLI exit contract:

- `0`: review completed; AI findings remain advisory;
- `1`: selected ADR failed deterministic structural validation;
- `2`: invalid Action/CLI configuration;
- `3`: provider, transport, cancellation, or operational failure;
- `4`: explicit deterministic policy enforcement failed.

Exit code `4` is distinct from model findings. A provider failure never becomes a clean-review result.

## Pull-request reporting

This first iteration intentionally uses step summaries and file annotations only. It does not create or update PR comments and therefore does not require `pull-requests: write` or another write permission.
