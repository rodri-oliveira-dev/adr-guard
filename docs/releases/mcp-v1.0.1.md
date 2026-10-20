# ADR Guard MCP v1.0.1

[Português (Brasil)](mcp-v1.0.1.pt-BR.md)

<!-- release-candidate-only:start -->
ADR Guard MCP `1.0.1` is prepared in source control but, as of 2026-10-20, is **not published** on NuGet.org, GitHub Releases, GitHub Packages, or the MCP Registry. The current public MCP release remains `1.0.0`. This patch candidate changes internal composition and fixes platform handling while preserving every public MCP contract.
<!-- release-candidate-only:end -->

## Highlights

- References the internal `AdrGuard.Core` project directly for shared parsing, validation, configuration, Git inventory, impact, and drift behavior.
- Removes any build or runtime dependency on the ADR Guard CLI executable. The package includes `adr-guard-mcp` and `AdrGuard.Core.dll` and excludes `adr-guard.dll`.
- Retains all seven read-only schema `1.0` tools over local `stdio`: `list_adrs`, `get_adr`, `search_adrs`, `get_adr_relationships`, `validate_adrs`, `analyze_impact`, and `verify_architecture`.

## Fixes and internal improvements

- Resolves physical Git-directory identities before containment checks so macOS `/var` and `/private/var` aliases do not cause false repository-boundary failures.
- Keeps deterministic ordering, fingerprints, validation, impact, and drift behavior shared with the CLI through Core without publishing Core as a separate package.
- Strengthens package graph and archive checks so MCP cannot acquire a CLI reference or package the opposite executable.
- Preserves the manual, fail-closed release flow and its independent version reservation, local-package smoke tests, provenance checks, and Registry validation.

## Compatibility and requirements

- Requires .NET 10 and a compatible local `stdio` MCP client.
- Tool names, startup arguments, schema version `1.0`, structured fields, limits, error semantics, transport, and read-only annotations are unchanged from `1.0.0`.
- Git remains optional and is used only for `analyze_impact` and commit-aware `verify_architecture` metadata.
- The server is independent from the CLI and VS Code extension. VS Code `1.1.0` can register it when the MCP integration is explicitly enabled and a separate executable path is configured.

## Behavior changes

No intentional public behavior changes. The macOS path fix prevents a valid repository from being rejected solely because the operating system exposes equivalent physical path aliases. Missing, malformed, out-of-scope, or unverifiable evidence continues to fail closed or remain `unknown`/inconclusive.

## Known limitations

- The server has no network transport, write tools, hosted service, provider integration, autonomous approval, or evidence-production capability.
- It does not run builds, tests, scripts, or Git fetches and cannot create, edit, approve, or change the lifecycle status of an ADR.
- The checked-in `.mcp/server.json` records the last independently verified Registry entry. The protected release workflow derives the selected version from the validated release commit and validates the generated manifest before Registry publication.

## Install or update

<!-- release-candidate-only:start -->
Before publication, install only the locally validated package:

```shell
dotnet tool install --tool-path ./.mcp-tools RodriOliveira.AdrGuard.Mcp \
  --version 1.0.1 --add-source ./artifacts/mcp
./.mcp-tools/adr-guard-mcp --repository /absolute/path/to/repository --adr-directory docs/adr
```

Continue using `1.0.0` for public NuGet installations until `1.0.1` is independently visible in NuGet.org and the MCP Registry.
<!-- release-candidate-only:end -->

When NuGet.org lists the exact version, install it with:

```shell
dotnet tool install --global RodriOliveira.AdrGuard.Mcp --version 1.0.1
adr-guard-mcp --repository /absolute/path/to/repository --adr-directory docs/adr
```

<!-- release-candidate-only:start -->
## Post-publication checklist

After publication is explicitly authorized and completed:

- verify `1.0.1` on NuGet.org, its package provenance and contents, and a clean installation;
- verify the immutable `mcp/v1.0.1` GitHub Release and assets;
- verify the official Registry entry names `1.0.1`, points to the same NuGet package, and still declares `stdio`;
- verify GitHub Packages separately; do not run the historical `1.0.0` backfill as part of this release;
- only then update the checked-in `.mcp/server.json`, installation defaults, changelog status, README files, compatibility matrix, and portal sources from candidate to published.
<!-- release-candidate-only:end -->
