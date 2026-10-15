# ADR Guard MCP server (source only)

This project is the local, read-only MCP adapter for ADR Guard. It is implemented and tested from source on the `feat/mcp-server-integration` branch; it is not an officially published package or distribution.

For local development after a Release build, configure an MCP client to run:

```text
dotnet src/AdrGuard.Mcp/bin/Release/net10.0/adr-guard-mcp.dll --repository <repository> [--adr-directory <relative-path>]
```

The server uses only the `stdio` transport. Its process stdout is reserved for MCP messages; operational logging is written to stderr. The repository root is fixed at startup, all tools are read-only, and paths accepted by tools are repository-relative.

Optional startup argument `--timeout-seconds` accepts values from 1 through 300 and defaults to 30 seconds.

See the bilingual [MCP overview](../../docs/mcp.md), [configuration](../../docs/mcp-configuration.md), [tool contracts](../../docs/mcp-tools.md), and [security model](../../docs/mcp-security.md) for the complete source-build documentation and publication status.
