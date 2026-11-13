using Xunit;

namespace AdrGuard.Agent.Tests;

public sealed class ArchitectureWorkflowTests
{
    [Theory]
    [InlineData("Should we record this decision as an ADR?", "IdentifyDecisionToRecord", "adr-guard-when-to-record")]
    [InlineData("Quando devemos registrar esta decisão?", "IdentifyDecisionToRecord", "adr-guard-when-to-record")]
    [InlineData("Compare options and their trade-offs", "AnalyzeTradeOffs", "adr-guard-tradeoff-analysis")]
    [InlineData("Comparar opções e seus prós e contras", "AnalyzeTradeOffs", "adr-guard-tradeoff-analysis")]
    [InlineData("Validate ADR-7 and explain its diagnostics", "ConsultAndValidateAdrs", "adr-guard-validate")]
    [InlineData("Consultar e validar a ADR-7", "ConsultAndValidateAdrs", "adr-guard-validate")]
    [InlineData("Perform a technical review of ADR-7", "AdvisoryTechnicalReview", "adr-guard-technical-review")]
    [InlineData("Fazer uma revisão técnica da ADR-7", "AdvisoryTechnicalReview", "adr-guard-technical-review")]
    [InlineData("Prepare an ADR proposal for the boundary", "PrepareAdrProposal", "adr-guard-create")]
    [InlineData("Preparar uma proposta de ADR para a fronteira", "PrepareAdrProposal", "adr-guard-create")]
    public void SelectsOneControlledWorkflow(
        string question,
        string expectedKind,
        string expectedSkill)
    {
        var workflow = ArchitectureWorkflowSelector.Instance.Select(question);

        Assert.Equal(expectedKind, workflow.Kind.ToString());
        Assert.Equal(expectedSkill, workflow.SkillName);
        Assert.Equal($"skills/{expectedSkill}/SKILL.md", workflow.SkillPath);
    }

