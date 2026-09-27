# AI review release availability

This note records the published version boundary for AI-assisted ADR technical review without retroactively changing older release notes.

| Capability | First published version | Notes |
| --- | --- | --- |
| `adr-guard review`, eight-dimension analysis, explicit/bounded context, cross-ADR evidence, text/JSON schema `1.0` reports | `v1.1.2` | CLI/.NET Tool and release containers |
| Deterministic review policy schema `1.0`, advisory default, explicit `enforce`, exit code `4` | `v1.1.3` | Model findings remain advisory |
| Security/trust-boundary hardening plus deterministic mock-provider regression matrix | `v1.1.4` | Includes strict output validation and review security controls |
| Current published release | `v1.1.5` | Includes all CLI review capabilities above |
| Reusable GitHub Action `command: review` | Not published at the time this note was written | Implemented and CI-validated in PR #85; it becomes part of the moving `@v1` Action only after the post-merge release publishes |

The already-published `@v1` Action must therefore be treated as `check`/`index` only until the release containing PR #85 is published. Do not copy a pre-release `command: review` workflow and assume the current remote `@v1` supports it.

The Marketplace listing is a separate publication milestone and is not claimed as live here.

See [AI review guide](../adr-review.md) and [GitHub Action AI review](../github-action-review.md).
