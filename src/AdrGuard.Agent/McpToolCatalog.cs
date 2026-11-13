using ModelContextProtocol.Client;

namespace AdrGuard.Agent;

internal static class McpToolCatalog
{
    internal static readonly string[] ToolNames =
    [
        "analyze_impact",
        "get_adr",
        "get_adr_relationships",
        "list_adrs",
        "search_adrs",
        "validate_adrs",
        "verify_architecture",
    ];

    internal static void ValidateDiscoveredTools(IList<McpClientTool> tools)
    {
        ValidateAvailableToolNames(tools.Select(tool => tool.Name));

        if (tools.Any(tool => tool.ProtocolTool.Annotations?.ReadOnlyHint is not true
                              || tool.ProtocolTool.Annotations?.DestructiveHint is true
                              || tool.ProtocolTool.OutputSchema is null))
        {
            throw new AgentMcpException(
                "The configured MCP server tool catalog is missing required read-only annotations or output schemas.");
        }
    }

    internal static void ValidateAvailableToolNames(IEnumerable<string> toolNames)
    {
        ArgumentNullException.ThrowIfNull(toolNames);

        var actualNames = toolNames.Order(StringComparer.Ordinal).ToArray();
        if (!actualNames.SequenceEqual(ToolNames, StringComparer.Ordinal))
        {
            throw new AgentMcpException(
                "The configured MCP server does not expose the required read-only tool catalog.");
        }
    }
}
