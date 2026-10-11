# Development skill provenance

These skills support contributors working on ADR Guard. They are separate from the eleven consumer-facing skills under `skills/`. The machine-readable source record is `sources.json`.

All imported skills are pinned to `rodri-oliveira-dev/agent-skills-catalog@90930c3c593cb91d1737e02f0a2dff133b44b054`. `run-tests` and `csharp-refactoring` are unmodified mirrors of `dotnet/skills@1d40f95ed0f2cdedfa4c9b2614b7c2035a4323cb`; the other four imports are reviewed MIT adaptations maintained by the catalog. License texts are retained under `.agents/licenses/`.

## Selection record

| Candidate | Decision | Reason |
| --- | --- | --- |
| `dotnet-issue-implementation` | Installed | Provides a focused issue-to-DoD workflow without embedding repository rules in the skill. |
| `dotnet-bug-investigation` | Installed | Separates evidence-driven root-cause work from feature implementation. |
| `dotnet-security-review` | Installed | Fits the CLI, file/path handling, provider integrations, Actions, and supply-chain surface. |
| `run-tests` | Installed with repository overlay | Correctly distinguishes native Microsoft.Testing.Platform and xUnit v3 syntax. |
| `repository-governance-sdd` | Installed | Covers recurring changes to AGENTS.md, skills, ADRs, and process documentation. |
| `csharp-refactoring` | Installed | Adds behavior-preserving refactoring safeguards and public-contract checks. |
| `msbuild` | Not installed | The current build has no custom targets or recurring MSBuild diagnostics; the complete official bundle is large and should be added when that workflow becomes recurrent. |
| `docker-build-strategies` | Not installed | Docker is already covered by a small Dockerfile plus Hadolint, smoke tests, and Trivy; the broad skill would add low-frequency guidance and executable assets. |
| `code-testing` | Not installed | Its broad multi-agent pipeline and companion references overlap repository testing rules; focused tests are adequately covered by AGENTS.md and `run-tests`. |
| `test-anti-patterns` | Not installed | Useful for dedicated audits, but too broad for routine activation and overlaps the deterministic testing guidance retained in AGENTS.md. |

No ADR Guard-specific drift-development skill was added. The current recurring invariants fit the root architecture rules, while implementation and regression work route cleanly through the .NET issue, bug, security, refactoring, and testing skills.
