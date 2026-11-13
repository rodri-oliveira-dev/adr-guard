using Xunit;

namespace AdrGuard.Agent.Tests;

public sealed class AdrProposalWorkflowTests
{
    [Fact]
    public async Task ProposalUsesMcpEvidenceAndCliDescriptionBeforePreview()
    {
        var model = new FakeLanguageModel(ValidProposal);
        var previewClient = new FakePreviewClient();
        var runner = CreateRunner(
            new FakeMcpClient(total: 1),
            model,
            previewClient);

        var result = await runner.RunAsync(
            CreateConfiguration(),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, previewClient.DescribeCalls);
        Assert.Equal(1, previewClient.PreviewCalls);
        var request = Assert.Single(model.Requests);
        Assert.Contains("The CLI described template 'minimal'", request.Prompt, StringComparison.Ordinal);
        Assert.Contains("Proposal status: Proposed", result.Message, StringComparison.Ordinal);
        Assert.Contains("Proposed destination: docs/adr/0042-use-queue.md", result.Message, StringComparison.Ordinal);
        Assert.Contains("Evidence (MCP, read-only):", result.Message, StringComparison.Ordinal);
        Assert.Contains("Uncertainties:", result.Message, StringComparison.Ordinal);
        Assert.Contains("# Use a queue", result.Message, StringComparison.Ordinal);
        Assert.Contains("no ID was reserved", result.Message, StringComparison.Ordinal);
        Assert.Contains("Allowed form operation: create", result.Message, StringComparison.Ordinal);
        Assert.Contains("Proposal fingerprint:", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidModelFieldsAreRejectedBeforeCliPreview()
    {
        const string invalidProposal = """
            {
              "title": "Use a queue",
              "fields": {"context":"Context","decision":"Decision","unexpected":"Risk"},
              "alternatives": ["Keep synchronous processing"],
              "requirements": ["Preserve delivery"],
              "risks": ["Operational complexity"],
              "references": [{"evidenceId":"E1","claim":"Related ADR evidence"}],
              "uncertainties": [],
              "missingInformation": []
            }
            """;
        var previewClient = new FakePreviewClient();
        var runner = CreateRunner(
            new FakeMcpClient(total: 1),
            new FakeLanguageModel(invalidProposal),
            previewClient);

        var exception = await Assert.ThrowsAsync<AgentLanguageModelException>(
            () => runner.RunAsync(
                CreateConfiguration(),
                TestContext.Current.CancellationToken));

        Assert.Contains("did not match the template", exception.Message, StringComparison.Ordinal);
        Assert.Equal(1, previewClient.DescribeCalls);
        Assert.Equal(0, previewClient.PreviewCalls);
    }

    [Fact]
    public async Task RejectedPreviewFailsClosedWithoutAProposalResult()
    {
        var previewClient = new FakePreviewClient
        {
            PreviewException = new AgentCliException(
                "ADR proposal preview was rejected by 'adr-guard new --form' with exit code 1. No file was written."),
        };
        var runner = CreateRunner(
            new FakeMcpClient(total: 1),
            new FakeLanguageModel(ValidProposal),
            previewClient);

        var exception = await Assert.ThrowsAsync<AgentCliException>(
            () => runner.RunAsync(
                CreateConfiguration(),
                TestContext.Current.CancellationToken));

        Assert.Contains("preview was rejected", exception.Message, StringComparison.Ordinal);
        Assert.Equal(1, previewClient.PreviewCalls);
    }

    [Fact]
    public async Task MissingRelatedAdrEvidenceSkipsModelAndCli()
    {
        var model = new FakeLanguageModel(ValidProposal);
        var previewClient = new FakePreviewClient();
        var runner = CreateRunner(
            new FakeMcpClient(total: 0),
            model,
            previewClient);

        var result = await runner.RunAsync(
            CreateConfiguration(),
            TestContext.Current.CancellationToken);

        Assert.Empty(model.Requests);
        Assert.Equal(0, previewClient.DescribeCalls);
        Assert.Equal(0, previewClient.PreviewCalls);
        Assert.Contains("No related ADR evidence was available", result.Message, StringComparison.Ordinal);
        Assert.Contains("No model request, CLI describe/preview operation", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidationResultDoesNotSubstituteForRelatedAdrEvidence()
    {
        var model = new FakeLanguageModel(ValidProposal);
        var previewClient = new FakePreviewClient();
        var runner = CreateRunner(
            new FakeMcpClient(total: 0),
            model,
            previewClient);

        var result = await runner.RunAsync(
            CreateConfiguration("Prepare an ADR proposal and validate the existing ADR set"),
            TestContext.Current.CancellationToken);

        Assert.Empty(model.Requests);
        Assert.Equal(0, previewClient.DescribeCalls);
        Assert.Contains("No related ADR evidence was available", result.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, "denied")]
    [InlineData(1, "canceled")]
    public async Task CreationDecisionWithoutApprovalDoesNotInvokeCreate(
        int decisionValue,
        string expectedMessage)
    {
        var decision = (AdrProposalAuthorizationDecision)decisionValue;
        var previewClient = new FakePreviewClient();
        var authorization = new FakeAuthorization(decision);
        var runner = CreateRunner(
            new FakeMcpClient(total: 1),
            new FakeLanguageModel(ValidProposal),
            previewClient,
            authorization);

        var result = await runner.RunAsync(
            CreateConfiguration(enableCreation: true),
            TestContext.Current.CancellationToken);

        Assert.Contains(expectedMessage, result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, previewClient.CreateCalls);
        Assert.NotNull(authorization.Proposal);
        Assert.Contains("Markdown preview", authorization.Preview, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExactApprovalCreatesWithoutAnotherModelRequest()
    {
        var model = new FakeLanguageModel(ValidProposal);
        var previewClient = new FakePreviewClient();
        var authorization = new FakeAuthorization(AdrProposalAuthorizationDecision.Approved);
        var runner = CreateRunner(
            new FakeMcpClient(total: 1),
            model,
            previewClient,
            authorization);

        var result = await runner.RunAsync(
            CreateConfiguration(enableCreation: true),
            TestContext.Current.CancellationToken);

        Assert.Single(model.Requests);
        Assert.Equal(1, previewClient.CreateCalls);
        Assert.Contains("Created Proposed ADR", result.Message, StringComparison.Ordinal);
        Assert.Equal("Proposed", authorization.Proposal?.Preview.Markdown.Split("\n")[4]);
    }

    private static ArchitectureQuestionRunner CreateRunner(
        IMcpClient mcpClient,
        ILanguageModel model,
        IAdrProposalPreviewClient previewClient,
        IAdrProposalAuthorization? authorization = null) =>
        new(
            mcpClient,
            model,
            instructionLoader: FakeInstructionLoader.Instance,
            proposalPreviewClient: previewClient,
            proposalAuthorization: authorization);

    private static AgentConfiguration CreateConfiguration(
        string question = "Prepare an ADR proposal for asynchronous order processing",
        bool enableCreation = false) =>
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
            "advisory",
            new AdrProposalConfiguration(
                "fake-cli",
                ["fake-cli.dll"],
                TimeSpan.FromSeconds(5),
                AllowCreate: enableCreation));

    private const string ValidProposal = """
        {
          "title": "Use a queue",
          "fields": {
            "context": "Orders require asynchronous processing.",
            "decision": "Propose a managed queue.",
            "consequences": "Operations must manage retries."
          },
          "alternatives": ["Keep synchronous processing"],
          "requirements": ["Preserve delivery semantics"],
          "risks": ["Operational complexity"],
          "references": [{"evidenceId":"E1","claim":"The related ADR evidence describes the current boundary."}],
          "uncertainties": ["Throughput evidence is missing."],
          "missingInformation": ["No load test was supplied."]
        }
        """;

    private sealed class FakeInstructionLoader : IWorkflowInstructionLoader
    {
        internal static FakeInstructionLoader Instance { get; } = new();

        public Task<WorkflowInstructionLoadResult> LoadAsync(
            AgentConfiguration configuration,
            ArchitectureWorkflow workflow,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new WorkflowInstructionLoadResult(
                "Prepare a reviewable ADR proposal without writing it.",
                null));
        }
    }

    private sealed class FakeLanguageModel(string content) : ILanguageModel
    {
        internal List<LanguageModelRequest> Requests { get; } = [];

        public Task<LanguageModelResponse> CompleteAsync(
            LanguageModelRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            return Task.FromResult(new LanguageModelResponse(content, request.Provider, request.Model));
        }
    }

    private sealed class FakePreviewClient : IAdrProposalPreviewClient
    {
        internal int DescribeCalls { get; private set; }

        internal int PreviewCalls { get; private set; }

        internal int CreateCalls { get; private set; }

        internal Exception? PreviewException { get; init; }

        public Task<AdrFormDescription> DescribeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DescribeCalls++;
            return Task.FromResult(new AdrFormDescription(
                "minimal",
                [
                    new AdrFormField("context", true),
                    new AdrFormField("decision", true),
                    new AdrFormField("consequences", true),
                ]));
        }

        public Task<AdrFormPreview> PreviewAsync(
            string title,
            IReadOnlyDictionary<string, string> fields,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PreviewCalls++;
            if (PreviewException is not null)
            {
                return Task.FromException<AdrFormPreview>(PreviewException);
            }

            Assert.Equal("Use a queue", title);
            Assert.Equal(3, fields.Count);
            return Task.FromResult(new AdrFormPreview(
                "docs/adr/0042-use-queue.md",
                "# Use a queue\n\n## Status\n\nProposed\n"));
        }

        public Task<AdrFormCreation> CreateAsync(
            AdrCreationProposal proposal,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CreateCalls++;
            return Task.FromResult(new AdrFormCreation(proposal.Preview.Path));
        }
    }

    private sealed class FakeAuthorization(AdrProposalAuthorizationDecision decision)
        : IAdrProposalAuthorization
    {
        internal AdrCreationProposal? Proposal { get; private set; }

        internal string Preview { get; private set; } = string.Empty;

        public Task<AdrProposalAuthorizationDecision> AuthorizeAsync(
            AdrCreationProposal proposal,
            string preview,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Proposal = proposal;
            Preview = preview;
            return Task.FromResult(decision);
        }
    }

    private sealed class FakeMcpClient(int total) : IMcpClient
    {
        public Task<McpEvidence> ListAdrsAsync(CancellationToken cancellationToken) =>
            Result("list_adrs", cancellationToken);

        public Task<McpEvidence> GetAdrAsync(string identifier, CancellationToken cancellationToken) =>
            Result("get_adr", cancellationToken);

        public Task<McpEvidence> SearchAdrsAsync(
            string query,
            int limit,
            CancellationToken cancellationToken) =>
            Result("search_adrs", cancellationToken);

        public Task<McpEvidence> GetAdrRelationshipsAsync(
            string? identifier,
            CancellationToken cancellationToken) =>
            Result("get_adr_relationships", cancellationToken);

        public Task<McpEvidence> ValidateAdrsAsync(
            string? validationProfile,
            CancellationToken cancellationToken) =>
            Result("validate_adrs", cancellationToken);

        public Task<McpEvidence> AnalyzeImpactAsync(
            string baseReference,
            string manifestPath,
            CancellationToken cancellationToken) =>
            Result("analyze_impact", cancellationToken);

        public Task<McpEvidence> VerifyArchitectureAsync(
            string rulesPath,
            IReadOnlyList<string>? evidencePaths,
            string? baselinePath,
            string policy,
            CancellationToken cancellationToken) =>
            Result("verify_architecture", cancellationToken);

        private Task<McpEvidence> Result(string toolName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var content = toolName is "search_adrs" or "list_adrs"
                ? $$"""{"schemaVersion":"1.0","total":{{total}},"matches":[]}"""
                : """{"schemaVersion":"1.0","result":"evidence"}""";
            return Task.FromResult(new McpEvidence(
                toolName,
                "{}",
                "1.0",
                content,
                McpEvidenceStatus.Complete,
                true));
        }
    }
}
