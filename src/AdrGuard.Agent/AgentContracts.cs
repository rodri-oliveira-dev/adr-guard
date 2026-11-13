using System.Diagnostics.CodeAnalysis;

namespace AdrGuard.Agent;

internal interface IAgentRunner
{
    Task<AgentRunResult> RunAsync(
        AgentConfiguration configuration,
        CancellationToken cancellationToken);
}

internal interface IMcpClient
{
    Task<McpEvidence> ListAdrsAsync(CancellationToken cancellationToken);

    Task<McpEvidence> GetAdrAsync(string identifier, CancellationToken cancellationToken);

    Task<McpEvidence> SearchAdrsAsync(string query, int limit, CancellationToken cancellationToken);

    Task<McpEvidence> GetAdrRelationshipsAsync(string? identifier, CancellationToken cancellationToken);

    Task<McpEvidence> ValidateAdrsAsync(string? validationProfile, CancellationToken cancellationToken);

    Task<McpEvidence> AnalyzeImpactAsync(
        string baseReference,
        string manifestPath,
        CancellationToken cancellationToken);

    Task<McpEvidence> VerifyArchitectureAsync(
        string rulesPath,
        IReadOnlyList<string>? evidencePaths,
        string? baselinePath,
        string policy,
        CancellationToken cancellationToken);
}

internal interface ILanguageModel
{
    Task<LanguageModelResponse> CompleteAsync(
        LanguageModelRequest request,
        CancellationToken cancellationToken);
}

internal interface IArchitectureQuestionToolSelector
{
    ArchitectureToolSelection Select(
        AgentConfiguration configuration,
        ArchitectureWorkflow workflow);
}

internal interface IArchitectureWorkflowSelector
{
    ArchitectureWorkflow Select(string question);
}

internal interface IWorkflowInstructionLoader
{
    Task<WorkflowInstructionLoadResult> LoadAsync(
        AgentConfiguration configuration,
        ArchitectureWorkflow workflow,
        CancellationToken cancellationToken);
}

internal interface IAdrProposalPreviewClient
{
    Task<AdrFormDescription> DescribeAsync(CancellationToken cancellationToken);

    Task<AdrFormPreview> PreviewAsync(
        string title,
        IReadOnlyDictionary<string, string> fields,
        CancellationToken cancellationToken);

    Task<AdrFormCreation> CreateAsync(
        AdrCreationProposal proposal,
        CancellationToken cancellationToken);
}

internal interface IAdrProposalAuthorization
{
    Task<AdrProposalAuthorizationDecision> AuthorizeAsync(
        AdrCreationProposal proposal,
        string preview,
        CancellationToken cancellationToken);
}

internal sealed record AgentRunResult(string Message);

internal sealed record McpEvidence(
    string ToolName,
    string ArgumentsJson,
    string SchemaVersion,
    string ContentJson,
    McpEvidenceStatus Status,
    bool ContentIsUntrusted);

internal enum McpEvidenceStatus
{
    Complete,
    Findings,
    Inconclusive,
}

internal sealed record LanguageModelRequest(
    string Provider,
    string Model,
    string Prompt,
    int MaxOutputTokens);

internal sealed record LanguageModelResponse(
    string Content,
    string Provider,
    string Model);

internal sealed record ArchitectureToolSelection(
    IReadOnlyList<ArchitectureToolRequest> Requests,
    IReadOnlyList<string> MissingInformation);

internal sealed record ArchitectureWorkflow(
    ArchitectureWorkflowKind Kind,
    string Name,
    string SkillName,
    string SkillPath);

internal sealed record WorkflowInstructionLoadResult(
    string? Content,
    string? MissingInformation);

internal sealed record AdrFormDescription(
    string Template,
    IReadOnlyList<AdrFormField> Fields);

internal sealed record AdrFormField(string Id, bool Required);

internal sealed record AdrFormPreview(string Path, string Markdown);

internal sealed record AdrFormCreation(string Path);

internal sealed record AdrCreationProposal(
    string Fingerprint,
    string TargetDirectory,
    string CliCommand,
    IReadOnlyList<string> CliArguments,
    string Template,
    string Title,
    IReadOnlyDictionary<string, string> Fields,
    AdrFormPreview Preview);

internal enum AdrProposalAuthorizationDecision
{
    Denied,
    Canceled,
    Approved,
}

internal enum ArchitectureWorkflowKind
{
    IdentifyDecisionToRecord,
    AnalyzeTradeOffs,
    ConsultAndValidateAdrs,
    AdvisoryTechnicalReview,
    PrepareAdrProposal,
}

internal sealed record ArchitectureToolRequest(
    ArchitectureToolKind Kind,
    string? Argument = null);

internal enum ArchitectureToolKind
{
    ListAdrs,
    GetAdr,
    SearchAdrs,
    GetAdrRelationships,
    ValidateAdrs,
    AnalyzeImpact,
    VerifyArchitecture,
}

internal sealed record ArchitectureAnswer(
    string Summary,
    IReadOnlyList<ArchitectureReference> References,
    IReadOnlyList<string> TradeOffs,
    IReadOnlyList<string> Uncertainties,
    IReadOnlyList<string> MissingInformation);

internal sealed record ArchitectureReference(string EvidenceId, string Claim);

internal sealed record AdrProposal(
    string Title,
    string Status,
    IReadOnlyDictionary<string, string> Fields,
    IReadOnlyList<string> Alternatives,
    IReadOnlyList<string> Requirements,
    IReadOnlyList<string> Risks,
    IReadOnlyList<ArchitectureReference> References,
    IReadOnlyList<string> Uncertainties,
    IReadOnlyList<string> MissingInformation);

[SuppressMessage(
    "Design",
    "S3871:Exception types should be public",
    Justification = "The development-only agent keeps all implementation contracts internal.")]
internal class AgentExecutionException : Exception
{
    internal AgentExecutionException(string message)
        : base(message)
    {
    }

    internal AgentExecutionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

internal sealed class AgentMcpException : AgentExecutionException
{
    internal AgentMcpException(string message)
        : base(message)
    {
    }

    internal AgentMcpException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

internal sealed class AgentLanguageModelException : AgentExecutionException
{
    internal AgentLanguageModelException(string message)
        : base(message)
    {
    }

    internal AgentLanguageModelException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

internal sealed class AgentCliException : AgentExecutionException
{
    internal AgentCliException(string message)
        : base(message)
    {
    }

    internal AgentCliException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
