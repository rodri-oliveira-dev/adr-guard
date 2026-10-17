# Configure the ADR Guard MCP server

[Português (Brasil)](mcp-configuration.pt-BR.md) · [Overview](mcp.md) · [Tools](mcp-tools.md) · [Security](mcp-security.md)

MCP 1.0.0 is a [published .NET Tool on NuGet.org](https://www.nuget.org/packages/RodriOliveira.AdrGuard.Mcp/1.0.0) with a [dedicated GitHub Release](https://github.com/rodri-oliveira-dev/adr-guard/releases/tag/mcp/v1.0.0) and official Registry entry. NuGet.org is the recommended installation channel; GitHub Packages is an optional authenticated secondary feed that requires a one-time backfill for version 1.0.0.

## Install the published package

The package requires the .NET 10 runtime and installs `adr-guard-mcp` independently from the traditional `adr-guard` CLI:

```shell
dotnet tool install --global RodriOliveira.AdrGuard.Mcp --version 1.0.0
adr-guard-mcp --repository /absolute/path/to/repository --adr-directory docs/adr
```

For a local tool manifest, use `dotnet tool install --local RodriOliveira.AdrGuard.Mcp --version 1.0.0` and `dotnet tool run adr-guard-mcp -- --repository ...`. Verify the NuGet package and Registry entry first; a checked-in workflow alone is not publication.

## Prerequisites and source build

- .NET SDK `10.0.402`, as pinned by `global.json`.
- A local checkout containing `src/AdrGuard.Mcp`.
- Git on the server host only when using `analyze_impact` or when `verify_architecture` records the current commit.

From the repository root:

```shell
dotnet restore AdrGuard.slnx
dotnet build AdrGuard.slnx --configuration Release --no-restore
```

The framework-dependent entry point is:

```text
src/AdrGuard.Mcp/bin/Release/net10.0/adr-guard-mcp.dll
```

Run it with the runtime and a fixed repository root:

```shell
dotnet src/AdrGuard.Mcp/bin/Release/net10.0/adr-guard-mcp.dll \
  --repository /absolute/path/to/repository \
  --adr-directory docs/adr \
  --timeout-seconds 30
```

The process waits for MCP messages on standard input. Starting it interactively is useful only for confirming configuration errors on standard error; use an MCP client for protocol communication.

## Startup arguments

| Argument | Required | Default | Contract |
| --- | --- | --- | --- |
| `--repository <path>` | No | Current working directory | Existing local repository root; resolved to a full path at startup. |
| `--adr-directory <relative-path>` | No | `.adrguard.yml`, then `docs/adr` when present, otherwise `.` | Existing directory contained by the repository. |
| `--timeout-seconds <1-300>` | No | `30` | Per-tool operation timeout. |

Unknown, missing, or malformed arguments return exit code `2`. Startup I/O and access failures return `3`. The configured repository and ADR directory are validated before the protocol host starts.

## VS Code extension integration

The integration prepared for VS Code extension 1.1.0 uses the stable VS Code MCP provider API and is disabled by default:

1. Build the server in the environment that hosts the workspace extension.
2. Open a trusted local `file:` workspace.
3. Open the ADR Guard visual settings and choose **MCP Integration**, or edit VS Code settings.
4. Set `adrGuard.mcp.path` to an absolute path outside every open workspace. Use either `adr-guard-mcp.dll` or the platform app host named `adr-guard-mcp` (`adr-guard-mcp.exe` on Windows).
5. Enable `adrGuard.mcp.enabled` for the intended workspace folder.
6. Use **Check MCP configuration** in the settings screen. “Available to start” validates configuration; it does not claim that a process is connected or running.
7. Save explicitly. VS Code refreshes its MCP definitions and starts/stops servers through its own MCP lifecycle when an agent requests them.

Example settings (replace the placeholder with a real host-local absolute path):

```json
{
  "adrGuard.mcp.enabled": true,
  "adrGuard.mcp.path": "/opt/adr-guard/adr-guard-mcp.dll"
}
```

The extension contributes provider ID `adrGuard.mcpServers`. It creates one distinguishable `stdio` definition per enabled workspace folder and supplies `--repository` plus that folder's configured `adrGuard.validation.directory`. It never starts MCP during extension activation and does not modify `.vscode/mcp.json`.

VS Code can offer these definitions to compatible agent experiences, including GitHub Copilot agent features when separately installed, enabled, and permitted by the user. ADR Guard has no required Copilot dependency. Repository tests verify the VS Code provider and a local MCP client harness without a paid provider, token, or external model call; they do not claim validation of every agent product.

The extension accepts `adrGuard.mcp.path` only from the trusted user/machine setting. A workspace value is ignored. A `.dll` requires `dotnet` on the extension host `PATH`; discovery examines a bounded number of absolute `PATH` entries and never uses a shell.

## Remote development

In Remote-SSH, Dev Containers, and WSL, the workspace extension host is remote. Build or place the server there, configure the remote machine setting, and use paths that exist in that environment. A path on the local UI machine is not reused remotely. Virtual and untrusted workspaces do not receive MCP definitions.

## Direct client configuration

A standard MCP client that accepts a `stdio` command can launch the same DLL with an argument array equivalent to:

```json
{
  "command": "dotnet",
  "args": [
    "/opt/adr-guard/adr-guard-mcp.dll",
    "--repository", "/work/project",
    "--adr-directory", "docs/adr"
  ]
}
```

This shape describes the server contract; client-specific file names and approval controls vary. The repository currently verifies registration through the VS Code extension provider, not through independently maintained configuration files for other clients. Do not copy secrets into the command, arguments, or environment unless a client independently requires them; ADR Guard MCP requires none.

## Troubleshooting

- **Not configured:** set an absolute server path and explicitly enable the folder.
- **Executable not found:** confirm the file exists on the workspace extension host; for a DLL, confirm `dotnet` is on that host's `PATH`.
- **Invalid configuration:** use the exact filename, a regular non-symlink file outside all open workspaces, and a supported local absolute path. UNC paths are rejected on Windows.
- **No definition:** trust the workspace, use a local `file:` folder, save the folder-scoped enablement, and check the machine-scoped path.
- **Server exits with usage error:** verify the three supported options and ensure their values are separate arguments.
- **Tool timeout or oversized response:** narrow the request or set `--timeout-seconds` within the supported range; the 1 MiB response limit is fixed.
