using System.Text;
using System.Text.Json;

namespace AdrGuard.Agent;

internal sealed class AdrGuardMcpClient : IMcpClient, IAsyncDisposable
{
    internal const int MaximumResponseBytes = 1_048_576;
    private const string SupportedSchemaVersion = "1.0";
    private const string SchemaVersionProperty = "schemaVersion";
    private const string TotalProperty = "total";
    private const string PolicyProperty = "policy";
    private const string CoverageProperty = "coverage";
    private const string RepositoryProperty = "repository";
    private const string UncertaintyProperty = "uncertainty";
    private const string ValidateAdrsTool = "validate_adrs";
    private const string AnalyzeImpactTool = "analyze_impact";
    private const string VerifyArchitectureTool = "verify_architecture";

    private static readonly string[] AdrSummaryProperties =
        ["identifier", "title", "status", "path", "format"];

    private static readonly Dictionary<string, ToolContract> Contracts =
        new Dictionary<string, ToolContract>(StringComparer.Ordinal)
        {
            ["list_adrs"] = new([SchemaVersionProperty, TotalProperty, "adrs"]),
            ["get_adr"] = new([SchemaVersionProperty, "adr", "contentIsUntrusted", "content"], ContentIsUntrusted: true),
            ["search_adrs"] = new([SchemaVersionProperty, "query", "excerptsAreUntrusted", TotalProperty, "matches"], ContentIsUntrusted: true),
            ["get_adr_relationships"] = new([SchemaVersionProperty, "relationships", "issues"]),
            [ValidateAdrsTool] = new([SchemaVersionProperty, "valid", "files", PolicyProperty, "diagnostics"]),
            [AnalyzeImpactTool] = new([SchemaVersionProperty, "advisory", RepositoryProperty, "summary", CoverageProperty, UncertaintyProperty, "changedFiles", "decisions"]),
            [VerifyArchitectureTool] = new([SchemaVersionProperty, PolicyProperty, RepositoryProperty, CoverageProperty, "findings", "inconclusive", "policyFailed"]),
        };

    private readonly IMcpSession session;
    private readonly TimeSpan operationTimeout;

    private AdrGuardMcpClient(IMcpSession session, TimeSpan operationTimeout)
    {
        this.session = session;
        this.operationTimeout = operationTimeout;
    }

    internal static async Task<AdrGuardMcpClient> ConnectAsync(
        AgentConfiguration configuration,
        IMcpSessionFactory? sessionFactory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        sessionFactory ??= SdkMcpSessionFactory.Instance;
        var session = await sessionFactory.ConnectAsync(configuration, cancellationToken).ConfigureAwait(false);
        return new AdrGuardMcpClient(session, configuration.McpTimeout);
    }

    public Task<McpEvidence> ListAdrsAsync(CancellationToken cancellationToken) =>
        CallAsync("list_adrs", EmptyArguments, cancellationToken);

    public Task<McpEvidence> GetAdrAsync(string identifier, CancellationToken cancellationToken) =>
        CallAsync("get_adr", new Dictionary<string, object?> { ["identifier"] = identifier }, cancellationToken);

    public Task<McpEvidence> SearchAdrsAsync(string query, int limit, CancellationToken cancellationToken) =>
        CallAsync(
            "search_adrs",
            new Dictionary<string, object?> { ["query"] = query, ["limit"] = limit },
            cancellationToken);

    public Task<McpEvidence> GetAdrRelationshipsAsync(
        string? identifier,
        CancellationToken cancellationToken) =>
        CallAsync(
            "get_adr_relationships",
            OptionalArgument("identifier", identifier),
            cancellationToken);

    public Task<McpEvidence> ValidateAdrsAsync(
        string? validationProfile,
        CancellationToken cancellationToken) =>
        CallAsync(
            ValidateAdrsTool,
            OptionalArgument("validationProfile", validationProfile),
            cancellationToken);

    public Task<McpEvidence> AnalyzeImpactAsync(
        string baseReference,
        string manifestPath,
        CancellationToken cancellationToken) =>
        CallAsync(
            AnalyzeImpactTool,
            new Dictionary<string, object?>
            {
                ["baseReference"] = baseReference,
                ["manifestPath"] = manifestPath,
            },
            cancellationToken);

