using AdrGuard.Generation;
using AdrGuard.Parsing;
using AdrGuard.Validation;
using Xunit;

namespace AdrGuard.Tests.Generation;

/// <summary>
/// Final cross-workflow gate: real race barriers, ID invariants, and rollback
/// are exercised against the same creation/persistence primitives as the CLI.
/// </summary>
public sealed class AdrTemplateRegressionTests
{
    [Fact]
    public void IdAllocationUsesMaximumNotFirstGapAndExhaustionIsExplicit()
    {
        var root = CreateDirectory();
        try
        {
            WriteValid(root, 1, "Use Redis");
            WriteValid(root, 3, "Use Kafka");

            var documents = AdrDocumentLoader.LoadDirectory(
                root,
                TestContext.Current.CancellationToken);
            Assert.True(AdrValidator.Validate(documents).IsValid);
            Assert.Equal(4, AdrIdAllocator.NextId(documents));
            Assert.Equal(
                Path.Combine(root, "0004-use-cache.md"),
                AdrCreationService.AllocateFilePath(root, "Use Cache", documents));

            WriteValid(root, 9999, "Final Decision");
            documents = AdrDocumentLoader.LoadDirectory(
                root,
                TestContext.Current.CancellationToken);
            var error = Assert.Throws<InvalidOperationException>(
                () => AdrIdAllocator.NextId(documents));
            Assert.Contains("9999", error.Message, StringComparison.Ordinal);
            Assert.Equal(3, Directory.GetFiles(root, "*.md").Length);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Theory]
    [InlineData("Adotar Café — Redis", "adotar-cafe-redis")]
    [InlineData("Use SQL Server #1", "use-sql-server-1")]
    [InlineData("  Use   Message/Broker  ", "use-message-broker")]
    public void NormalizedTitleProducesDeterministicSafeSlug(
        string title,
        string slug)
    {
        Assert.Equal(slug, AdrSlug.Create(title));
        var fileName = Path.GetFileName(
            AdrCreationService.AllocateFilePath(
                Path.GetTempPath(),
                title,
                []));
        Assert.Equal($"0001-{slug}.md", fileName);
    }

    [Fact]
    public async Task SameTitleConcurrentNewCallsNeverOverwriteTheFirstCommit()
    {
        var root = CreateDirectory();
        var blocking = new BlockingPersistence();
        try
        {
            var template = AdrBuiltInTemplates.Get("extended", "pt-BR");
            var first = new AdrNewService(blocking)
                .CreateAsync(
                    root,
                    "Adotar Redis",
                    template,
                    dryRun: false,
                    TestContext.Current.CancellationToken);
            await blocking.Entered.WaitAsync(
                TimeSpan.FromSeconds(10),
                TestContext.Current.CancellationToken);

            var second = new AdrNewService()
                .CreateAsync(
                    root,
                    "Adotar Redis",
                    template,
                    dryRun: false,
                    TestContext.Current.CancellationToken);

            await Task.Delay(100, TestContext.Current.CancellationToken);
            Assert.False(second.IsCompleted);
            blocking.Release();

            var written = await first.WaitAsync(
                TimeSpan.FromSeconds(10),
                TestContext.Current.CancellationToken);
            var conflict = await Assert.ThrowsAsync<IOException>(
                () => second.WaitAsync(
                    TimeSpan.FromSeconds(10),
                    TestContext.Current.CancellationToken));

            Assert.True(written.Written);
            Assert.Contains("already exists", conflict.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(written.Content, File.ReadAllText(written.FilePath!));
            Assert.Single(Directory.GetFiles(root, "*.md"));
            Assert.True(AdrValidator.Validate(
                AdrDocumentLoader.LoadDirectory(
                    root,
                    TestContext.Current.CancellationToken)).IsValid);
            AssertCleanDirectory(root);
        }
        finally
        {
            blocking.Release();
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task MixedOfflineAndAiCreatorsShareMutexAndPersistMatchingFinalIds()
    {
        var root = CreateDirectory();
        var blocking = new BlockingPersistence();
        try
        {
            var template = AdrCustomTemplateLoader.Load(
                Fixture("custom-template.en-US.md"),
                "en-US",
                cancellationToken: TestContext.Current.CancellationToken);
            var offline = new AdrNewService(blocking).CreateAsync(
                root,
                "Adopt Redis",
                template,
                dryRun: false,
                TestContext.Current.CancellationToken);
            await blocking.Entered.WaitAsync(
                TimeSpan.FromSeconds(10),
                TestContext.Current.CancellationToken);

            var provider = new CountingProvider();
            var assisted = new AdrGenerationService(provider).GenerateAsync(
                root,
                "Adopt Kafka",
                "Only explicitly provided context.",
                "en-US",
                [],
                includeExistingAdrs: false,
                dryRun: false,
                template,
                TestContext.Current.CancellationToken);

            await Task.Delay(100, TestContext.Current.CancellationToken);
            Assert.False(assisted.IsCompleted);
            Assert.Equal(1, provider.Calls);
            blocking.Release();

            var savedOffline = await offline.WaitAsync(
                TimeSpan.FromSeconds(10),
                TestContext.Current.CancellationToken);
            var savedAssisted = await assisted.WaitAsync(
                TimeSpan.FromSeconds(10),
                TestContext.Current.CancellationToken);

            Assert.True(savedOffline.Written);
            Assert.True(savedAssisted.Written);
            Assert.Equal(1, provider.Calls);
            Assert.Contains("ADR 0001", savedOffline.Content, StringComparison.Ordinal);
            Assert.Contains("ADR 0002", savedAssisted.Content, StringComparison.Ordinal);
            Assert.Equal(savedOffline.Content, File.ReadAllText(savedOffline.FilePath!));
            Assert.Equal(savedAssisted.Content, File.ReadAllText(savedAssisted.FilePath!));
            Assert.Equal(
                ["0001", "0002"],
                new[] { savedOffline.FilePath!, savedAssisted.FilePath! }
                    .Select(path => Path.GetFileName(path)[..4])
                    .Order(StringComparer.Ordinal)
                    .ToArray());
            Assert.True(AdrValidator.Validate(
                AdrDocumentLoader.LoadDirectory(
                    root,
                    TestContext.Current.CancellationToken)).IsValid);
            AssertCleanDirectory(root);
        }
        finally
        {
            blocking.Release();
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task CancelledOfflineCreatorWaitingForLockLeavesNoReservedIdOrArtifact()
    {
        var root = CreateDirectory();
        var blocking = new BlockingPersistence();
        try
        {
            var template = AdrBuiltInTemplates.Get("minimal", "en-US");
            var first = new AdrNewService(blocking).CreateAsync(
                root,
                "Use Redis",
                template,
                dryRun: false,
                TestContext.Current.CancellationToken);
            await blocking.Entered.WaitAsync(
                TimeSpan.FromSeconds(10),
                TestContext.Current.CancellationToken);

            using var cancelled = new CancellationTokenSource();
            var second = new AdrNewService().CreateAsync(
                root,
                "Use Kafka",
                template,
                dryRun: false,
                cancelled.Token);
            await Task.Delay(100, TestContext.Current.CancellationToken);
            cancelled.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => second.WaitAsync(
                    TimeSpan.FromSeconds(10),
                    TestContext.Current.CancellationToken));

            blocking.Release();
            var saved = await first.WaitAsync(
                TimeSpan.FromSeconds(10),
                TestContext.Current.CancellationToken);
            Assert.True(saved.Written);
            Assert.Single(Directory.GetFiles(root, "*.md"));
            Assert.True(AdrValidator.Validate(
                AdrDocumentLoader.LoadDirectory(
                    root,
                    TestContext.Current.CancellationToken)).IsValid);
            AssertCleanDirectory(root);

            // Cancellation does not consume 0002 and leaves the directory writable.
            var retry = await new AdrNewService().CreateAsync(
                root,
                "Use Kafka",
                template,
                dryRun: false,
                TestContext.Current.CancellationToken);
            Assert.EndsWith(
                "0002-use-kafka.md",
                retry.FilePath,
                StringComparison.Ordinal);
            AssertCleanDirectory(root);
        }
        finally
        {
            blocking.Release();
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task FailedTemporaryWriteRollsBackAndAllowsRetryWithTheSameId()
    {
        var root = CreateDirectory();
        try
        {
            var persistence = new AtomicAdrDraftFilePersistence(
                async (temporaryPath, value, cancellationToken) =>
                {
                    await File.WriteAllTextAsync(
                        temporaryPath,
                        "INCOMPLETE ADR",
                        cancellationToken);
                    throw new IOException("simulated disk failure");
                });
            var template = AdrBuiltInTemplates.Get("minimal", "en-US");

            var failure = await Assert.ThrowsAsync<IOException>(
                () => new AdrNewService(persistence).CreateAsync(
                    root,
                    "Use Redis",
                    template,
                    dryRun: false,
                    TestContext.Current.CancellationToken));
            Assert.Contains("simulated disk failure", failure.Message, StringComparison.Ordinal);
            Assert.Empty(Directory.EnumerateFiles(root));
            AssertCleanDirectory(root);

            var retry = await new AdrNewService().CreateAsync(
                root,
                "Use Redis",
                template,
                dryRun: false,
                TestContext.Current.CancellationToken);
            Assert.True(retry.Written);
            Assert.EndsWith(
                "0001-use-redis.md",
                retry.FilePath,
                StringComparison.Ordinal);
            Assert.True(AdrValidator.Validate(
                AdrDocumentLoader.LoadDirectory(
                    root,
                    TestContext.Current.CancellationToken)).IsValid);
            AssertCleanDirectory(root);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static void WriteValid(string directory, int id, string title)
    {
        var content = AdrMarkdownRenderer.RenderTemplate(
            new AdrTemplateRenderRequest(
                title,
                AdrBuiltInTemplates.Get("minimal", "en-US"),
                new Dictionary<string, string>(),
                Id: id));
        var fileName = $"{id:D4}-{AdrSlug.Create(title)}.md";
        File.WriteAllText(Path.Combine(directory, fileName), content);
    }

    private static string Fixture(string name) =>
        Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "Templates",
            name);

    private static void AssertCleanDirectory(string root)
    {
        Assert.DoesNotContain(
            Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories),
            path => Path.GetExtension(path) == ".tmp"
                || Path.GetFileName(path).Contains(
                    "reservation",
                    StringComparison.OrdinalIgnoreCase)
                || Path.GetFileName(path).Contains(
                    "lock",
                    StringComparison.OrdinalIgnoreCase));
    }

    private static string CreateDirectory()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "adr-guard-template-regression",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteDirectory(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class CountingProvider : IAdrGenerationProvider
    {
        private int _calls;

        internal int Calls => Volatile.Read(ref _calls);

        public Task<AdrGenerationResult> GenerateAsync(
            AdrGenerationRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _calls);
            return Task.FromResult(new AdrGenerationResult(
                "Provider context.",
                "Use Kafka.",
                "Operate Kafka."));
        }
    }

    private sealed class BlockingPersistence : IAdrDraftFilePersistence
    {
        private readonly TaskCompletionSource _entered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly AtomicAdrDraftFilePersistence _inner = new();

        internal Task Entered => _entered.Task;

        internal void Release() => _release.TrySetResult();

        public async Task WriteNewAsync(
            string filePath,
            string content,
            CancellationToken cancellationToken)
        {
            _entered.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken);
            await _inner.WriteNewAsync(
                filePath,
                content,
                cancellationToken);
        }
    }
}
