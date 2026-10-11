# Reproducible architectural-drift case study

The accepted ADR forbids Domain → Infrastructure. The committed `Domain.csproj` is compliant.

1. Run `adr-guard drift . --rules .adrguard-fitness.json --format json`; outcome is `pass`.
2. Replace `src/Domain/Domain.csproj` with `Domain.violation.csproj.fixture`; rerun in advisory mode to see the exact reference and line as a `violation`.
3. Run with `--policy enforce`; the new deterministic violation returns exit 4.
4. Create history only by explicit owner action: `adr-guard drift . --rules .adrguard-fitness.json --write-baseline drift-baseline.json`. Review the new file before committing it.
5. Restore the original project (remove the forbidden reference) and run with `--baseline drift-baseline.json`; the prior violation is `resolved` and the current rule is `pass`.

Replacing the project with `Domain.unknown.csproj.fixture` produces `unknown` and exit 3 because MSBuild conditions are not evaluated. Running `.adrguard-fitness-with-external.json` without `--evidence` also produces `unknown`, never pass. These outcomes show an analysis boundary, not a certified architecture: humans still decide whether the rule represents the ADR and whether baseline changes are acceptable.
