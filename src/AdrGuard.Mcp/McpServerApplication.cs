using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace AdrGuard.Mcp;

internal static class McpServerApplication
{
    internal static async Task<int> RunAsync(
        string[] args,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var host = Build(args);
            await host.RunAsync(cancellationToken).ConfigureAwait(false);
            return 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return 0;
        }
        catch (McpServerConfigurationException exception)
        {
            await Console.Error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            return 2;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await Console.Error.WriteLineAsync($"Unable to start ADR Guard MCP server: {exception.Message}").ConfigureAwait(false);
            return 3;
        }
    }

    internal static IHost Build(string[] args)
    {
        var options = McpServerOptions.Parse(args);
        var builder = Host.CreateApplicationBuilder([]);
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(console => console.LogToStandardErrorThreshold = LogLevel.Trace);
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<McpWorkspace>();
        builder.Services
            .AddMcpServer(server =>
            {
                server.ServerInfo = new()
                {
                    Name = "adr-guard",
                    Version = typeof(McpServerApplication).Assembly.GetName().Version?.ToString() ?? "0.0.0",
                };
            })
            .WithStdioServerTransport()
            .WithTools<AdrGuardMcpTools>();

        return builder.Build();
    }
}
