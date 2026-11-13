using System.Diagnostics.CodeAnalysis;

namespace AdrGuard.Agent;

internal sealed record AgentConfiguration(
    string RepositoryRoot,
    string AdrDirectory,
    string McpCommand,
    IReadOnlyList<string> McpArguments,
    TimeSpan McpTimeout,
    string? Provider,
    string? Model,
    string Question,
    bool AllowRemoteContext,
    AgentLimits Limits,
    string? ImpactManifestPath,
    string ImpactBaseReference,
    string? ArchitectureRulesPath,
    IReadOnlyList<string> ArchitectureEvidencePaths,
    string? ArchitectureBaselinePath,
    string ArchitecturePolicy,
    AdrProposalConfiguration? AdrProposal = null)
{
    private const int DefaultMcpTimeoutSeconds = 30;
    private const int DefaultCliTimeoutSeconds = 30;
    private const int MaximumQuestionCharacters = 4_096;

    [SuppressMessage(
        "Maintainability",
        "S3776:Cognitive Complexity of methods should not be too high",
        Justification = "The explicit CLI option table keeps parsing and validation auditable at one security boundary.")]
    internal static AgentConfiguration Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        string? repository = null;
        string? adrDirectory = null;
        string? mcpCommand = null;
        var mcpArguments = new List<string>();
        var mcpTimeoutSeconds = DefaultMcpTimeoutSeconds;
        string? provider = null;
        string? model = null;
        string? question = null;
        var allowRemoteContext = false;
        var maxIterations = AgentLimits.Default.MaxIterations;
        var maxContextCharacters = AgentLimits.Default.MaxContextCharacters;
        var maxOutputTokens = AgentLimits.Default.MaxOutputTokens;
        var maxDurationSeconds = (int)AgentLimits.Default.MaxDuration.TotalSeconds;
        var maxToolCalls = AgentLimits.Default.MaxToolCalls;
        var searchLimit = AgentLimits.Default.SearchLimit;
        string? impactManifestPath = null;
        var impactBaseReference = "HEAD";
        string? architectureRulesPath = null;
        var architectureEvidencePaths = new List<string>();
        string? architectureBaselinePath = null;
        var architecturePolicy = "advisory";
        string? cliCommand = null;
        var cliArguments = new List<string>();
        var cliTimeoutSeconds = DefaultCliTimeoutSeconds;
        var allowAdrCreation = false;

        var index = 0;
        while (index < args.Count)
        {
            var option = args[index++];
            if (option == "--allow-remote-context")
            {
                if (allowRemoteContext)
                {
                    throw new AgentConfigurationException(
                        "Option '--allow-remote-context' may only be specified once.");
                }

                allowRemoteContext = true;
                continue;
            }

            if (option == "--enable-adr-creation")
            {
                if (allowAdrCreation)
                {
                    throw new AgentConfigurationException(
                        "Option '--enable-adr-creation' may only be specified once.");
                }

                allowAdrCreation = true;
                continue;
            }

            if (!ValueOptions.Contains(option, StringComparer.Ordinal))
            {
                throw new AgentConfigurationException($"Unknown argument '{option}'.");
            }

            if (index >= args.Count || string.IsNullOrWhiteSpace(args[index]))
            {
                throw new AgentConfigurationException($"Option '{option}' requires a value.");
            }

            var value = args[index++];
            switch (option)
            {
                case "--repository":
                    AssignOnce(ref repository, value, option);
                    break;
                case "--adr-directory":
                    AssignOnce(ref adrDirectory, value, option);
                    break;
                case "--mcp-command":
                    AssignOnce(ref mcpCommand, value, option);
                    break;
                case "--mcp-argument":
                    mcpArguments.Add(value);
                    break;
                case "--mcp-timeout-seconds":
                    mcpTimeoutSeconds = ParseBoundedInteger(option, value, 1, 300);
                    break;
                case "--provider":
                    AssignOnce(ref provider, value, option);
                    break;
                case "--model":
                    AssignOnce(ref model, value, option);
                    break;
                case "--question":
                    AssignOnce(ref question, value, option);
                    break;
                case "--max-iterations":
                    maxIterations = ParseBoundedInteger(option, value, 1, 10);
                    break;
                case "--max-context-characters":
                    maxContextCharacters = ParseBoundedInteger(option, value, 1_024, 1_048_576);
                    break;
                case "--max-output-tokens":
                    maxOutputTokens = ParseBoundedInteger(option, value, 1, 16_384);
                    break;
                case "--max-duration-seconds":
                    maxDurationSeconds = ParseBoundedInteger(option, value, 1, 600);
                    break;
                case "--max-tool-calls":
                    maxToolCalls = ParseBoundedInteger(option, value, 1, 20);
                    break;
                case "--search-limit":
                    searchLimit = ParseBoundedInteger(option, value, 1, 100);
                    break;
                case "--impact-manifest":
                    AssignOnce(ref impactManifestPath, value, option);
                    break;
                case "--impact-base-reference":
                    impactBaseReference = value;
                    break;
                case "--architecture-rules":
                    AssignOnce(ref architectureRulesPath, value, option);
                    break;
                case "--architecture-evidence":
                    architectureEvidencePaths.Add(value);
                    break;
                case "--architecture-baseline":
                    AssignOnce(ref architectureBaselinePath, value, option);
                    break;
                case "--architecture-policy" when value is "advisory" or "enforce":
                    architecturePolicy = value;
                    break;
                case "--architecture-policy":
                    throw new AgentConfigurationException(
                        "Option '--architecture-policy' must be 'advisory' or 'enforce'.");
                case "--cli-command":
                    AssignOnce(ref cliCommand, value, option);
                    break;
                case "--cli-argument":
                    cliArguments.Add(value);
                    break;
                case "--cli-timeout-seconds":
                    cliTimeoutSeconds = ParseBoundedInteger(option, value, 1, 300);
                    break;
            }
        }

        if (repository is null || adrDirectory is null || mcpCommand is null)
        {
            throw new AgentConfigurationException(
                "Options '--repository', '--adr-directory', and '--mcp-command' are required.");
        }

        if ((provider is null) != (model is null))
        {
            throw new AgentConfigurationException(
                "Options '--provider' and '--model' must be specified together.");
        }

        if (provider is not null && !string.Equals(provider, "openai", StringComparison.Ordinal))
        {
            throw new AgentConfigurationException(
                "Unsupported provider. The first functional flow supports only the explicitly selected provider 'openai'.");
        }

        if (model is not null && (model.Length > 256 || model.Any(char.IsControl)))
        {
            throw new AgentConfigurationException(
                "Option '--model' must contain printable text and not exceed 256 characters.");
        }

        if (question is null)
        {
            throw new AgentConfigurationException("Option '--question' is required.");
        }

        if (question.Length > MaximumQuestionCharacters || question.Any(char.IsControl))
        {
            throw new AgentConfigurationException(
                $"Option '--question' must contain printable text and not exceed {MaximumQuestionCharacters} characters.");
        }

        if (provider is not null && !allowRemoteContext)
        {
            throw new AgentConfigurationException(
                "Remote context sharing is disabled. Pass '--allow-remote-context' to authorize sending the question and selected MCP evidence to the configured provider.");
        }
        if (provider is null && allowRemoteContext)
        {
            throw new AgentConfigurationException(
                "Option '--allow-remote-context' requires '--provider' and '--model'.");
        }

        var repositoryRoot = GetExistingRepository(repository);
        var normalizedAdrDirectory = ValidateAdrDirectory(repositoryRoot, adrDirectory);
        ValidateCommand(mcpCommand, "MCP");
        if (cliCommand is null && cliArguments.Count > 0)
        {
            throw new AgentConfigurationException(
                "Option '--cli-argument' requires '--cli-command'.");
        }
        if (cliCommand is not null)
        {
            ValidateCommand(cliCommand, "CLI");
        }
        if (allowAdrCreation && cliCommand is null)
        {
            throw new AgentConfigurationException(
                "Option '--enable-adr-creation' requires '--cli-command'.");
        }

        return new AgentConfiguration(
            repositoryRoot,
            normalizedAdrDirectory,
            mcpCommand,
            mcpArguments.AsReadOnly(),
            TimeSpan.FromSeconds(mcpTimeoutSeconds),
            provider,
            model,
            question,
            allowRemoteContext,
            new AgentLimits(
                maxIterations,
                maxContextCharacters,
                maxOutputTokens,
                TimeSpan.FromSeconds(maxDurationSeconds),
                maxToolCalls,
                searchLimit),
            NormalizeOptionalRepositoryPath(repositoryRoot, impactManifestPath, "impact manifest"),
            impactBaseReference,
            NormalizeOptionalRepositoryPath(repositoryRoot, architectureRulesPath, "architecture rules"),
            architectureEvidencePaths
                .Select(path => NormalizeRepositoryPath(repositoryRoot, path, "architecture evidence"))
                .ToArray(),
            NormalizeOptionalRepositoryPath(repositoryRoot, architectureBaselinePath, "architecture baseline"),
            architecturePolicy,
            cliCommand is null
                ? null
                : new AdrProposalConfiguration(
                    cliCommand,
                    cliArguments.AsReadOnly(),
                    TimeSpan.FromSeconds(cliTimeoutSeconds),
                    allowAdrCreation));
    }

    private static string[] ValueOptions { get; } =
    [
        "--repository",
        "--adr-directory",
        "--mcp-command",
        "--mcp-argument",
        "--mcp-timeout-seconds",
        "--provider",
        "--model",
        "--question",
        "--max-iterations",
        "--max-context-characters",
        "--max-output-tokens",
        "--max-duration-seconds",
        "--max-tool-calls",
        "--search-limit",
        "--impact-manifest",
        "--impact-base-reference",
        "--architecture-rules",
        "--architecture-evidence",
        "--architecture-baseline",
        "--architecture-policy",
        "--cli-command",
        "--cli-argument",
        "--cli-timeout-seconds",
    ];

    private static string GetExistingRepository(string repository)
    {
        string repositoryRoot;
        try
        {
            repositoryRoot = Path.GetFullPath(repository);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
        {
            throw new AgentConfigurationException("The configured repository path is invalid.", exception);
        }

        if (!Directory.Exists(repositoryRoot))
        {
            throw new AgentConfigurationException("The configured repository does not exist.");
        }

        return repositoryRoot;
    }

    private static string ValidateAdrDirectory(string repositoryRoot, string adrDirectory)
    {
        var relativeDirectory = NormalizeRepositoryPath(repositoryRoot, adrDirectory, "ADR directory");
        if (!Directory.Exists(Path.Combine(repositoryRoot, relativeDirectory)))
        {
            throw new AgentConfigurationException("The configured ADR directory does not exist.");
        }

        return relativeDirectory;
    }

    private static string? NormalizeOptionalRepositoryPath(
        string repositoryRoot,
        string? path,
        string description) =>
        path is null ? null : NormalizeRepositoryPath(repositoryRoot, path, description);

    private static string NormalizeRepositoryPath(
        string repositoryRoot,
        string path,
        string description)
    {
        if (Path.IsPathRooted(path))
        {
            throw new AgentConfigurationException(
                $"The configured {description} must be relative to the repository.");
        }

        try
        {
            var fullPath = Path.GetFullPath(path, repositoryRoot);
            var relativePath = Path.GetRelativePath(repositoryRoot, fullPath);
            if (relativePath == ".."
                || relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || Path.IsPathRooted(relativePath))
            {
                throw new AgentConfigurationException(
                    $"The configured {description} must remain within the repository.");
            }

            return relativePath.Replace(Path.DirectorySeparatorChar, '/');
        }
        catch (AgentConfigurationException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
        {
            throw new AgentConfigurationException($"The configured {description} path is invalid.", exception);
        }
    }

    private static void ValidateCommand(string command, string component)
    {
        if (!Path.IsPathRooted(command))
        {
            return;
        }

        try
        {
            if (!File.Exists(Path.GetFullPath(command)))
            {
                throw new AgentConfigurationException(
                    $"The configured {component} executable does not exist.");
            }
        }
        catch (AgentConfigurationException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
        {
            throw new AgentConfigurationException(
                $"The configured {component} executable path is invalid.",
                exception);
        }
    }

    private static int ParseBoundedInteger(string option, string value, int minimum, int maximum)
    {
        if (!int.TryParse(value, out var parsed) || parsed < minimum || parsed > maximum)
        {
            throw new AgentConfigurationException(
                $"Option '{option}' must be an integer from {minimum} through {maximum}.");
        }

        return parsed;
    }

    private static void AssignOnce(ref string? destination, string value, string option)
    {
        if (destination is not null)
        {
            throw new AgentConfigurationException($"Option '{option}' may only be specified once.");
        }

        destination = value;
    }
}

internal sealed record AdrProposalConfiguration(
    string CliCommand,
    IReadOnlyList<string> CliArguments,
    TimeSpan CliTimeout,
    bool AllowCreate = false);

internal sealed record AgentLimits(
    int MaxIterations,
    int MaxContextCharacters,
    int MaxOutputTokens,
    TimeSpan MaxDuration,
    int MaxToolCalls,
    int SearchLimit)
{
    internal static AgentLimits Default { get; } = new(
        MaxIterations: 1,
        MaxContextCharacters: 65_536,
        MaxOutputTokens: 2_048,
        MaxDuration: TimeSpan.FromSeconds(120),
        MaxToolCalls: 4,
        SearchLimit: 20);
}

[SuppressMessage(
    "Design",
    "S3871:Exception types should be public",
    Justification = "The development-only agent keeps its CLI parsing contract internal.")]
internal sealed class AgentConfigurationException : Exception
{
    internal AgentConfigurationException(string message)
        : base(message)
    {
    }

    internal AgentConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
