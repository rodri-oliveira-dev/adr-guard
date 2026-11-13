using Xunit;

namespace AdrGuard.Agent.Tests;

public sealed class AdrNewFormPreviewClientTests
{
    [Fact]
    public async Task RealCliDescribeAndPreviewDoNotWriteOrReserveAnId()
    {
        using var repository = new TemporaryRepository();
        var client = new AdrNewFormPreviewClient(repository.Configuration);

        var description = await client.DescribeAsync(TestContext.Current.CancellationToken);
        var preview = await client.PreviewAsync(
            "Choose a queue",
            RequiredFields,
            TestContext.Current.CancellationToken);

        Assert.Equal("minimal", description.Template);
        Assert.Equal(["context", "decision", "consequences"], description.Fields.Select(field => field.Id));
        Assert.EndsWith("docs/adr/0001-choose-a-queue.md", preview.Path, StringComparison.Ordinal);
        Assert.Contains("## Status\n\nProposed", preview.Markdown, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFileSystemEntries(repository.AdrDirectory));
    }

    [Fact]
    public async Task InvalidPreviewInputIsRejectedByRealCliWithoutWriting()
    {
        using var repository = new TemporaryRepository();
        var client = new AdrNewFormPreviewClient(repository.Configuration);
        var invalidFields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["context"] = "Context",
            ["decision"] = "Decision",
        };

        var exception = await Assert.ThrowsAsync<AgentCliException>(
            () => client.PreviewAsync(
                "Invalid proposal",
                invalidFields,
                TestContext.Current.CancellationToken));

