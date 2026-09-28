# AI-assisted ADR technical review

[Português (Brasil)](adr-review.pt-BR.md)

`adr-guard review` performs a read-only, evidence-oriented technical review of one existing, structurally valid ADR through an explicitly selected AI provider. It is architectural assistance for a human reviewer, not proof that a decision is correct, not a formal security/compliance certification, and not an automated approval or rejection.

## Version availability

The CLI and the GitHub Action have separate release histories.

| Capability | First published CLI release | Status |
| --- | --- | --- |
| Base `adr-guard review`, eight dimensions, bounded context, versioned reports | `v1.1.2` | Published |
| Deterministic advisory/enforce policy v1 | `v1.1.3` | Published |
| Review trust-boundary hardening and deterministic mock-provider regression matrix | `v1.1.4` | Published |
| Current published CLI package/image | `v1.1.6` | Includes the capabilities above |
| Reusable Action `command: review` | `v1.1.6` | Published in the moving `@v1` Action as an explicit opt-in command |

The published `@v1` Action supports `check`, `index`, and opt-in `review`. The Marketplace listing is still tracked separately; this documentation does not claim that it is live.

## Minimal CLI usage

```bash
adr-guard review docs/adr/0007-cache-strategy.md \
  --provider openai \
  --model <openai-model>
```

The selected ADR is structurally validated before provider invocation. The command is read-only: it does not edit the ADR, change status, regenerate the index, alter git state, or approve/reject the decision.

### Providers, models, and authentication

ADR Guard never chooses a model automatically. `--provider` and `--model` are required.

| Provider | CLI value | Credential environment variable | Endpoint |
| --- | --- | --- | --- |
| OpenAI | `openai` | `OPENAI_API_KEY` | Official endpoint; custom `--endpoint` rejected |
| Anthropic | `anthropic` | `ANTHROPIC_API_KEY` | Official endpoint; custom `--endpoint` rejected |
| Gemini | `gemini` | `GEMINI_API_KEY` | Official endpoint; custom `--endpoint` rejected |
| OpenAI-compatible | `openai-compatible` | `ADR_GUARD_OPENAI_COMPATIBLE_API_KEY` (optional) | `--endpoint <uri>` required |

Authentication is read from environment variables, not CLI arguments. Remote OpenAI-compatible endpoints require HTTPS. Plain HTTP is allowed only for unauthenticated loopback endpoints; if an API key is configured, HTTPS is required even on loopback.

Provider/model availability, billing, quotas and rate limits are controlled by the provider. ADR Guard does not estimate precise token cost.

## Review inputs

```text
adr-guard review <adr-file>
  --provider <provider>
  --model <model>
  [--endpoint <uri>]
  [--context-file <path>]...
  [--include-existing-adrs]
  [--policy advisory|enforce]
  [--policy-file <path>]
  [--format text|json]
  [--output <path> [--overwrite]]
```

| Option | Behavior |
| --- | --- |
| `<adr-file>` | Required target ADR. Only its filename, not its absolute local path, is exposed to the model. |
| `--context-file <path>` | Explicit UTF-8 `.md` or `.txt` file; repeatable and never discovered automatically. |
| `--include-existing-adrs` | Explicitly allows bounded parsed ADR discovery below the target ADR directory for cross-ADR context. |
| `--policy advisory\|enforce` | Defaults to `advisory`. Only deterministic local policy rules can enforce. |
| `--policy-file <path>` | Strict schema `1.0` local JSON policy. It is not sent to the provider. Required for `enforce`. |
| `--format text\|json` | Defaults to text. JSON emits one schema-versioned object to stdout. |
| `--output <path>` | Optional report persistence. Text requires `.md`/`.txt`; JSON requires `.json`. |
| `--overwrite` | Allows atomic replacement of an existing report only; it never permits replacing the reviewed ADR or ADR index. |

## Context selection, limits, and privacy

By default, **only the selected ADR** is sent to the configured external provider. Nothing else is silently discovered.

Additional material is opt-in:

```bash
adr-guard review docs/adr/0007-cache-strategy.md \
  --provider anthropic \
  --model <anthropic-model> \
  --context-file docs/architecture/security.md \
  --context-file docs/architecture/availability.txt \
  --include-existing-adrs
```

Review context limits are deterministic:

| Source | Maximum |
| --- | ---: |
| Each explicit `--context-file` | 50,000 characters and 150,000 bytes |
| All explicit context files combined | 100,000 characters and 300,000 bytes |
| Parsed existing ADR context | 12,000 characters |
| Final composed review context | 120,000 characters |

