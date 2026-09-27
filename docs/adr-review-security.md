# ADR review security and trust boundaries

AI-assisted ADR review processes repository documentation through an external model. Treat every ADR, context file, provider response, and fork contribution as untrusted input.

## Execution boundary

`adr-guard review` does not expose tools to the model. Reviewed material cannot trigger:

- arbitrary filesystem reads;
- URL or network fetches;
- external commands or shell execution;
- ADR/index/status changes;
- file writes other than an explicitly requested local review report;
- secret or environment-variable access.

Source material is serialized into a JSON data envelope with explicit capability flags set to `false`. Instructions embedded in an ADR or selected context remain inert data and do not modify the review contract or deterministic policy evaluation.

Deterministic enforcement from `--policy enforce` is evaluated locally before provider invocation. Model wording cannot bypass, create, or satisfy policy rules.

## Credentials and redaction

Provider credentials are read only from environment variables:

- `OPENAI_API_KEY`
- `ANTHROPIC_API_KEY`
- `GEMINI_API_KEY`
- `ADR_GUARD_OPENAI_COMPATIBLE_API_KEY`

For defensive redaction, `GITHUB_TOKEN` and `GH_TOKEN` are also treated as sensitive when present.

Credential values are removed from selected source content before the provider context is built and from provider-derived findings, diagnostics, and persisted reports. Credentials are never intentionally placed in provider request bodies. Authentication headers remain transport metadata required by the selected provider.

Official providers use fixed HTTPS endpoints. OpenAI-compatible remote endpoints require HTTPS. Plain HTTP is allowed only for unauthenticated loopback usage; any endpoint carrying an API key must use HTTPS, including loopback.

## Provider output

Provider responses are untrusted. ADR Guard:

- limits successful HTTP responses to 1 MiB;
- rejects malformed or incomplete review JSON;
- rejects unexpected JSON properties;
- limits the number of findings;
- bounds source, excerpt, explanation, and guidance fields;
- normalizes control/newline characters before report rendering;
- neutralizes `::` workflow-command delimiters in provider-derived text;
- never turns provider failure, refusal, timeout, rate limiting, or malformed output into “no issues” or an approval.

Timeouts and caller cancellation remain distinct. Transport/provider failures use controlled diagnostics that do not include response bodies, authorization headers, or raw network exception details.

## Context disclosure and external processing

By default, review sends only the selected ADR. Additional context files require explicit `--context-file`; existing ADR discovery requires `--include-existing-adrs`. Existing ADR content and the final composed context remain bounded.

Selected material is transmitted to the configured external provider and may leave the local machine, organization, or region. Provider retention, logging, data residency, training, and processing terms are controlled by that provider. Do not send sensitive architectural material unless those terms and organizational policies permit it.

Local source disclosure lists which selected source filenames are sent. ADR Guard does not silently scan source code, git history, environment variables as context, arbitrary URLs, or unrelated repository files.

## Community and fork pull requests

Do not expose provider credentials to untrusted fork code or workflows. For future GitHub Action review integration:

- preserve GitHub's default behavior that withholds repository secrets from fork pull requests;
- do not add secrets merely to make an untrusted fork review run;
- do not use `pull_request_target` to check out and execute untrusted PR content with privileged secrets;
- prefer trusted/manual approval boundaries before any provider-backed review that requires credentials;
- keep review advisory unless an explicit deterministic local policy is configured.

A provider-backed review is guidance for a human reviewer, not a security certification or architectural approval.
