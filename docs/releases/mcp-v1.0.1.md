# ADR Guard MCP v1.0.1

[Português (Brasil)](mcp-v1.0.1.pt-BR.md)

ADR Guard MCP `1.0.1` was published on 2026-11-05 to NuGet.org, GitHub Packages, GitHub Releases, and the official MCP Registry.

## Highlights

- References the internal `AdrGuard.Core` project directly for shared parsing, validation, configuration, Git inventory, impact, and drift behavior.
- Removes any build or runtime dependency on the ADR Guard CLI executable. The package includes `adr-guard-mcp` and `AdrGuard.Core.dll` and excludes `adr-guard.dll`.
- Retains all seven read-only schema `1.0` tools over local `stdio`.
- Resolves physical Git-directory aliases before containment checks so equivalent macOS `/var` and `/private/var` paths do not cause false boundary failures.

## Compatibility and requirements

- Requires .NET 10 and a compatible local `stdio` MCP client.
- Tool names, startup arguments, schema `1.0`, structured fields, limits, error semantics, transport, and read-only annotations are unchanged from `1.0.0`.
- Git remains optional and is used only for impact analysis and commit-aware architecture metadata.
- The server remains independent from the CLI and VS Code extension and cannot create, edit, approve, or change the status of ADRs.

Install with:

```shell
dotnet tool install --global RodriOliveira.AdrGuard.Mcp --version 1.0.1
adr-guard-mcp --repository /absolute/path/to/repository --adr-directory docs/adr
```
