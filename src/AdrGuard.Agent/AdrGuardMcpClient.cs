using System.Text;
using System.Text.Json;

namespace AdrGuard.Agent;

internal sealed class AdrGuardMcpClient : IMcpClient, IAsyncDisposable
{
    internal const int MaximumResponseBytes = 1_048_576;
    private const string SupportedSchemaVersion = "1.0";

    private static readonly Dictionary<string, ToolContract> Contracts =
        new Dictionary<string, ToolContract>(StringComparer.Ordinal)
        {
            ["list_adrs"] = new(["schemaVersion", "total", "adrs"]),
            ["get_adr"] = new(["schemaVersion", "adr", "contentIsUntrusted", "content"], ContentIsUntrusted: true),
            ["search_adrs"] = new(["schemaVersion", "query", "excerptsAreUntrusted", "total", "matches"], ContentIsUntrusted: true),
            ["get_adr_relationships"] = new(["schemaVersion", "relationships", "issues"]),
            ["validate_adrs"] = new(["schemaVersion", "valid", "files", "policy", "diagnostics"]),
            ["analyze_impact"] = new(["schemaVersion", "advisory", "repository", "summary", "coverage", "uncertainty", "changedFiles", "decisions"]),
            ["verify_architecture"] = new(["schemaVersion", "policy", "repository", "coverage", "findings", "inconclusive", "policyFailed"]),
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
            "validate_adrs",
            OptionalArgument("validationProfile", validationProfile),
            cancellationToken);

    public Task<McpEvidence> AnalyzeImpactAsync(
        string baseReference,
        string manifestPath,
        CancellationToken cancellationToken) =>
        CallAsync(
            "analyze_impact",
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
            ["policy"] = policy,
        };
        if (evidencePaths is not null)
        {
            arguments["evidencePaths"] = evidencePaths;
        }

        if (baselinePath is not null)
        {
            arguments["baselinePath"] = baselinePath;
        }

        return CallAsync("verify_architecture", arguments, cancellationToken);
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

        if (!content.TryGetProperty("schemaVersion", out var schemaVersion)
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
        if (toolName == "analyze_impact"
            && content.GetProperty("uncertainty").ValueKind == JsonValueKind.Object
            && content.GetProperty("uncertainty").TryGetProperty("hasUnknowns", out var hasUnknowns)
            && hasUnknowns.ValueKind == JsonValueKind.True)
        {
            return McpEvidenceStatus.Inconclusive;
        }

        if (toolName == "verify_architecture")
        {
            if (content.GetProperty("inconclusive").ValueKind == JsonValueKind.True)
            {
                return McpEvidenceStatus.Inconclusive;
            }

            if (content.GetProperty("policyFailed").ValueKind == JsonValueKind.True)
            {
                return McpEvidenceStatus.Findings;
            }
        }

        if (toolName == "validate_adrs" && content.GetProperty("valid").ValueKind == JsonValueKind.False)
        {
            return McpEvidenceStatus.Findings;
        }

        return McpEvidenceStatus.Complete;
    }

    private static bool HasExpectedPropertyTypes(string toolName, JsonElement content) =>
        IsKind(content, "schemaVersion", JsonValueKind.String)
        && toolName switch
        {
            "list_adrs" => IsKind(content, "total", JsonValueKind.Number)
                           && IsKind(content, "adrs", JsonValueKind.Array),
            "get_adr" => IsKind(content, "adr", JsonValueKind.Object)
                         && IsBoolean(content, "contentIsUntrusted")
                         && IsKind(content, "content", JsonValueKind.String),
            "search_adrs" => IsKind(content, "query", JsonValueKind.String)
                             && IsBoolean(content, "excerptsAreUntrusted")
                             && IsKind(content, "total", JsonValueKind.Number)
                             && IsKind(content, "matches", JsonValueKind.Array),
            "get_adr_relationships" => IsKind(content, "relationships", JsonValueKind.Array)
                                       && IsKind(content, "issues", JsonValueKind.Array),
            "validate_adrs" => IsBoolean(content, "valid")
                               && IsKind(content, "files", JsonValueKind.Number)
                               && IsKind(content, "policy", JsonValueKind.Object)
                               && IsKind(content, "diagnostics", JsonValueKind.Array),
            "analyze_impact" => IsBoolean(content, "advisory")
                                && IsKind(content, "repository", JsonValueKind.Object)
                                && IsKind(content, "summary", JsonValueKind.Object)
                                && IsKind(content, "coverage", JsonValueKind.Object)
                                && IsKind(content, "uncertainty", JsonValueKind.Object)
                                && IsKind(content, "changedFiles", JsonValueKind.Array)
                                && IsKind(content, "decisions", JsonValueKind.Array),
            "verify_architecture" => IsKind(content, "policy", JsonValueKind.String)
                                     && IsKind(content, "repository", JsonValueKind.Object)
                                     && IsKind(content, "coverage", JsonValueKind.Object)
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

    private sealed record ToolContract(
        IReadOnlyCollection<string> TopLevelProperties,
        bool ContentIsUntrusted = false);
}
