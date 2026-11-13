using System.Text.Json;
using ModelContextProtocol.Client;

namespace AdrGuard.Agent;

internal interface IMcpSession : IAsyncDisposable
{
    Task<McpRawToolResult> CallToolAsync(
        string toolName,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken);
}

internal interface IMcpSessionFactory
{
    Task<IMcpSession> ConnectAsync(
        AgentConfiguration configuration,
        CancellationToken cancellationToken);
}

internal sealed record McpRawToolResult(bool IsError, JsonElement? StructuredContent);

internal sealed class SdkMcpSessionFactory : IMcpSessionFactory
{
    internal static SdkMcpSessionFactory Instance { get; } = new();

    public async Task<IMcpSession> ConnectAsync(
        AgentConfiguration configuration,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "ADR Guard Agent",
            Command = configuration.McpCommand,
            Arguments =
            [
                .. configuration.McpArguments,
                "--repository",
                configuration.RepositoryRoot,
                "--adr-directory",
                configuration.AdrDirectory,
                "--timeout-seconds",
                ((int)configuration.McpTimeout.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ],
            WorkingDirectory = configuration.RepositoryRoot,
            InheritEnvironmentVariables = false,
            EnvironmentVariables = StdioClientTransportOptions.GetDefaultEnvironmentVariables(),
            ShutdownTimeout = TimeSpan.FromSeconds(5),
        });

        McpClient? client = null;
        try
        {
            client = await McpClient.CreateAsync(
                transport,
                new McpClientOptions { InitializationTimeout = configuration.McpTimeout },
                cancellationToken: cancellationToken).ConfigureAwait(false);
            var tools = await client.ListToolsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            McpToolCatalog.ValidateDiscoveredTools(tools);
            return new SdkMcpSession(client);
        }
        catch (OperationCanceledException)
        {
            if (client is not null)
            {
                await client.DisposeAsync().ConfigureAwait(false);
            }

            throw;
        }
        catch (Exception exception)
        {
            if (client is not null)
            {
                await client.DisposeAsync().ConfigureAwait(false);
            }

            throw new AgentMcpException("Unable to start or validate the configured MCP server.", exception);
        }
    }

    private sealed class SdkMcpSession(McpClient client) : IMcpSession
    {
        public async Task<McpRawToolResult> CallToolAsync(
            string toolName,
            IReadOnlyDictionary<string, object?> arguments,
            CancellationToken cancellationToken)
        {
            var result = await client.CallToolAsync(
                toolName,
                arguments,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return new McpRawToolResult(result.IsError is true, result.StructuredContent?.Clone());
        }

        public ValueTask DisposeAsync() => client.DisposeAsync();
    }
}