        Assert.Contains("exit code 2", exception.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFileSystemEntries(repository.AdrDirectory));
    }

    [Fact]
    public async Task ConcurrentConflictChangesOnlyTheProvisionalDestination()
    {
        using var repository = new TemporaryRepository();
        var client = new AdrNewFormPreviewClient(repository.Configuration);

        var first = await client.PreviewAsync(
            "Choose a queue",
            RequiredFields,
            TestContext.Current.CancellationToken);
        Assert.Empty(Directory.EnumerateFileSystemEntries(repository.AdrDirectory));

        var concurrentPath = Path.Combine(
            repository.RepositoryRoot,
            first.Path.Replace('/', Path.DirectorySeparatorChar));
        File.WriteAllText(concurrentPath, first.Markdown);

        var second = await client.PreviewAsync(
            "Choose a queue",
            RequiredFields,
            TestContext.Current.CancellationToken);

        Assert.EndsWith("docs/adr/0001-choose-a-queue.md", first.Path, StringComparison.Ordinal);
        Assert.EndsWith("docs/adr/0002-choose-a-queue.md", second.Path, StringComparison.Ordinal);
        Assert.Single(Directory.EnumerateFiles(repository.AdrDirectory));
        Assert.Equal(first.Markdown, File.ReadAllText(concurrentPath));
    }

    [Fact]
    public async Task AuthorizedCreateUsesRealCliAndWritesOnlyProposedContent()
    {
        using var repository = new TemporaryRepository(allowCreate: true);
        var client = new AdrNewFormPreviewClient(repository.Configuration);
        var description = await client.DescribeAsync(TestContext.Current.CancellationToken);
        var preview = await client.PreviewAsync(
            "Choose a queue",
            RequiredFields,
            TestContext.Current.CancellationToken);
        var proposal = AdrCreationProposalFactory.Create(
            repository.Configuration,
            description,
            CreateProposal(),
            preview);

        var creation = await client.CreateAsync(proposal, TestContext.Current.CancellationToken);

        var createdPath = Path.Combine(
            repository.RepositoryRoot,
            creation.Path.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(createdPath));
        Assert.Equal(preview.Markdown, File.ReadAllText(createdPath));
        Assert.Contains("## Status\n\nProposed", File.ReadAllText(createdPath), StringComparison.Ordinal);
        Assert.Single(Directory.EnumerateFiles(repository.AdrDirectory));
    }

    [Fact]
    public async Task ChangedAuthorizedContentIsRejectedBeforeCreate()
    {
        using var repository = new TemporaryRepository(allowCreate: true);
        var client = new AdrNewFormPreviewClient(repository.Configuration);
        var description = await client.DescribeAsync(TestContext.Current.CancellationToken);
        var preview = await client.PreviewAsync(
            "Choose a queue",
            RequiredFields,
            TestContext.Current.CancellationToken);
        var altered = preview with { Markdown = preview.Markdown + "unexpected" };
        var proposal = AdrCreationProposalFactory.Create(
            repository.Configuration,
            description,
            CreateProposal(),
            altered);

        var exception = await Assert.ThrowsAsync<AgentCliException>(
            () => client.CreateAsync(proposal, TestContext.Current.CancellationToken));

        Assert.Contains("changed after preview", exception.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFileSystemEntries(repository.AdrDirectory));
    }

    [Fact]
    public async Task ChangedAuthorizedInvocationIsRejectedBeforeCreate()
    {
        using var repository = new TemporaryRepository(allowCreate: true);
        var client = new AdrNewFormPreviewClient(repository.Configuration);
        var description = await client.DescribeAsync(TestContext.Current.CancellationToken);
        var preview = await client.PreviewAsync(
            "Choose a queue",
            RequiredFields,
            TestContext.Current.CancellationToken);
        var proposal = AdrCreationProposalFactory.Create(
            repository.Configuration,
            description,
            CreateProposal(),
            preview) with
        {
            CliArguments = ["unexpected"],
        };

        var exception = await Assert.ThrowsAsync<AgentCliException>(
            () => client.CreateAsync(proposal, TestContext.Current.CancellationToken));

        Assert.Contains("CLI invocation changed", exception.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFileSystemEntries(repository.AdrDirectory));
    }

    [Fact]
    public async Task ChangedCliTimeoutInvalidatesAuthorizedProposal()
    {
        using var repository = new TemporaryRepository(allowCreate: true);
        var descriptionClient = new AdrNewFormPreviewClient(repository.Configuration);
        var description = await descriptionClient.DescribeAsync(TestContext.Current.CancellationToken);
        var preview = await descriptionClient.PreviewAsync(
            "Choose a queue",
            RequiredFields,
            TestContext.Current.CancellationToken);
        var proposal = AdrCreationProposalFactory.Create(
            repository.Configuration,
            description,
            CreateProposal(),
            preview);
        var changedConfiguration = repository.Configuration with
        {
            AdrProposal = repository.Configuration.AdrProposal! with
            {
                CliTimeout = TimeSpan.FromSeconds(1),
            },
        };
        var changedClient = new AdrNewFormPreviewClient(changedConfiguration);

        var exception = await Assert.ThrowsAsync<AgentCliException>(
            () => changedClient.CreateAsync(proposal, TestContext.Current.CancellationToken));

        Assert.Contains("CLI invocation changed", exception.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFileSystemEntries(repository.AdrDirectory));
    }

    [Fact]
    public async Task ConcurrentWriterIsPreservedAndAuthorizedCreateGetsNextAtomicId()
    {
        using var repository = new TemporaryRepository(allowCreate: true);
        var client = new AdrNewFormPreviewClient(repository.Configuration);
        var description = await client.DescribeAsync(TestContext.Current.CancellationToken);
        var preview = await client.PreviewAsync(
            "Choose a queue",
            RequiredFields,
            TestContext.Current.CancellationToken);
        var proposal = AdrCreationProposalFactory.Create(
            repository.Configuration,
            description,
            CreateProposal(),
            preview);
        var concurrentPath = Path.Combine(repository.AdrDirectory, "0001-concurrent.md");
        const string concurrentContent = "# Concurrent\n\n## Status\n\nProposed\n\n## Context\n\nContext\n\n## Decision\n\nDecision\n\n## Consequences\n\nConsequences\n";
        File.WriteAllText(concurrentPath, concurrentContent);

        var creation = await client.CreateAsync(proposal, TestContext.Current.CancellationToken);

        Assert.EndsWith("docs/adr/0002-choose-a-queue.md", creation.Path, StringComparison.Ordinal);
        Assert.Equal(concurrentContent, File.ReadAllText(concurrentPath));
        Assert.Equal(2, Directory.EnumerateFiles(repository.AdrDirectory).Count());
    }

    private static IReadOnlyDictionary<string, string> RequiredFields { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["context"] = "Orders require asynchronous processing.",
            ["decision"] = "Propose a managed queue.",
            ["consequences"] = "Operations must manage retries.",
        };

    private static AdrProposal CreateProposal() => new(
        "Choose a queue",
        "Proposed",
        RequiredFields,
        [],
        [],
        [],
        [],
        [],
        []);

    private sealed class TemporaryRepository : IDisposable
    {
        internal TemporaryRepository(bool allowCreate = false)
        {
            RepositoryRoot = Path.Combine(
                Path.GetTempPath(),
                "adr-guard-agent-proposal-tests",
                Guid.NewGuid().ToString("N"));
            AdrDirectory = Path.Combine(RepositoryRoot, "docs", "adr");
            Directory.CreateDirectory(AdrDirectory);
            Configuration = new AgentConfiguration(
                RepositoryRoot,
                "docs/adr",
                "fake-mcp",
                [],
                TimeSpan.FromSeconds(5),
                "openai",
                "fake-model",
                "Prepare an ADR proposal",
                true,
                AgentLimits.Default,
                null,
                "HEAD",
                null,
                [],
                null,
                "advisory",
                new AdrProposalConfiguration(
                    "dotnet",
                    [CliAssemblyPath],
                    TimeSpan.FromSeconds(15),
                    allowCreate));
        }

        internal string RepositoryRoot { get; }

        internal string AdrDirectory { get; }

        internal AgentConfiguration Configuration { get; }

        private static string CliAssemblyPath
        {
            get
            {
                var configurationDirectory = Directory.GetParent(AppContext.BaseDirectory.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar))
                    ?? throw new InvalidOperationException("Unable to resolve the test build configuration.");
                var repositoryRoot = Path.GetFullPath(
                    Path.Combine(AppContext.BaseDirectory, "../../../../../"));
                return Path.Combine(
                    repositoryRoot,
                    "src",
                    "AdrGuard",
                    "bin",
                    configurationDirectory.Name,
                    "net10.0",
                    "adr-guard.dll");
            }
        }

        public void Dispose()
        {
            if (Directory.Exists(RepositoryRoot))
            {
                Directory.Delete(RepositoryRoot, recursive: true);
            }
        }
    }
}
