# Architectural drift and fitness evidence

`adr-guard drift` verifies narrow architecture rules linked to effective ADRs. It is local, deterministic where possible, and separate from advisory AI review and change-impact analysis.

```text
adr-guard drift . --rules .adrguard-fitness.json --format json
adr-guard drift . --rules .adrguard-fitness.json --policy enforce
```

The default policy is `advisory`. Enforcement blocks only a new, `gate: true`, locally verified `forbid-project-reference` violation (exit 4). A baselined existing violation remains visible but does not block. External reported failures never block because their producer is not attested. Any `unknown` result exits 3 in both policies, so missing or ambiguous evidence cannot masquerade as a green check. Invalid usage exits 2.

Use `--evidence path.json` repeatedly for explicit external artifacts. Use `--write-baseline drift-baseline.json` to create a snapshot and add `--update-baseline` only for a deliberate replacement. `--baseline drift-baseline.json` compares without writing. Review baseline changes like source code.

## CI recipe

The current development source adds a read-only `drift` mode to the composite Action. This Action contract is not yet published on `@v1` or `v1.9.0`; pin a reviewed source commit and an exact published runtime image only for development evaluation. No secret or write permission is required.

```yaml
permissions:
  contents: read
steps:
  - uses: actions/checkout@v7
    with:
      persist-credentials: false
  - uses: rodri-oliveira-dev/adr-guard@<commit-sha>
    with:
      version: 1.9.0
      command: drift
      drift-rules: .adrguard-fitness.json
      drift-policy: enforce
```

The Action requests the CLI JSON report, preserves exits `0`, `2`, `3`, and `4`, and writes a bounded summary without making a new policy decision. Optional `drift-baseline` reads an existing snapshot; newline-delimited `drift-evidence` accepts at most 20 explicit JSON files. Baseline creation/update remains a deliberate direct CLI operation. Confirm a coordinated Action release before replacing `<commit-sha>` with a release tag. See the [Action guide](../github-action.md#architectural-drift-verification-development-source-only) and [complete source-only example](../examples/github-action-drift.yml).

## Limits

Only exact static `.csproj` `ProjectReference` relationships are locally proven. Conditions, MSBuild properties, wildcards, imports, missing files and unsafe XML are `unknown`; ADR Guard never evaluates MSBuild. External JSON is a reported observation with preserved provenance, not attestation. Human review remains necessary for whether a rule faithfully represents the ADR and whether a baseline update is justified.

Contracts: [fitness rules v1](../fitness-rules-v1.md), [rules schema](../schemas/adr-fitness-rules-v1.schema.json), [evidence schema](../schemas/adr-fitness-evidence-v1.schema.json), [snapshot schema](../schemas/adr-drift-snapshot-v1.schema.json), and [report schema](../schemas/adr-drift-report-v1.schema.json).
