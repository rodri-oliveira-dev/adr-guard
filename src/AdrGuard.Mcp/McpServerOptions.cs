using AdrGuard.Configuration;

namespace AdrGuard.Mcp;

internal sealed record McpServerOptions(
    string RepositoryRoot,
    string AdrDirectory,
    TimeSpan OperationTimeout)
{
    private const int DefaultTimeoutSeconds = 30;

    internal static McpServerOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        string repository = Directory.GetCurrentDirectory();
        string? adrDirectory = null;
        var timeoutSeconds = DefaultTimeoutSeconds;
        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            if (argument is not ("--repository" or "--adr-directory" or "--timeout-seconds")
                || index + 1 >= args.Count)
            {
                throw new McpServerConfigurationException(
                    "Usage: adr-guard-mcp [--repository <path>] [--adr-directory <relative-path>] [--timeout-seconds <1-300>].");
            }

            var value = args[++index];
            switch (argument)
            {
                case "--repository":
                    repository = value;
                    break;
                case "--adr-directory":
                    adrDirectory = value;
                    break;
                case "--timeout-seconds" when int.TryParse(value, out var parsed)
                                                   && parsed is >= 1 and <= 300:
                    timeoutSeconds = parsed;
                    break;
                default:
                    throw new McpServerConfigurationException(
                        "MCP operation timeout must be an integer from 1 through 300 seconds.");
            }
        }

        var root = Path.GetFullPath(repository);
        if (!Directory.Exists(root))
        {
            throw new McpServerConfigurationException("The configured MCP repository does not exist.");
        }

        try
        {
            _ = RepositoryPath.ResolveContained(root, ".", allowMissingLeaf: false);
            var configuration = AdrGuardConfigurationLoader.Load(root);
            var selectedDirectory = adrDirectory
                ?? configuration?.AdrDirectory
                ?? (Directory.Exists(Path.Combine(root, "docs", "adr")) ? "docs/adr" : ".");
            var normalized = RepositoryPath.NormalizeRelative(root, selectedDirectory);
            var fullDirectory = RepositoryPath.ResolveContained(root, normalized, allowMissingLeaf: false);
            if (!Directory.Exists(fullDirectory))
            {
                throw new McpServerConfigurationException("The configured MCP ADR directory does not exist.");
            }

            return new McpServerOptions(root, normalized, TimeSpan.FromSeconds(timeoutSeconds));
        }
        catch (McpServerConfigurationException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException
                                          or IOException
                                          or UnauthorizedAccessException
                                          or AdrGuardConfigurationException)
        {
            throw new McpServerConfigurationException(
                $"The configured MCP workspace is invalid: {exception.Message}",
                exception);
        }
    }
}

internal sealed class McpServerConfigurationException : Exception
{
    internal McpServerConfigurationException(string message)
        : base(message)
    {
    }

    internal McpServerConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
