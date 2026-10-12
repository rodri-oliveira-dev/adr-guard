# Architectural drift and fitness evidence

`adr-guard drift` verifies narrow architecture rules linked to effective ADRs. It is local, deterministic where possible, and separate from advisory AI review and change-impact analysis.

```text
adr-guard drift . --rules .adrguard-fitness.json --format json
adr-guard drift . --rules .adrguard-fitness.json --policy enforce
```

The default policy is `advisory`. Enforcement blocks only a new, `gate: true`, locally verified `forbid-project-reference` violation (exit 4). A baselined existing violation remains visible but does not block. External reported failures never block because their producer is not attested. Any `unknown` result exits 3 in both policies, so missing or ambiguous evidence cannot masquerade as a green check. Invalid usage exits 2.

Use `--evidence path.json` repeatedly for explicit external artifacts. Use `--write-baseline drift-baseline.json` to create a snapshot and add `--update-baseline` only for a deliberate replacement. `--baseline drift-baseline.json` compares without writing. Review baseline changes like source code.

## CI recipe

The current composite Action contract is intentionally unchanged. Install the .NET Tool in a normal least-privilege step and invoke drift directly; no secret or write permission is required.

```yaml
permissions:
  contents: read
steps:
  - uses: actions/checkout@v6
  - uses: actions/setup-dotnet@v5
    with:
      dotnet-version: 10.0.x
  - run: dotnet tool install --global RodriOliveira.AdrGuard --version 1.6.0
  - run: adr-guard drift . --rules .adrguard-fitness.json --policy enforce --format json
```

This example requires ADR Guard v1.6.0 from NuGet.org. Confirm that the coordinated release is published before running the workflow; merging the implementation PR alone does not publish a package. The `drift` command performs no network calls and reads no provider credentials.

## Limits

Only exact static `.csproj` `ProjectReference` relationships are locally proven. Conditions, MSBuild properties, wildcards, imports, missing files and unsafe XML are `unknown`; ADR Guard never evaluates MSBuild. External JSON is a reported observation with preserved provenance, not attestation. Human review remains necessary for whether a rule faithfully represents the ADR and whether a baseline update is justified.

Contracts: [fitness rules v1](../fitness-rules-v1.md), [rules schema](../schemas/adr-fitness-rules-v1.schema.json), [evidence schema](../schemas/adr-fitness-evidence-v1.schema.json), [snapshot schema](../schemas/adr-drift-snapshot-v1.schema.json), and [report schema](../schemas/adr-drift-report-v1.schema.json).