    public Task<McpEvidence> VerifyArchitectureAsync(
        string rulesPath,
        IReadOnlyList<string>? evidencePaths,
        string? baselinePath,
        string policy,
        CancellationToken cancellationToken)
    {
        var arguments = new Dictionary<string, object?>
        {
            ["rulesPath"] = rulesPath,
            [PolicyProperty] = policy,
        };
        if (evidencePaths is not null)
        {
            arguments["evidencePaths"] = evidencePaths;
        }

        if (baselinePath is not null)
        {
            arguments["baselinePath"] = baselinePath;
        }

        return CallAsync(VerifyArchitectureTool, arguments, cancellationToken);
    }

    public ValueTask DisposeAsync() => session.DisposeAsync();

    private static IReadOnlyDictionary<string, object?> EmptyArguments { get; } =
        new Dictionary<string, object?>();

    private static IReadOnlyDictionary<string, object?> OptionalArgument(string name, string? value) =>
        value is null
            ? EmptyArguments
            : new Dictionary<string, object?> { [name] = value };

    private async Task<McpEvidence> CallAsync(
        string toolName,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(operationTimeout);

        McpRawToolResult result;
        try
        {
            result = await session.CallToolAsync(toolName, arguments, timeoutSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception)
        {
            throw new AgentMcpException($"MCP tool '{toolName}' timed out.", exception);
        }
        catch (AgentMcpException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new AgentMcpException($"MCP tool '{toolName}' failed.", exception);
        }

        if (result.IsError)
        {
            throw new AgentMcpException($"MCP tool '{toolName}' returned an error.");
        }

        if (result.StructuredContent is not { } content)
        {
            throw new AgentMcpException($"MCP tool '{toolName}' returned no structured evidence.");
        }

        return ValidateEvidence(toolName, arguments, content);
    }

    private static McpEvidence ValidateEvidence(
        string toolName,
        IReadOnlyDictionary<string, object?> arguments,
        JsonElement content)
    {
        var contentJson = content.GetRawText();
        if (Encoding.UTF8.GetByteCount(contentJson) > MaximumResponseBytes)
        {
            throw new AgentMcpException(
                $"MCP tool '{toolName}' exceeded the {MaximumResponseBytes}-byte evidence limit.");
        }

        if (content.ValueKind != JsonValueKind.Object || !Contracts.TryGetValue(toolName, out var contract))
        {
            throw new AgentMcpException($"MCP tool '{toolName}' returned an unsupported evidence shape.");
        }

        var actualProperties = content.EnumerateObject().Select(property => property.Name).ToArray();
        if (actualProperties.Length != contract.TopLevelProperties.Count
            || !actualProperties.ToHashSet(StringComparer.Ordinal).SetEquals(contract.TopLevelProperties))
        {
            throw new AgentMcpException($"MCP tool '{toolName}' returned an unsupported evidence shape.");
        }

        if (!HasExpectedPropertyTypes(toolName, content))
        {
            throw new AgentMcpException($"MCP tool '{toolName}' returned an unsupported evidence shape.");
        }

        if (!content.TryGetProperty(SchemaVersionProperty, out var schemaVersion)
            || schemaVersion.ValueKind != JsonValueKind.String
            || schemaVersion.GetString() is not SupportedSchemaVersion)
        {
            throw new AgentMcpException(
                $"MCP tool '{toolName}' returned an unsupported schema version.");
        }

        var status = DetermineStatus(toolName, content);
        return new McpEvidence(
            toolName,
            JsonSerializer.Serialize(arguments),
            SupportedSchemaVersion,
            contentJson,
            status,
            contract.ContentIsUntrusted);
    }

    private static McpEvidenceStatus DetermineStatus(string toolName, JsonElement content)
    {
        if (toolName == AnalyzeImpactTool
            && content.GetProperty(UncertaintyProperty).ValueKind == JsonValueKind.Object
            && content.GetProperty(UncertaintyProperty).TryGetProperty("hasUnknowns", out var hasUnknowns)
            && hasUnknowns.ValueKind == JsonValueKind.True)
        {
            return McpEvidenceStatus.Inconclusive;
        }

        if (toolName == VerifyArchitectureTool)
        {
            if (content.GetProperty("inconclusive").ValueKind == JsonValueKind.True)
            {
                return McpEvidenceStatus.Inconclusive;
            }

            if (content.GetProperty("findings").GetArrayLength() > 0
                || content.GetProperty("policyFailed").ValueKind == JsonValueKind.True)
            {
                return McpEvidenceStatus.Findings;
            }
        }

        if (toolName == ValidateAdrsTool && content.GetProperty("valid").ValueKind == JsonValueKind.False)
        {
            return McpEvidenceStatus.Findings;
        }

        return McpEvidenceStatus.Complete;
    }

    private static bool HasExpectedPropertyTypes(string toolName, JsonElement content) =>
        IsKind(content, SchemaVersionProperty, JsonValueKind.String)
        && toolName switch
        {
            "list_adrs" => IsKind(content, TotalProperty, JsonValueKind.Number)
                           && IsKind(content, "adrs", JsonValueKind.Array),
            "get_adr" => IsAdrSummary(content, "adr")
                         && IsBoolean(content, "contentIsUntrusted")
                         && IsKind(content, "content", JsonValueKind.String),
            "search_adrs" => IsKind(content, "query", JsonValueKind.String)
                             && IsBoolean(content, "excerptsAreUntrusted")
                             && IsKind(content, TotalProperty, JsonValueKind.Number)
                             && IsKind(content, "matches", JsonValueKind.Array),
            "get_adr_relationships" => IsKind(content, "relationships", JsonValueKind.Array)
                                       && IsKind(content, "issues", JsonValueKind.Array),
            ValidateAdrsTool => IsBoolean(content, "valid")
                               && IsKind(content, "files", JsonValueKind.Number)
                               && IsKind(content, PolicyProperty, JsonValueKind.Object)
                               && IsKind(content, "diagnostics", JsonValueKind.Array),
            AnalyzeImpactTool => IsBoolean(content, "advisory")
                                && IsKind(content, RepositoryProperty, JsonValueKind.Object)
                                && IsKind(content, "summary", JsonValueKind.Object)
                                && IsKind(content, CoverageProperty, JsonValueKind.Object)
                                && IsKind(content, UncertaintyProperty, JsonValueKind.Object)
                                && IsBoolean(content.GetProperty(UncertaintyProperty), "hasUnknowns")
                                && IsKind(content, "changedFiles", JsonValueKind.Array)
                                && IsKind(content, "decisions", JsonValueKind.Array),
            VerifyArchitectureTool => IsKind(content, PolicyProperty, JsonValueKind.String)
                                     && IsKind(content, RepositoryProperty, JsonValueKind.Object)
                                     && IsKind(content, CoverageProperty, JsonValueKind.Object)
                                     && IsKind(content, "findings", JsonValueKind.Array)
                                     && IsBoolean(content, "inconclusive")
                                     && IsBoolean(content, "policyFailed"),
            _ => false,
        };

    private static bool IsKind(JsonElement content, string propertyName, JsonValueKind expectedKind) =>
        content.TryGetProperty(propertyName, out var value) && value.ValueKind == expectedKind;

    private static bool IsBoolean(JsonElement content, string propertyName) =>
        content.TryGetProperty(propertyName, out var value)
        && value.ValueKind is JsonValueKind.True or JsonValueKind.False;

    private static bool IsAdrSummary(JsonElement content, string propertyName)
    {
        if (!content.TryGetProperty(propertyName, out var summary)
            || summary.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var properties = summary.EnumerateObject().Select(property => property.Name).ToArray();
        return properties.Length == AdrSummaryProperties.Length
            && properties.ToHashSet(StringComparer.Ordinal).SetEquals(AdrSummaryProperties)
            && IsKind(summary, "identifier", JsonValueKind.String)
            && IsNullableString(summary, "title")
            && IsNullableString(summary, "status")
            && IsKind(summary, "path", JsonValueKind.String)
            && IsKind(summary, "format", JsonValueKind.String);
    }

    private static bool IsNullableString(JsonElement content, string propertyName) =>
        content.TryGetProperty(propertyName, out var value)
        && value.ValueKind is JsonValueKind.String or JsonValueKind.Null;

    private sealed record ToolContract(
        IReadOnlyCollection<string> TopLevelProperties,
        bool ContentIsUntrusted = false);
}
