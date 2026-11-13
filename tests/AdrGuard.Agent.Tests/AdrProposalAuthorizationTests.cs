using Xunit;

namespace AdrGuard.Agent.Tests;

public sealed class AdrProposalAuthorizationTests
{
    [Fact]
    public async Task NonInteractiveInputCannotAuthorizeCreation()
    {
        using var output = new StringWriter();
        var authorization = new ConsoleAdrProposalAuthorization(
            new StringReader($"create {Proposal.Fingerprint}"),
            output,
            isInteractive: false);

        var exception = await Assert.ThrowsAsync<AgentCliException>(
            () => authorization.AuthorizeAsync(
                Proposal,
                "exact preview",
                TestContext.Current.CancellationToken));

        Assert.Contains("non-interactive", exception.Message, StringComparison.Ordinal);
        Assert.Contains("exact preview", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExactFingerprintIsRequiredForApproval()
    {
        using var output = new StringWriter();
        var authorization = new ConsoleAdrProposalAuthorization(
            new StringReader($"create {Proposal.Fingerprint}\n"),
            output,
            isInteractive: true);

        var decision = await authorization.AuthorizeAsync(
            Proposal,
            "exact preview",
            TestContext.Current.CancellationToken);

        Assert.Equal(AdrProposalAuthorizationDecision.Approved, decision);
        Assert.Contains(Proposal.Fingerprint, output.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("deny", 0)]
    [InlineData("cancel", 1)]
    [InlineData("create WRONG", 0)]
    public async Task DenialCancellationAndWrongFingerprintDoNotApprove(
        string response,
        int expectedValue)
    {
        var authorization = new ConsoleAdrProposalAuthorization(
            new StringReader(response + "\n"),
            TextWriter.Null,
            isInteractive: true);

        var decision = await authorization.AuthorizeAsync(
            Proposal,
            "exact preview",
            TestContext.Current.CancellationToken);

        Assert.Equal((AdrProposalAuthorizationDecision)expectedValue, decision);
    }

    [Fact]
    public async Task CancellationAbandonsLateApprovalInput()
    {
        var input = new DeferredTextReader();
        var authorization = new ConsoleAdrProposalAuthorization(
            input,
            TextWriter.Null,
            isInteractive: true);
        using var cancellationSource = new CancellationTokenSource();

        var authorizationTask = authorization.AuthorizeAsync(
            Proposal,
            "exact preview",
            cancellationSource.Token);
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => authorizationTask);
        input.Complete($"create {Proposal.Fingerprint}");
        Assert.True(authorizationTask.IsCanceled);
    }

    private static AdrCreationProposal Proposal { get; } = new(
        new string('A', 64),
        "docs/adr",
        "adr-guard",
        ["new", "docs/adr", "--form"],
        "minimal",
        "Use a queue",
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["context"] = "Context",
            ["decision"] = "Decision",
            ["consequences"] = "Consequences",
        },
        new AdrFormPreview(
            "docs/adr/0001-use-a-queue.md",
            "# Use a queue\n\n## Status\n\nProposed\n"));

    private sealed class DeferredTextReader : TextReader
    {
        private readonly TaskCompletionSource<string?> completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal void Complete(string value) => completion.TrySetResult(value);

        public override ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken) =>
            new(completion.Task);
    }
}
