using System.ComponentModel;
using System.Text;
using AdrGuard.Configuration;
using AdrGuard.Drift;
using AdrGuard.Git;
using AdrGuard.Impact;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AdrGuard.Mcp;

[McpServerToolType]
internal sealed class AdrGuardMcpTools(
    McpWorkspace workspace,
    McpServerOptions options,
    ILogger<AdrGuardMcpTools> logger)
{
    private static readonly Action<ILogger, string, Exception?> LogTimedOut =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(1, "McpToolTimeout"),
            "ADR Guard MCP tool {ToolName} exceeded its operation timeout.");

    private static readonly Action<ILogger, string, string, Exception?> LogFailed =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(2, "McpToolFailure"),
            "ADR Guard MCP tool {ToolName} failed safely with {ExceptionType}.");

    [McpServerTool(Name = "list_adrs", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Lists ADRs in the server's configured repository and ADR directory.")]
    public ListAdrsResult ListAdrs(CancellationToken cancellationToken) =>
        Execute("list_adrs", workspace.ListAdrs, cancellationToken);

    [McpServerTool(Name = "get_adr", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Reads one ADR by stable ID, numeric ID, or safe repository-relative path.")]
    public GetAdrResult GetAdr(
        [Description("Stable ADR ID, numeric ID, or path within the configured ADR directory.")] string identifier,
        CancellationToken cancellationToken) =>
        Execute("get_adr", token => workspace.GetAdr(identifier, token), cancellationToken);

    [McpServerTool(Name = "search_adrs", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Performs deterministic case-insensitive text search over ADR IDs, titles, and bounded content.")]
    public SearchAdrsResult SearchAdrs(
        [Description("One or more textual terms. Every term must match the selected ADR.")] string query,
        [Description("Maximum result count from 1 through 100.")] int limit = 20,
        CancellationToken cancellationToken = default) =>
        Execute("search_adrs", token => workspace.SearchAdrs(query, limit, token), cancellationToken);

    [McpServerTool(Name = "get_adr_relationships", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Returns declared ADR supersession and dependency relationships without inferring missing edges.")]
    public GetAdrRelationshipsResult GetAdrRelationships(
        [Description("Optional stable ID, numeric ID, or safe ADR path. Omit to inspect all ADRs.")] string? identifier = null,
        CancellationToken cancellationToken = default) =>
        Execute("get_adr_relationships", token => workspace.GetRelationships(identifier, token), cancellationToken);

    [McpServerTool(Name = "validate_adrs", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Runs ADR Guard deterministic validation and returns diagnostics as a successful analysis result.")]
    public ValidateAdrsResult ValidateAdrs(
        [Description("Optional validation profile override: legacy, advisory, standard, or strict.")] string? validationProfile = null,
        CancellationToken cancellationToken = default) =>
        Execute("validate_adrs", token => workspace.ValidateAdrs(validationProfile, token), cancellationToken);

    [McpServerTool(Name = "analyze_impact", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Analyzes architecture impact from an explicit local mapping and Git base reference; never fetches or changes Git state.")]
    public ImpactToolResult AnalyzeImpact(
        [Description("Explicit local Git base reference used by ADR Guard impact analysis.")] string baseReference,
        [Description("Forward-slash repository-relative impact mapping JSON path.")] string manifestPath,
        CancellationToken cancellationToken) =>
        Execute("analyze_impact", token => workspace.AnalyzeImpact(baseReference, manifestPath, token), cancellationToken);

    [McpServerTool(Name = "verify_architecture", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Verifies declared architecture rules from bounded local evidence without running tests, builds, or scripts.")]
    public DriftToolResult VerifyArchitecture(
        [Description("Forward-slash repository-relative fitness rules JSON path.")] string rulesPath,
        [Description("Optional repository-relative bounded evidence JSON paths already produced by trusted tools.")] string[]? evidencePaths = null,
        [Description("Optional repository-relative existing drift baseline path, read only.")] string? baselinePath = null,
        [Description("Advisory or enforce. Enforcement reports policy failure but does not mutate the repository.")] string policy = "advisory",
        CancellationToken cancellationToken = default) =>
        Execute(
            "verify_architecture",
            token => workspace.VerifyArchitecture(rulesPath, evidencePaths, baselinePath, policy, token),
            cancellationToken);

    private T Execute<T>(
        string toolName,
        Func<CancellationToken, T> operation,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(options.OperationTimeout);
        try
        {
            return operation(timeoutSource.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception)
        {
            LogTimedOut(logger, toolName, null);
            throw new McpException("ADR Guard MCP operation timed out.", exception);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            throw new McpProtocolException(exception.Message, McpErrorCode.InvalidParams);
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException
                                          or UnauthorizedAccessException
                                          or InvalidDataException
                                          or InvalidOperationException
                                          or DecoderFallbackException
                                          or GitOperationException
                                          or ImpactManifestException
                                          or ImpactAnalysisException
                                          or FitnessManifestException
                                          or AdrGuardConfigurationException)
        {
            LogFailed(logger, toolName, exception.GetType().Name, null);
            throw new McpException($"ADR Guard operation failed: {Sanitize(exception.Message)}", exception);
        }
    }

    private string Sanitize(string message)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var sanitized = message.Replace(options.RepositoryRoot, "<repository>", comparison);
        var slashRoot = options.RepositoryRoot.Replace(Path.DirectorySeparatorChar, '/');
        return sanitized.Replace(slashRoot, "<repository>", comparison);
    }
}
