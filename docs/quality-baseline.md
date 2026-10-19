# Quality baseline

This page records contributor-facing evidence for the P1 quality roadmap. It describes source-repository checks, not capabilities guaranteed by a published ADR Guard release.

## Cross-platform core CLI coverage

The `Core CLI cross-platform smoke` workflow runs one focused public-command journey on Ubuntu, Windows, and macOS. It Release-builds only the authoritative CLI and invokes the resulting `adr-guard.dll` through `dotnet`; it does not repeat the full .NET suite or the MCP package matrix.

The disposable repository path contains whitespace and Unicode. The journey verifies:

- `init → new → check → index` through real command-line arguments;
- idempotent `init` and byte-stable `index` reruns;
- JSON schema `1.0`, success exit `0`, validation exit `1`, and a stable `ADR004` finding;
- a Git repository and `check --changed --base-ref main`;
- identical machine output on repeated invalid checks; and
- cleanup in a `finally` block.

Run it with the repository-pinned native MTP runner:

```text
dotnet test --project tests/AdrGuard.Tests/AdrGuard.Tests.csproj --configuration Release --filter-class AdrGuard.Tests.Cli.CoreCliCrossPlatformSmokeTests
```

The existing unit/integration suite remains responsible for traversal, physical-path and symlink containment. The smoke does not create symlinks because Windows shared runners do not consistently grant that privilege. Host-native separators and filesystem case behavior are exercised by each runner; the test does not assert that distinct case variants must coexist.

## Diagnostic audit

All current validation codes were traced from the parser and validator through text, JSON/SARIF, and the VS Code Problems adapter. `--diagnostic-locations` remains opt-in and source-only until a containing CLI release is published.

| Code / family | Trigger and action | Exact location evidence | Coverage / audit result |
| --- | --- | --- | --- |
| `ADR001` | File name violates the selected convention; rename it. | File only; the violation is the path. | Validator coverage; intentionally unlocated. |
| `ADR002` | Missing H1 title; add one. | File only; no heading exists. | Validator coverage; intentionally unlocated. |
| `ADR003` | Missing/empty canonical status or empty MADR status metadata; add a supported value. | Existing canonical heading only. Missing heading and front matter remain file-only. | Validator and adapter fallback coverage. |
| `ADR004` | Unsupported status; choose a configured lifecycle value. | Parsed canonical status heading. | JSON, SARIF, VS Code, and cross-platform smoke coverage. |
| `ADR005` | Required section is missing/empty; add content. | Parsed heading when empty; missing section is file-only. | JSON/SARIF and VS Code coverage. |
| `ADR006` | Duplicate numeric/stable identity; make identities unique. | Multi-file conflict, so file-only. | Incremental/global validation coverage. |
| `ADR007` | Unsafe or unresolved Markdown/relationship target; repair the reported target. | Parsed link when present; unresolved MADR status metadata is file-only. | Parser-backed relationship coverage. |
| `ADR008` | Superseded ADR lacks a usable successor; add one existing ADR link. | Parsed `Superseded by` heading when it exists; absent section is file-only. | **Gap fixed:** validator, JSON, SARIF, and VS Code contract fixture. |
| `ADR009` | Duplicate canonical/MADR section; remove or merge duplicates. | Second parsed heading. | Validator location coverage. |
| `ADR010` | Supersession graph contains a cycle; break the cycle. | Global multi-edge conflict, so file-only. | Graph determinism coverage. |
| `ADR011`–`ADR012` | Self-supersession or multiple successors; correct the relationship set. | Graph-level result currently file-only. | Covered; finer per-edge positioning deferred because the validator normalizes targets before these checks. |
| `ADR013` | Supersession direction conflicts with status/target; align both sides. | Parsed `Supersedes` reference where retained; normalized successor/status conflicts remain file-only. | Both located and fallback paths covered. |
| `ADR014` | Dependency targets an inactive ADR; revise the dependency. | Parsed dependency reference. | Validator location coverage. |
| `ADR015` | Unresolved authoring placeholder; replace it. | Containing parsed section heading. Severity is policy-dependent. | Placeholder-policy coverage. |
| `ADR016` | Invalid bounded metadata; correct the named key/value. | File only because front-matter key positions are not retained by the current parser. | Metadata coverage; parser expansion deferred to avoid inferred coordinates. |

