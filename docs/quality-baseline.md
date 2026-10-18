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