Explicit context accepts only valid UTF-8 `.md`/`.txt` content. UTF-16, UTF-32, invalid UTF-8 and binary/NUL content are rejected. Files over a limit are rejected rather than silently truncated.

With `--include-existing-adrs`, ADR Guard may inspect Markdown ADRs below the target ADR directory to build bounded parsed context and cross-ADR evidence. It does not scan source code, git history, arbitrary URLs, unrelated repository files, or environment variables as architectural context.

Before provider invocation, selected sources are disclosed locally. Provider context uses source IDs and filenames instead of absolute local filesystem paths.

**Third-party processing:** the selected ADR, opted-in context files, and opted-in existing ADR material are transmitted to the configured provider and can leave your machine, organization or region. Provider retention, logging, data residency, training and processing terms apply. If that is not acceptable, do not invoke `review`; continue using the offline/deterministic `check`, `index`, and `new` workflows.

Credential redaction is defense in depth, not a data-loss-prevention system. Exact non-empty literal values of known provider/GitHub credential variables are replaced when encountered, but encoded values, partial matches and secrets from unrelated variables are not guaranteed to be detected.

## Eight review dimensions

Every valid provider response must cover exactly these dimensions:

1. `clarity-and-rationale`
2. `considered-alternatives`
3. `nonfunctional-requirements`
4. `risks-and-consequences`
5. `architectural-consistency`
6. `security-and-compliance`
7. `implementation-and-operational-feasibility`
8. `measurable-verification-criteria`

The model must not invent workloads, SLAs, measurements, infrastructure, budgets, legal obligations or source documents.

## Evidence classifications and uncertainty

Findings use five classifications:

| Classification | Meaning | CI effect |
| --- | --- | --- |
| `observed-evidence` | The selected material directly supports the observation. | Advisory only |
| `potential-risk` | Evidence suggests a risk, but human confirmation is required. | Advisory only |
| `missing-context` | Required facts are absent; explanation must explicitly state “not enough information”. | Advisory only |
| `recommendation-for-human-investigation` | The selected material warrants a human follow-up. | Advisory only |
| `not-applicable` | Selected evidence supports that the dimension does not apply. | Advisory only |

Follow-up priorities are derived locally: `missing-context` is `required`; risks and human-investigation recommendations are `recommended`. Report uncertainty is also explicit: `not-enough-information`, `potential`, or `requires-human-investigation`.

A provider finding is never converted into an objective policy violation just because it uses severe wording such as “critical” or “high”.

## Advisory versus deterministic enforcement

The default is:

```bash
adr-guard review docs/adr/0007-cache-strategy.md \
  --provider openai \
  --model <openai-model> \
  --policy advisory
```

In advisory mode, deterministic policy diagnostics and all model findings remain non-gating.

To enforce an objective local rule:

```bash
adr-guard review docs/adr/0007-cache-strategy.md \
  --provider openai \
  --model <openai-model> \
  --policy enforce \
  --policy-file docs/adr/review-policy.json
```

Policy schema `1.0` supports:

- `required-section-content`: a named level-two Markdown section must contain non-whitespace content;
- `required-context-file`: a relative `.md`/`.txt` file must exist relative to the policy file and must also be explicitly selected with `--context-file`.

Enforcement runs locally **before provider construction/invocation**. A violation exits `4`. A passing policy still does not mean that the architecture is approved.

See [ADR review policy v1](adr-review-policy-v1.md).

## Reports and JSON schema

Text is the default:

```bash
adr-guard review docs/adr/0007-cache-strategy.md \
  --provider gemini \
  --model <gemini-model>
```

Versioned JSON is available for tooling:

```bash
adr-guard review docs/adr/0007-cache-strategy.md \
  --provider gemini \
  --model <gemini-model> \
  --format json \
  --output artifacts/adr-review.json
```

JSON report schema is currently `1.0` and uses stable camelCase field names. It contains target/reviewer/provider metadata, selected input scope, all eight dimensions, actionable follow-up findings, limitations and the provider-cost caveat. In JSON mode, pre-provider source disclosure goes to stderr so stdout remains parseable JSON.

### Human-readable report example

Abridged example:

```markdown
# ADR Technical Review

Schema version: 1.0
Target: 0007-cache-strategy.md (ADR 0007)
Provider: openai / <openai-model>
Outcome: needs-context

> Advisory AI-assisted review only. Human review remains authoritative; this report does not approve, reject, certify, or change the ADR.

## Analyzed dimensions

### clarity-and-rationale
- Classification: observed-evidence
  Evidence: [target] 0007-cache-strategy.md
  Explanation: The decision and stated rationale are present.
  Guidance: Verify that the rationale matches the actual workload.

### security-and-compliance
- Classification: missing-context
  Follow-up priority: required
  Uncertainty: not-enough-information
  Evidence: not available from verified selected sources.
  Explanation: not enough information to determine applicable security or compliance constraints.
  Guidance: Provide the relevant security/compliance requirements.

## Follow-up findings
- security-and-compliance [missing-context] — required
  not enough information to determine applicable security or compliance constraints.
  Guidance: Provide the relevant security/compliance requirements.
```