The audit did not justify changing diagnostic identifiers, severity, ordering, messages, exit codes, schema versions, or baseline fingerprints. In particular, missing structures, graph-wide conflicts, and front-matter findings remain unlocated rather than receiving inferred coordinates.

## Reproducible performance baseline

`benchmarks/AdrGuard.Benchmarks` is a dependency-free, informational harness. It generates separate valid and deterministically invalid synthetic corpora of 100, 500, and 900 ADRs with normalized LF line endings. The files use varied lifecycle statuses, resolved references/dependencies, bounded prose, and one invalid status per 100 ADRs. No production content or network/provider call is involved.

Build the CLI and harness, then record five subsequent samples plus a first-run sample:

```text
dotnet build AdrGuard.slnx --configuration Release
dotnet run --project benchmarks/AdrGuard.Benchmarks/AdrGuard.Benchmarks.csproj --configuration Release --no-build -- --cli src/AdrGuard/bin/Release/net10.0/adr-guard.dll --output benchmarks/results/local.json --samples 5
```

The harness reports generation time separately. `check-invalid` expects exit `1`; `index-valid` expects exit `0` and removes only its generated `README.md` before each timed invocation so every sample measures index creation rather than the idempotent no-op. Each operation starts a fresh CLI process. Therefore `firstRun` is an OS/filesystem-cache proxy, not a persistent-process JIT measurement; `subsequentRuns` contains the median, minimum, maximum, spread, and median peak working set. Results also record dataset hashes, OS, architecture, .NET runtime, logical processor count, available memory, CLI version, source SHA, and dirty-tree state. `WorkingTreeDirty` is `null` when Git metadata cannot be read, rather than treating an unknown state as a clean worktree.

Committed measurements are environment-specific observations, not SLAs or CI thresholds. Compare only reports with matching dataset hashes and investigate material changes with profiling before proposing optimization.

### Observed Windows baseline — 2026-10-18

The complete machine-readable report is [`benchmarks/results/windows-x64-2026-10-18.json`](../benchmarks/results/windows-x64-2026-10-18.json). It measured source and CLI SHA `f32b81956037b4935fc3da2cd64bd763e8b7491c` on Windows x64, .NET 10.0.13, 8 logical processors, and approximately 31.4 GiB available memory. The worktree-dirty marker is true because the checkout contained a pre-existing unrelated `.gitignore` edit; the report records the exact CLI informational version and dataset hashes so this limitation is explicit.

| ADRs | Operation | Preparation | First run | Subsequent median (min–max; spread) | Median peak working set |
| ---: | --- | ---: | ---: | ---: | ---: |
| 100 | invalid `check` | 65.182 ms | 1068.201 ms | 255.384 ms (243.517–331.316; 87.799 ms) | 30.5 MiB |
| 100 | valid `index` | 65.182 ms | 247.975 ms | 242.322 ms (236.766–274.834; 38.068 ms) | 30.6 MiB |
| 500 | invalid `check` | 288.848 ms | 334.655 ms | 417.161 ms (310.669–451.435; 140.766 ms) | 35.5 MiB |
| 500 | valid `index` | 288.848 ms | 377.254 ms | 304.760 ms (296.728–324.549; 27.821 ms) | 35.6 MiB |
| 900 | invalid `check` | 472.707 ms | 370.342 ms | 368.928 ms (362.144–406.005; 43.860 ms) | 36.5 MiB |
| 900 | valid `index` | 472.707 ms | 482.886 ms | 367.128 ms (358.665–380.474; 21.809 ms) | 36.3 MiB |

Two complete local generations produced identical hashes for all six valid/invalid datasets. Temporary scenario directories were absent after each run. The first 100-ADR check shows substantial cold-start/noise cost, reinforcing why no threshold or optimization conclusion is attached to this baseline. No macOS or Linux performance numbers were measured in this checkpoint.
