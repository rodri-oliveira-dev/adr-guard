# Explicit comparative AI review

> Availability: comparative AI-assisted review is published in [ADR Guard v1.3.0](https://github.com/rodri-oliveira-dev/adr-guard/releases/tag/v1.3.0), including the former v1.4 development scope; it remains opt-in and requires human oversight.

```bash
adr-guard review docs/adr/0007-cache-strategy.md \
  --compare-ref main \
  --provider openai \
  --model <explicit-model>
```

ADR Guard verifies the Git reference, reads the selected ADR's current file and the same repository-relative path at that one commit, and adds the prior version as `[comparison-base]`. It does not transmit commit history, diffs, commit messages, neighboring files, environment variables, or repository context. Existing ADRs and context files remain separate explicit opt-ins.

The provider contract asks for evidence-backed changes to context/problem, decision outcome, rationale, and consequences. Findings must distinguish direct evidence, potential architectural impact, uncertainty, and `not enough information`; changed wording alone is not a policy violation, approval, rejection, or regression.

Both versions are untrusted data, receive the existing credential redaction and size bounds, and are disclosed locally before provider invocation. Provider output remains untrusted and advisory. Reports retain schema `1.0`; `[comparison-base]` appears in `inputScope.explicitContext`, preserving compatibility for consumers that already process explicit sources.

The command fails before provider invocation when the ref/path is absent, unsafe, outside Git, canceled, or cannot be read. Tests use mock providers only.

For GitHub pull requests, run provider-backed comparison only on trusted same-repository events with an explicitly selected secret. Never expose provider credentials to fork PR code and never use `pull_request_target` to check out and review untrusted changes. Forks should run deterministic `check`; a maintainer can trigger comparative review later from a trusted commit/workflow.
