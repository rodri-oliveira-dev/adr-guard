---
core: dotnet-test/run-tests
binding-revision: "1"
mode: extend
---

# ADR Guard test binding

ADR Guard uses the .NET SDK selected by `global.json` (currently .NET 10), native Microsoft.Testing.Platform mode, and xUnit v3. Run commands from the repository root.

## Commands

- Full local suite: `dotnet test --solution AdrGuard.slnx --configuration Release`.
- Full suite after a Release build: `dotnet test --solution AdrGuard.slnx --configuration Release --no-build`.
- Test project: `dotnet test --project tests/AdrGuard.Tests/AdrGuard.Tests.csproj --configuration Release`.
- One xUnit v3 class: append `--filter-class AdrGuard.Tests.Namespace.TestClass` to the native MTP command.
- One xUnit v3 method: append `--filter-method TestMethod` and, when needed, `--filter-class AdrGuard.Tests.Namespace.TestClass`.
- Combined xUnit v3 expressions: use one `--filter-query` expression. Do not use VSTest `--filter` syntax.

Native MTP arguments are direct arguments: do not insert the VSTest bridge separator `--`. Use `--solution` or `--project`; do not pass a solution or project as an unlabelled positional argument in new local commands.

## Local versus CI

The checked-in workflow is the CI authority and currently invokes `dotnet test` after an explicit Release build. Preserve its exact command shape unless the task deliberately updates and validates CI. Locally, omit `--no-build` unless the matching configuration was already built. Network-backed review providers are not required by the deterministic test suite.

Report the exact command, exit code, and literal Passed/Failed/Skipped summary. A successful filtered run must also show that the intended test scope was selected.
