# Architecture fitness rules v1

`docs/schemas/adr-fitness-rules-v1.schema.json` defines the opt-in manifest that links effective ADRs to bounded architecture evidence. This contract is separate from advisory `review --policy` findings and from architecture-impact candidates.

The supported outcomes are `pass`, `violation`, `unknown`, and `not-applicable`. Missing or incomplete evidence is always `unknown`, never `pass`. A rule attached to a non-effective ADR is `not-applicable`; an ADR that cannot be resolved is `unknown`.

Version 1 supports two checks:

- `forbid-project-reference`: inspect exact, repository-relative `.csproj` paths without evaluating MSBuild;
- `external-test`: correlate an explicit test ID with a bounded local evidence artifact without executing or downloading it.

All paths use `/`, are relative to the selected repository, and must remain physically contained by it. Consumers must reject unknown fields, duplicate JSON properties, unsafe XML features, excessive input, and ambiguous references. `gate: true` records that a rule is eligible for an explicitly selected enforcement policy; it does not turn unknown evidence into success or failure.

The fixture at `tests/AdrGuard.Tests/Fixtures/Drift/contract` contains one rule of each type and the security/regression cases that later Phase 2 work exercises.
