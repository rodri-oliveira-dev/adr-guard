$ErrorActionPreference = 'Stop'

# ADR Guard selects native Microsoft.Testing.Platform in global.json (xUnit v3).
# Use one invocation to avoid double-counting the same projects in coverage.
dotnet test --solution AdrGuard.slnx --configuration Release --no-build
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
