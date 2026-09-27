# Deterministic ADR review fixtures

These fixtures exercise the ADR review **contract and control flow**, not architectural truth.

- `adrs/` contains synthetic, non-sensitive ADRs for target, active-conflict, superseded/history, replacement, and structural-failure scenarios.
- `context/` contains explicit selected/unselected context fixtures.
- `policies/` contains deterministic local policy input.
- `responses/` contains fixed mock-provider payloads for complete, sparse, non-applicable, cross-ADR, empty, malformed, and malicious-response scenarios.

No fixture is a permanent label asserting that Redis, PostgreSQL, or any other technology is objectively correct. Tests assert schema, evidence attribution, uncertainty, policy behavior, exit codes, ordering, bounds, failure handling, and read-only invariants.

The dedicated CI regression gate injects an in-process fixture provider. It must never require provider credentials, network access, or billable AI calls.