    [Fact]
    public void CatalogAuthorizesExactlyTheElevenExistingAgentSkills()
    {
        var skillsRoot = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "../../../../../skills"));
        var existingSkillNames = Directory
            .EnumerateDirectories(skillsRoot, "adr-guard-*", SearchOption.TopDirectoryOnly)
            .Where(directory => File.Exists(Path.Combine(directory, "SKILL.md")))
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            existingSkillNames,
            ArchitectureWorkflowCatalog.AuthorizedSkillNames.Order(StringComparer.Ordinal));
        Assert.Equal(11, existingSkillNames.Length);
    }

    [Fact]
    public async Task OnlySelectedWorkflowInstructionsReachTheModel()
    {
        var mcp = new FakeMcpClient();
        var model = new FakeLanguageModel();
        var loader = new RecordingInstructionLoader();
        var runner = new ArchitectureQuestionRunner(
            mcp,
            model,
            instructionLoader: loader);

        var result = await runner.RunAsync(
            CreateConfiguration("Compare options and their trade-offs"),
            TestContext.Current.CancellationToken);

        Assert.Equal(["adr-guard-tradeoff-analysis"], loader.LoadedSkills);
        var request = Assert.Single(model.Requests);
        Assert.Contains("SELECTED_TRADEOFF_INSTRUCTIONS", request.Prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("adr-guard-create", request.Prompt, StringComparison.Ordinal);
        Assert.Contains("adr-guard-tradeoff-analysis", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingWorkflowInstructionsRemainExplicitAndSkipModel()
    {
        var model = new FakeLanguageModel();
        var runner = new ArchitectureQuestionRunner(
            new FakeMcpClient(),
            model,
            instructionLoader: new MissingInstructionLoader());

        var result = await runner.RunAsync(
            CreateConfiguration("Prepare an ADR proposal for the boundary"),
            TestContext.Current.CancellationToken);

        Assert.Empty(model.Requests);
        Assert.Contains("authorized skill file is unavailable", result.Message, StringComparison.Ordinal);
        Assert.Contains("No model request", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingMcpToolFailsClosedBeforeAWorkflowCanUseIt()
    {
        var availableTools = McpToolCatalog.ToolNames
            .Where(name => name != "validate_adrs")
            .ToArray();

        var exception = Assert.Throws<AgentMcpException>(
            () => McpToolCatalog.ValidateAvailableToolNames(availableTools));

        Assert.Contains("does not expose the required read-only tool catalog", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProposalWorkflowAlwaysIncludesRelatedAdrEvidence()
    {
        var configuration = CreateConfiguration(
            "Prepare an ADR proposal and validate the existing ADR set");

        var selection = ArchitectureQuestionToolSelector.Instance.Select(
            configuration,
            ArchitectureWorkflowCatalog.PrepareAdrProposal);

        Assert.Contains(
            selection.Requests,
            request => request.Kind == ArchitectureToolKind.ValidateAdrs);
        Assert.Contains(
            selection.Requests,
            request => request.Kind == ArchitectureToolKind.SearchAdrs);
    }

    [Fact]
    public void ExchangeQuestionUsesAdrSearchWithoutImpactManifest()
    {
        var selection = ArchitectureQuestionToolSelector.Instance.Select(
            CreateConfiguration("Why use a message exchange?"),
            ArchitectureWorkflowCatalog.ConsultAndValidateAdrs);

        Assert.Empty(selection.MissingInformation);
        var request = Assert.Single(selection.Requests);
        Assert.Equal(ArchitectureToolKind.SearchAdrs, request.Kind);
        Assert.Equal("use message exchange", request.Argument);
    }

    [Fact]
    public void ProposalSearchQueryKeepsOnlyDomainTerms()
    {
        var selection = ArchitectureQuestionToolSelector.Instance.Select(
            CreateConfiguration("Prepare a proposal about Redis"),
            ArchitectureWorkflowCatalog.PrepareAdrProposal);

        var request = Assert.Single(selection.Requests);
        Assert.Equal(ArchitectureToolKind.SearchAdrs, request.Kind);
        Assert.Equal("Redis", request.Argument);
    }

    private static AgentConfiguration CreateConfiguration(string question) =>
        new(
            Path.GetFullPath("."),
            "docs/adr",
            "fake-mcp",
            [],
            TimeSpan.FromSeconds(5),
            "openai",
            "fake-model",
            question,
            true,
            AgentLimits.Default,
            null,
            "HEAD",
            null,
            [],
            null,
            "advisory");

    private sealed class RecordingInstructionLoader : IWorkflowInstructionLoader
    {
        internal List<string> LoadedSkills { get; } = [];

        public Task<WorkflowInstructionLoadResult> LoadAsync(
            AgentConfiguration configuration,
            ArchitectureWorkflow workflow,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LoadedSkills.Add(workflow.SkillName);
            return Task.FromResult(new WorkflowInstructionLoadResult(
                "SELECTED_TRADEOFF_INSTRUCTIONS",
                null));
        }
    }

    private sealed class MissingInstructionLoader : IWorkflowInstructionLoader
    {
        public Task<WorkflowInstructionLoadResult> LoadAsync(
            AgentConfiguration configuration,
            ArchitectureWorkflow workflow,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new WorkflowInstructionLoadResult(
                null,
                "The selected workflow requires its instructions, but the authorized skill file is unavailable."));
        }
    }

    private sealed class FakeLanguageModel : ILanguageModel
    {
        internal List<LanguageModelRequest> Requests { get; } = [];

        public Task<LanguageModelResponse> CompleteAsync(
            LanguageModelRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            return Task.FromResult(new LanguageModelResponse(
                """
                {
                  "summary": "The evidence is limited [E1].",
                  "references": [{ "evidenceId": "E1", "claim": "The MCP result is the selected evidence." }],
                  "tradeOffs": ["Human review remains required."],
                  "uncertainties": [],
                  "missingInformation": []
                }
                """,
                request.Provider,
                request.Model));
        }
    }

    private sealed class FakeMcpClient : IMcpClient
    {
        public Task<McpEvidence> ListAdrsAsync(CancellationToken cancellationToken) => Result(cancellationToken);

        public Task<McpEvidence> GetAdrAsync(string identifier, CancellationToken cancellationToken) => Result(cancellationToken);

        public Task<McpEvidence> SearchAdrsAsync(string query, int limit, CancellationToken cancellationToken) => Result(cancellationToken);

        public Task<McpEvidence> GetAdrRelationshipsAsync(string? identifier, CancellationToken cancellationToken) => Result(cancellationToken);

        public Task<McpEvidence> ValidateAdrsAsync(string? validationProfile, CancellationToken cancellationToken) => Result(cancellationToken);

        public Task<McpEvidence> AnalyzeImpactAsync(string baseReference, string manifestPath, CancellationToken cancellationToken) => Result(cancellationToken);

        public Task<McpEvidence> VerifyArchitectureAsync(
            string rulesPath,
            IReadOnlyList<string>? evidencePaths,
            string? baselinePath,
            string policy,
            CancellationToken cancellationToken) => Result(cancellationToken);

        private static Task<McpEvidence> Result(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new McpEvidence(
                "search_adrs",
                "{}",
                "1.0",
                "{\"schemaVersion\":\"1.0\",\"matches\":[]}",
                McpEvidenceStatus.Complete,
                true));
        }
    }
}
