using Xunit;

namespace AdrGuard.Agent.Tests;

public sealed class AgentApplicationTests
{
    [Fact]
    public async Task HelpDocumentsExplicitConfigurationWithoutRunningAgent()
    {
        var runner = new FakeAgentRunner();

        var result = await RunAsync(
            ["--help"],
            runner,
            TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("--repository <path>", result.Output, StringComparison.Ordinal);
        Assert.Contains("--adr-directory <path>", result.Output, StringComparison.Ordinal);
        Assert.Contains("--mcp-command <command>", result.Output, StringComparison.Ordinal);
        Assert.Contains("--provider openai", result.Output, StringComparison.Ordinal);
        Assert.Contains("--model <model>", result.Output, StringComparison.Ordinal);
        Assert.Contains("--question <text>", result.Output, StringComparison.Ordinal);
        Assert.Contains("--allow-remote-context", result.Output, StringComparison.Ordinal);
        Assert.Contains("--max-tool-calls <1-20>", result.Output, StringComparison.Ordinal);
        Assert.Contains("--cli-command <command>", result.Output, StringComparison.Ordinal);
        Assert.Contains("previewed before", result.Output, StringComparison.Ordinal);
        Assert.Contains("--enable-adr-creation", result.Output, StringComparison.Ordinal);
        Assert.Contains("OPENAI_API_KEY", result.Output, StringComparison.Ordinal);
        Assert.Equal(string.Empty, result.Error);
        Assert.Null(runner.Configuration);
    }

    [Fact]
    public async Task ExplicitConfigurationIsCanonicalizedAndPassedToRunner()
    {
        var runner = new FakeAgentRunner { Result = new AgentRunResult("ready") };

        var result = await RunAsync(
            ValidArguments,
            runner,
            TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal($"ready{Environment.NewLine}", result.Output);
        Assert.Equal(string.Empty, result.Error);
        Assert.Equal(RepositoryRoot, runner.Configuration?.RepositoryRoot);
        Assert.Equal("docs/adr", runner.Configuration?.AdrDirectory);
        Assert.Equal("dotnet", runner.Configuration?.McpCommand);
        Assert.Equal(["fake-server.dll"], runner.Configuration?.McpArguments);
        Assert.Equal(TimeSpan.FromSeconds(15), runner.Configuration?.McpTimeout);
        Assert.Equal("openai", runner.Configuration?.Provider);
        Assert.Equal("fake-model", runner.Configuration?.Model);
        Assert.Equal("Which ADR protects the domain boundary?", runner.Configuration?.Question);
        Assert.True(runner.Configuration?.AllowRemoteContext);
        Assert.Equal(3, runner.Configuration?.Limits.MaxToolCalls);
    }

    [Fact]
    public async Task ProposalPreviewCliRequiresExplicitBoundedConfiguration()
    {
        var runner = new FakeAgentRunner();
        string[] arguments =
        [
            .. ValidArguments,
            "--cli-command", "dotnet",
            "--cli-argument", "fake-cli.dll",
            "--cli-timeout-seconds", "12",
            "--enable-adr-creation",
        ];

        var result = await RunAsync(
            arguments,
            runner,
            TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("dotnet", runner.Configuration?.AdrProposal?.CliCommand);
        Assert.Equal(["fake-cli.dll"], runner.Configuration?.AdrProposal?.CliArguments);
        Assert.Equal(TimeSpan.FromSeconds(12), runner.Configuration?.AdrProposal?.CliTimeout);
        Assert.True(runner.Configuration?.AdrProposal?.AllowCreate);
    }

    [Fact]
    public async Task CreationCapabilityRequiresExplicitCliConfiguration()
    {
        var result = await RunAsync(
            [.. ValidArguments, "--enable-adr-creation"],
            new FakeAgentRunner(),
            TestContext.Current.CancellationToken);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("requires '--cli-command'", result.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData()]
    [InlineData("--repository", ".", "--provider", "openai")]
    [InlineData("--repository", ".", "--adr-directory", "docs/adr", "--mcp-command", "dotnet", "--provider", "openai", "--model", "fake-model", "--question", "question", "--allow-remote-context", "--unknown")]
    [InlineData("--repository", ".", "--repository", ".", "--adr-directory", "docs/adr", "--mcp-command", "dotnet", "--provider", "openai", "--model", "fake-model", "--question", "question", "--allow-remote-context")]
    public async Task InvalidUsageReturnsStableExitCodeAndDoesNotRunAgent(params string[] args)
    {
        var runner = new FakeAgentRunner();

        var result = await RunAsync(args, runner, TestContext.Current.CancellationToken);

        Assert.Equal(2, result.ExitCode);
        Assert.Equal(string.Empty, result.Output);
        Assert.Contains("Run 'adr-guard-agent --help' for usage.", result.Error, StringComparison.Ordinal);
        Assert.Null(runner.Configuration);
    }

    [Fact]
    public async Task MissingRepositoryReturnsUsageError()
    {
        var missingRepository = Path.Combine(Path.GetTempPath(), $"adr-guard-agent-missing-{Guid.NewGuid():N}");

        var result = await RunAsync(
            ["--repository", missingRepository, "--adr-directory", "docs/adr", "--mcp-command", "dotnet", "--provider", "openai", "--model", "fake-model", "--question", "question", "--allow-remote-context"],
            new FakeAgentRunner(),
            TestContext.Current.CancellationToken);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("repository does not exist", result.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("missing-adrs", "ADR directory does not exist")]
    [InlineData("../", "ADR directory must remain within")]
    public async Task AdrDirectoryMustBeExplicitExistingAndContained(
        string adrDirectory,
        string expectedMessage)
    {
        var result = await RunAsync(
            [
                "--repository", RepositoryRoot,
                "--adr-directory", adrDirectory,
                "--mcp-command", "dotnet",
                "--provider", "openai",
                "--model", "fake-model",
                "--question", "question",
                "--allow-remote-context",
            ],
            new FakeAgentRunner(),
            TestContext.Current.CancellationToken);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains(expectedMessage, result.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("301")]
    [InlineData("not-a-number")]
    public async Task McpTimeoutMustRemainWithinSupportedBounds(string timeout)
    {
        var arguments = ValidArguments;
        arguments[Array.IndexOf(arguments, "15")] = timeout;

        var result = await RunAsync(
            arguments,
            new FakeAgentRunner(),
            TestContext.Current.CancellationToken);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("integer from 1 through 300", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ModelIdentifierRejectsTerminalControlCharacters()
    {
        var arguments = ValidArguments;
        arguments[Array.IndexOf(arguments, "fake-model")] = "fake\nmodel";

        var result = await RunAsync(
            arguments,
            new FakeAgentRunner(),
            TestContext.Current.CancellationToken);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("must contain printable text", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OperationalFailureIsReportedWithoutAStackTrace()
    {
        var runner = new FakeAgentRunner
        {
            Exception = new AgentExecutionException("The local fake failed."),
        };

        var result = await RunAsync(
            ValidArguments,
            runner,
            TestContext.Current.CancellationToken);

        Assert.Equal(3, result.ExitCode);
        Assert.Equal(string.Empty, result.Output);
        Assert.Equal($"The local fake failed.{Environment.NewLine}", result.Error);
    }

    [Fact]
    public async Task ProviderAndModelAreOptionalForLocalReadOnlyMode()
    {
        var runner = new FakeAgentRunner { Result = new AgentRunResult("local evidence") };
        var arguments = ValidArguments
            .Where(argument => argument is not ("--provider" or "openai" or "--model" or "fake-model" or "--allow-remote-context"))
            .ToArray();
        var result = await RunAsync(
            arguments,
            runner,
            TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Null(runner.Configuration?.Provider);
        Assert.Null(runner.Configuration?.Model);
        Assert.False(runner.Configuration?.AllowRemoteContext);
    }

    [Fact]
    public async Task ProviderAndModelMustBeConfiguredTogether()
    {
        var arguments = ValidArguments
            .Where(argument => argument is not ("--model" or "fake-model"))
            .ToArray();

        var result = await RunAsync(
            arguments,
            new FakeAgentRunner(),
            TestContext.Current.CancellationToken);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("must be specified together", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RemoteContextRequiresExplicitAuthorization()
    {
        var arguments = ValidArguments.Where(argument => argument != "--allow-remote-context").ToArray();
        var result = await RunAsync(
            arguments,
            new FakeAgentRunner(),
            TestContext.Current.CancellationToken);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Remote context sharing is disabled", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancellationReturnsOperationalError()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        var result = await RunAsync(
            ValidArguments,
            new FakeAgentRunner(),
            cancellationSource.Token);

        Assert.Equal(3, result.ExitCode);
        Assert.Equal($"Agent execution was canceled.{Environment.NewLine}", result.Error);
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunAsync(
        IReadOnlyList<string> args,
        IAgentRunner runner,
        CancellationToken cancellationToken = default)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await AgentApplication.RunAsync(
            args,
            output,
            error,
            runner,
            cancellationToken: cancellationToken);

        return (exitCode, output.ToString(), error.ToString());
    }

    private static string[] ValidArguments =>
    [
        "--repository", RepositoryRoot,
        "--adr-directory", "docs/adr",
        "--mcp-command", "dotnet",
        "--mcp-argument", "fake-server.dll",
        "--mcp-timeout-seconds", "15",
        "--provider", "openai",
        "--model", "fake-model",
        "--question", "Which ADR protects the domain boundary?",
        "--allow-remote-context",
        "--max-tool-calls", "3",
    ];

    private static string RepositoryRoot => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    private sealed class FakeAgentRunner : IAgentRunner
    {
        internal AgentConfiguration? Configuration { get; private set; }

        internal AgentRunResult Result { get; init; } = new(string.Empty);

        internal Exception? Exception { get; init; }

        public Task<AgentRunResult> RunAsync(
            AgentConfiguration configuration,
            CancellationToken cancellationToken)
        {
            Configuration = configuration;
            cancellationToken.ThrowIfCancellationRequested();

            return Exception is null
                ? Task.FromResult(Result)
                : Task.FromException<AgentRunResult>(Exception);
        }
    }
}
