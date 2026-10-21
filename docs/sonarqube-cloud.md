# SonarQube Cloud — optional repository quality checks

[Português (Brasil)](sonarqube-cloud.pt-BR.md) · [Contributing](../AGENTS.md)

SonarQube Cloud complements ADR Guard's existing .NET tests, dependency/security scans, cross-platform smoke checks, and package/release policies. **This repository integration is opt-in and disabled until an administrator configures it.** Sonar results do not approve architectural decisions, change ADR statuses, or replace the deterministic ADR Guard validator.

## Enable the integration

1. In SonarQube Cloud, create or link a project to this repository. Use the **actual project key and organization** from the SonarQube Cloud project settings; neither is inferred from the GitHub repository name.
2. Configure these **GitHub repository Variables** under Settings → Secrets and variables → Actions:
   - `SONAR_CI_ENABLED`: `true` to enable Sonar analysis and high-impact issue synchronization; leave unset or `false` during setup.
   - `SONAR_PROJECT_KEY`: exact SonarQube Cloud project key.
   - `SONAR_ORGANIZATION`: exact SonarQube Cloud organization key.
3. Configure **repository secret** `SONAR_TOKEN` with the least project-level permissions needed to perform analysis and read Quality Gate/findings. Never put the token in Git-tracked files.
4. Confirm SonarQube Cloud GitHub integration and repository permissions. Enable `SONAR_CI_ENABLED` **only after** the other settings are ready and the project can accept analysis. Check the actual Actions run and Quality Gate before considering adding Sonar as a branch-protection required check.

The Sonar analysis workflow is [`.github/workflows/sonar.yml`](../.github/workflows/sonar.yml). It scans internal pull requests targeting `main`, pushes to `main`, and explicitly dispatched runs on `main`. **Fork pull requests never access Sonar secrets or run the privileged analysis.** A separate unprivileged Node.js test job executes on PRs even when the Sonar integration is disabled. The existing release workflows remain unchanged.

## Coverage and Quality Gate

- `.config/dotnet-tools.json` pins `dotnet-sonarscanner` and `dotnet-coverage`; `dotnet tool restore` installs those local tools.
- `eng/sonar-coverage.settings.xml` selects the **production** assemblies `AdrGuard.Core.dll`, `adr-guard.dll`, and `adr-guard-mcp.dll`. It excludes tests, benchmarks, generated files, unrelated assemblies, and `eng/**` automation from the coverage gate while retaining static analysis of that automation.
- `eng/run-sonar-tests.ps1` executes the repository's **native Microsoft.Testing.Platform** solution tests (xUnit v3) as one instrumented run; it does not use VSTest filters or provider-backed AI tests.
- Coverage is written as Visual Studio coverage XML at `artifacts/coverage/sonar/coverage.xml` and verified for all three production assemblies before upload.
- In pull requests, the scanner waits for the Quality Gate. The workflow resolves the current scan's Compute Engine task and queries the gate by its `analysisId`, so a failed gate is reported with its conditions even when the scanner exits unsuccessfully. A single bot-authored comment is always refreshed; missing task evidence, API/network failures, or malformed responses are reported as `UNKNOWN`, never as success or as a stale result.
- Sonar analysis is isolated from the normal CI and package/release jobs. Once enabled, a failing Sonar workflow is visible as a separate check. Configure required status checks only when stable.

The workflow does **not** upload coverage to Codecov or publish public coverage/Quality Gate badges. Add those only after configuring and verifying the corresponding service/project.

## HIGH/BLOCKER findings → GitHub Issues

[`sonar-quality-issues.yml`](../.github/workflows/sonar-quality-issues.yml) can run on Mondays or manually from `main`, **only when `SONAR_CI_ENABLED=true`**. It queries OPEN findings with HIGH/BLOCKER software-quality impact and synchronizes issues with label `Sonar Quality Issues`.

- Each finding uses its stable Sonar issue key and a bounded, machine-managed Markdown section. A GitHub issue is recognized as managed **only when it is bot-authored and carries valid markers**.
- Existing managed issues are updated idempotently, preserving text outside managed markers; disappeared/resolved findings are **not automatically closed**.
- Sonar API/auth failures cause the workflow to fail rather than silently claiming no findings. Never run this workflow with sample credentials against the production issue tracker.
- A disabled workflow makes no GitHub Issue changes. Manual notes and manually authored issues are not overwritten.

Offline test: `node --test scripts/tests/sonar-quality-sync.test.cjs scripts/tests/sonar-quality-gate.test.cjs`. This exercises create, repeat, human-note preservation, authorship checks, Quality Gate failure reporting, and API failure handling without external services.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| Analysis skipped | `SONAR_CI_ENABLED` is `true`; PR is internal; branch/event is allowed |
| Configuration missing | `SONAR_PROJECT_KEY`, `SONAR_ORGANIZATION` variables and `SONAR_TOKEN` secret |
| Coverage missing an assembly | Build Release instrumentation, native test command, coverage XML and `ModulePaths` |
| Quality Gate unknown | Scanner end status, asynchronous Sonar processing and API credentials |
| No synchronized issues | Findings have OPEN status and HIGH/BLOCKER impact, and sync workflow was enabled |

Do not invent a project key, baseline coverage percentage, Quality Gate status, findings or badges. This configuration is repository automation; it does **not** change CLI/MCP/VS Code release versions or public runtime behavior.