“No follow-up findings” is explicitly **not** an approval.

## Exit codes

| Code | Meaning for `review` |
| ---: | --- |
| `0` | Review completed. Model findings can still require human follow-up. |
| `1` | Selected ADR failed deterministic structural validation; provider is not called. |
| `2` | Invalid CLI/provider/policy configuration. |
| `3` | Operational, provider, transport, cancellation, malformed-response or report failure. Never interpreted as a clean review. |
| `4` | One or more named deterministic enforcement rules failed; provider is not called. |

## GitHub Action and CI governance

The Action integration implemented in PR #85 keeps `check` as the default and makes `review` explicit.

A trusted workflow uses only:

```yaml
permissions:
  contents: read
```

and may opt in after the corresponding Action release is published:

```yaml
- name: Review selected ADR
  uses: rodri-oliveira-dev/adr-guard@v1
  env:
    OPENAI_API_KEY: ${{ secrets.OPENAI_API_KEY }}
  with:
    command: review
    review-target: docs/adr/0007-cache-strategy.md
    provider: openai
    model: <openai-model>
    policy: advisory
```

The Action:

- accepts provider-backed review only on `push`, `workflow_dispatch`, `schedule`, and same-repository `pull_request`;
- rejects fork pull requests, `pull_request_target`, and all other event types before Docker/provider execution;
- never forwards `GITHUB_TOKEN` or `GH_TOKEN` to the review container;
- mounts the checkout read-only;
- uses outbound network only for provider-backed `review`; deterministic `check`/`index` retain `--network=none`;
- emits AI findings as `warning` annotations only for verified selected local evidence paths;
- does not invent line numbers;
- writes a structured `GITHUB_STEP_SUMMARY`;
- does not create PR comments and needs no `pull-requests: write`.

For community/fork contributions, run deterministic `check` on the untrusted PR and perform provider-backed review only after merge or from a separately trusted/manual workflow.

See [GitHub Action AI review](github-action-review.md) and [GitHub Action security model](github-action-security.md).

## Security, cost, and provider failure behavior

ADR/context material and provider output are untrusted. Embedded instructions, URLs, shell commands or requests for secrets are treated as inert source data. The review contract exposes no model-controlled filesystem, network-fetch, command-execution, file-write, ADR-status-change or secret-access capability.

Provider output is schema checked and bounded. Malformed, incomplete, oversized, timed-out, rate-limited or otherwise failed responses return an operational error; ADR Guard never converts them into “no issues”.

Persisted reports and CI artifacts can contain evidence excerpts and architectural observations. Protect them according to the same data-classification rules as the ADRs they review.

See [review security and trust boundaries](adr-review-security.md).

## Troubleshooting

### “needs-context” or many `missing-context` findings

The provider is saying the selected material is insufficient, not that the ADR failed. Add only the specific `--context-file` material you are allowed to share, or opt into `--include-existing-adrs` when cross-ADR context is appropriate. If missing evidence must be a CI requirement, encode it as a deterministic policy rule.

### Potential contradiction with another ADR

Cross-ADR analysis runs only when existing ADR context is explicitly enabled. Active decisions with an observable mismatch can be reported as a potential risk, but scope and chronology still require human confirmation. Deprecated and Superseded ADRs are historical context and do not create a current conflict solely because they differ.

### Provider timeout, rate limit, unavailable endpoint, or cancellation

These are exit `3` operational failures. Retry according to provider policy or investigate endpoint/connectivity/quota. Do not treat the failed run as a clean review.

### Malformed or incomplete provider response

The response is rejected when it violates the strict JSON contract, omits one of the eight dimensions, uses an unsupported classification, exceeds field/finding limits, or does not express uncertainty for `missing-context`. The result is exit `3`, never an approval.

### Policy exits `4` before the provider is called

That is expected for `--policy enforce`: local deterministic rules are evaluated first. Fix the named objective rule/evidence or intentionally change the policy configuration; do not ask the model to override it.

## Human ownership

ADR Guard can organize evidence, surface gaps and suggest follow-up questions. It cannot establish architectural correctness, certify security/compliance, determine organizational acceptance, or replace the accountable human reviewer. Final ADR status and governance decisions remain with people.
