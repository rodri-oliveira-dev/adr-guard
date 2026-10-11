# Architecture fitness rules v1

`docs/schemas/adr-fitness-rules-v1.schema.json` defines the opt-in manifest that links effective ADRs to bounded architecture evidence. This contract is separate from advisory `review --policy` findings and from architecture-impact candidates.

The supported outcomes are `pass`, `violation`, `unknown`, and `not-applicable`. Missing or incomplete evidence is always `unknown`, never `pass`. A rule attached to a non-effective ADR is `not-applicable`; an ADR that cannot be resolved is `unknown`.

Version 1 supports two checks:

- `forbid-project-reference`: inspect exact, repository-relative `.csproj` paths without evaluating MSBuild;
- `external-test`: correlate an explicit test ID with a bounded local evidence artifact without executing or downloading it.

External artifacts use `docs/schemas/adr-fitness-evidence-v1.schema.json`. They identify the producer/version, CI run, artifact, SHA-256 and observations. ADR Guard validates those identifiers and preserves them in results, but v1 does not attest the producer or independently verify the declared digest. A reported pass or failure therefore has `reported-external` confidence; contradictory, stale, future-dated, skipped, errored, missing, or mismatched observations are `unknown`. Timestamps are optional; when absent, the report discloses that freshness is unavailable. URLs are never dereferenced and scripts/tests are never executed.

All paths use `/`, are relative to the selected repository, and must remain physically contained by it. Consumers must reject unknown fields, duplicate JSON properties, unsafe XML features, excessive input, and ambiguous references. `gate: true` records that a rule is eligible for an explicitly selected enforcement policy; it does not turn unknown evidence into success or failure.

Snapshots use `docs/schemas/adr-drift-snapshot-v1.schema.json` and are written only by an explicit create/update operation. Writes are atomic and refuse symbolic-link targets. A current violation is `new` or `existing`; a baseline violation becomes `resolved` only when complete current evidence returns `pass`. Missing mappings, inactive decisions, and `unknown` evidence retain an `unresolved-baseline` record. Snapshot fingerprints exclude presentation text, timestamps, absolute paths, and JSON formatting.

The fixture at `tests/AdrGuard.Tests/Fixtures/Drift/contract` contains one rule of each type and the security/regression cases that later Phase 2 work exercises.
