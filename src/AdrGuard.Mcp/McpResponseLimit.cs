using System.Text.Json;
using ModelContextProtocol;

namespace AdrGuard.Mcp;

internal static class McpResponseLimit
{
    internal const int MaximumResponseBytes = 1_048_576;

    internal static T Ensure<T>(T response)
    {
        if (JsonSerializer.SerializeToUtf8Bytes(response).Length > MaximumResponseBytes)
        {
            throw new McpException(
                $"ADR Guard MCP response exceeds the {MaximumResponseBytes}-byte limit. Narrow the request.");
        }

        return response;
    }
}
