# ADR review policy v1

[Português (Brasil)](adr-review-policy-v1.pt-BR.md)

ADR Guard review policy separates deterministic CI enforcement from fallible AI reviewer guidance.

## Safe default

`adr-guard review` uses `advisory` policy mode by default. AI findings, including wording such as `critical` or `high`, never change the exit code by themselves and never accept, reject, or change the status of an ADR.

Opt-in enforcement requires both:

- `--policy enforce`
- `--policy-file <path>`

The policy file is local JSON, is not transmitted to the review provider, and uses schema version `1.0`.

## Policy schema

```json
{
  "schemaVersion": "1.0",
  "rules": [
    {
      "name": "decision-rationale-present",
      "type": "required-section-content",
      "section": "Decision"
    },
    {
      "name": "security-context-selected",
      "type": "required-context-file",
      "path": "security-context.md"
    }
  ]
}
```

Rule names must be unique and contain only letters, digits, `-`, `_`, or `.`.

Supported deterministic rules:

| Type | Deterministic evidence |
| --- | --- |
| `required-section-content` | A named level-two Markdown section exists and has non-whitespace content. |
| `required-context-file` | The relative `.md` or `.txt` file exists relative to the policy file and the same file was explicitly supplied with `--context-file`. |

Policy JSON rejects unknown properties, unsupported rule types, duplicate names, absolute required-context paths, unsupported extensions, and unsupported schema versions.

## Modes

| Mode | Deterministic policy violations | AI findings |
| --- | --- | --- |
| `advisory` | Reported as diagnostics; review continues and they do not fail CI. | Reported only; never fail CI. |
| `enforce` | Review stops before provider invocation and exits with code 4, listing each named rule and evidence. | Never evaluated as a policy gate and never fail CI. |

A passing enforcement policy does not mean the ADR is approved. Human decision ownership remains unchanged.

## Outcome and exit-code matrix

| Situation | Outcome / diagnostic | Exit code |
| --- | --- | ---: |
| Completed advisory review with or without model follow-up findings | Report outcome is `no-follow-up-findings`, `follow-up-suggested`, or `needs-context` | 0 |
| Enforce mode, all deterministic rules satisfied | Normal review report; model wording cannot change policy result | 0 |
| Structurally invalid ADR | Structural validation failure; provider is not called | 1 |
| Invalid CLI or invalid policy configuration | Usage/policy configuration error | 2 |
| Missing target/context input, provider unavailable, cancellation, or other I/O/operational failure | Explicit operational diagnostic; never treated as “no issues” | 3 |
| Malformed or incomplete provider review output | Explicit provider/report operational failure; never treated as “no issues” | 3 |
| Enforce mode, one or more deterministic policy rules violated | `policy-failed` diagnostic with rule name/type/evidence; provider is not called | 4 |

A model-produced `missing-context` finding yields report outcome `needs-context` but remains advisory, including in `enforce` mode. If missing context must be a CI gate, express that requirement as a deterministic policy rule such as `required-context-file`.
