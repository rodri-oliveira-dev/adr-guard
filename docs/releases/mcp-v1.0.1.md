# ADR Guard MCP v1.0.1

[Português (Brasil)](mcp-v1.0.1.pt-BR.md)

<!-- release-candidate-only:start -->
> Publication status: MCP `1.0.1` was published on [NuGet.org](https://www.nuget.org/packages/RodriOliveira.AdrGuard.Mcp/1.0.1), in the [dedicated GitHub Release](https://github.com/rodri-oliveira-dev/adr-guard/releases/tag/mcp/v1.0.1), and in the official MCP Registry. GitHub Packages availability is verified separately.
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
- The server is independent from the CLI and VS Code extension. Published VS Code `1.1.0` and candidate `1.1.1` can register it when MCP integration is explicitly enabled and a separate executable path is configured.

## Behavior changes

No intentional public behavior changes. The macOS path fix prevents a valid repository from being rejected solely because the operating system exposes equivalent physical path aliases. Missing, malformed, out-of-scope, or unverifiable evidence continues to fail closed or remain `unknown`/inconclusive.

## Known limitations

- The server has no network transport, write tools, hosted service, provider integration, autonomous approval, or evidence-production capability.
- It does not run builds, tests, scripts, or Git fetches and cannot create, edit, approve, or change the lifecycle status of an ADR.
- The checked-in `.mcp/server.json` records the last independently verified Registry entry. The protected release workflow derives the selected version from the validated release commit and validates the generated manifest before Registry publication.

## Install or update

<!-- release-candidate-only:start -->
<!-- Local pre-release installation instructions retired after publication. -->
<!-- release-candidate-only:end -->

Install the published package from NuGet.org:

```shell
dotnet tool install --global RodriOliveira.AdrGuard.Mcp --version 1.0.1
adr-guard-mcp --repository /absolute/path/to/repository --adr-directory docs/adr
```

To update an existing global installation to the published version:

```shell
dotnet tool update --global RodriOliveira.AdrGuard.Mcp --version 1.0.1
```


<!-- release-candidate-only:start -->
<!-- NuGet.org, GitHub Releases, and MCP Registry publication verified. GitHub Packages remains a separate channel. -->
<!-- release-candidate-only:end -->
