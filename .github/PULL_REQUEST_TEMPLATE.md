<!--
Thanks for contributing to ADR Guard!
Keep the description focused and factual. Mark applicable checkboxes only.
Use "N/A" when a section does not apply, and never report a check as passing unless it was run.
See AGENTS.md for architecture rules and the repository's validation commands.
-->

## Summary

<!-- Explain what changed and why. Focus on observable behavior and the problem being solved. -->

## Related issue

<!-- Example: Closes #123, Relates to #456, or N/A. -->

## Change type

- [ ] Bug fix
- [ ] Feature or enhancement
- [ ] Refactoring / maintenance
- [ ] Documentation / localization
- [ ] CI / build / repository governance
- [ ] Release preparation

## Affected components

- [ ] CLI (`src/AdrGuard`)
- [ ] Shared Core (`src/AdrGuard.Core`)
- [ ] MCP Server (`src/AdrGuard.Mcp`)
- [ ] VS Code extension (`extensions/vscode`)
- [ ] GitHub Action / container
- [ ] Agent Skills
- [ ] Documentation / website
- [ ] Other (describe below)

## Implementation and behavior

<!-- Summarize the approach, notable design choices, and any alternatives considered. -->
<!-- For durable architectural decisions, link an ADR; do not create one for routine implementation details. -->

## Compatibility, risk, and security

<!-- Explain impacted users, migration needs, behavioral changes, risks, and mitigations. Write N/A where appropriate. -->

- [ ] Existing public contracts remain compatible, or changes are explicitly identified and versioned.
- [ ] Relevant contracts were reviewed (CLI arguments/exit codes, JSON/SARIF, MCP tools, Action inputs/outputs, VS Code settings, configuration, or skill metadata).
- [ ] Untrusted inputs, repository/workspace boundaries, credentials, and failure behavior were considered where relevant.
- [ ] AI-assisted output, if affected, remains advisory; architectural approval and ADR status remain human-owned.

## Validation

<!-- List exact commands executed and their results. Do not claim success for checks that were not run. -->
<!-- The .NET tests use xUnit v3 / Microsoft.Testing.Platform. -->

| Check | Command or evidence | Result |
| --- | --- | --- |
| Build | `dotnet build AdrGuard.slnx --configuration Release --no-restore` | Not run / Passed / Failed / N/A |
| Tests | `dotnet test --solution AdrGuard.slnx --configuration Release --no-build` | Not run / Passed / Failed / N/A |
| Component-specific checks | <!-- VS Code, Action, MCP, docs, security, etc. --> | Not run / Passed / Failed / N/A |

<!-- Explain omitted checks and any known limitations. Add regression and contract coverage when behavior changes. -->

## Documentation and release readiness

- [ ] Relevant English and pt-BR documentation, examples, and website sources were updated, or no user-facing documentation is affected.
- [ ] Versioned contracts, compatibility guidance, and changelog/release notes were updated when applicable.
- [ ] Documentation distinguishes implemented source changes from artifacts actually published to NuGet, Marketplaces, container registries, or release tags.
- [ ] No release, package publication, tag movement, or architectural approval is implied by this PR alone.

## Reviewer notes

<!-- Highlight files or scenarios to review, follow-up work, and anything reviewers should pay special attention to. -->
