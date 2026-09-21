using AdrGuard.Generation;
using AdrGuard.Parsing;
using AdrGuard.Validation;
using Xunit;

namespace AdrGuard.Tests.Generation;

public sealed class AdrCreationServiceTests
{
    private const string ValidContent = """
        # Use Redis

        ## Status

        Proposed

        ## Context

        The service needs caching.

        ## Decision

        Use Redis.

        ## Consequences

        Operate Redis.
        """;

    [Fact]
    public async Task ConcurrentDifferentTitlesAllocateDistinctIdsAndValidateCompleteSet()
    {
        var root = CreateDirectory();
        try
        {
            var provider = new RendezvousProvider(expectedCalls: 2);
            var redis = new AdrGenerationService(provider);
            var kafka = new AdrGenerationService(provider);

            var first = GenerateAsync(redis, root, "Use Redis");
            var second = GenerateAsync(kafka, root, "Use Kafka");
            var outcomes = await Task.WhenAll(first, second);

            Assert.All(outcomes, outcome => Assert.True(outcome.Written));
            Assert.Equal(
                ["0001", "0002"],
                outcomes.Select(outcome =>
                        Path.GetFileName(outcome.FilePath!)[..4])
                    .Order(StringComparer.Ordinal)
                    .ToArray());
            Assert.Equal(2, Directory.GetFiles(root, "*.md").Length);
            Assert.True(AdrValidator.Validate(
                AdrDocumentLoader.LoadDirectory(root)).IsValid);
            Assert.False(File.Exists(Path.Combine(root, "README.md")));
            AssertNoCreationArtifacts(root);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task ConcurrentSameTitleNeverOverwritesAndOnlyOneFileIsCommitted()
    {
        var root = CreateDirectory();
        try
        {
            var provider = new RendezvousProvider(expectedCalls: 2);
            var first = GenerateAsync(
                new AdrGenerationService(provider), root, "Use Redis");
            var second = GenerateAsync(
                new AdrGenerationService(provider), root, "Use Redis");

            var results = await Task.WhenAll(
                ObserveAsync(first),
                ObserveAsync(second));

            Assert.Single(results, result => result.Written);
            var conflict = Assert.Single(
                results.Where(result => result.Error is not null)).Error;
            var io = Assert.IsType<IOException>(conflict);
            Assert.Contains(
                "already exists", io.Message,
                StringComparison.OrdinalIgnoreCase);
            Assert.Single(Directory.GetFiles(root, "*.md"));
            Assert.True(AdrValidator.Validate(
                AdrDocumentLoader.LoadDirectory(root)).IsValid);
            AssertNoCreationArtifacts(root);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task CancellationWhileWaitingForCreationLockLeavesNoArtifacts()
    {
        var root = CreateDirectory();
        var blocking = new BlockingPersistence();

        try
        {
            var first = GenerateAsync(
                new AdrGenerationService(new FixedProvider(), blocking),
                root,
                "Use Redis");

            await blocking.Entered.WaitAsync(TimeSpan.FromSeconds(10));

            using var cancellation = new CancellationTokenSource();
            var second = GenerateAsync(
                new AdrGenerationService(new FixedProvider()),
                root,
                "Use Kafka",
                cancellation.Token);

            // The first creator still owns the directory mutex.
            await Task.Delay(100, TestContext.Current.CancellationToken);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => second);

            blocking.Release();
            var completed = await first.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(completed.Written);
            Assert.Single(Directory.GetFiles(root, "*.md"));
            AssertNoCreationArtifacts(root);
        }
        finally
        {
            blocking.Release();
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task IdExhaustionFailsBeforeCallingProviderOrCreatingFiles()
    {
        var root = CreateDirectory();
        try
        {
            var existing = Path.Combine(root, "9999-final-decision.md");
            await File.WriteAllTextAsync(existing, ValidContent);
            var before = await File.ReadAllTextAsync(existing);
            var provider = new RecordingProvider();

            var failure = await Assert.ThrowsAsync<InvalidOperationException>(
                () => GenerateAsync(
                    new AdrGenerationService(provider), root, "Use Redis"));

            Assert.Contains("9999", failure.Message, StringComparison.Ordinal);
            Assert.Equal(0, provider.Calls);
            Assert.Equal(before, await File.ReadAllTextAsync(existing));
            Assert.Single(Directory.GetFiles(root, "*.md"));
            AssertNoCreationArtifacts(root);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task InvalidCandidateNeverPersistsOrLeavesLockFiles()
    {
        var root = CreateDirectory();
        try
        {
            var documents = AdrDocumentLoader.LoadDirectory(root);
            const string invalid = "# Invalid\n\n## Status\n\nProposed\n";
            var preview = AdrCreationService.Prepare(
                root,
                "Invalid",
                invalid,
                documents,
                TestContext.Current.CancellationToken);

            Assert.False(preview.ValidationResult.IsValid);
            Assert.Empty(Directory.EnumerateFiles(root));
            AssertNoCreationArtifacts(root);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static Task<AdrGenerationOutcome> GenerateAsync(
        AdrGenerationService service,
        string root,
        string title,
        CancellationToken cancellationToken = default) =>
        service.GenerateAsync(
            root,
            title,
            "We need an architectural decision.",
            "en-US",
            [],
            includeExistingAdrs: false,
            dryRun: false,
            cancellationToken);

    private static async Task<(bool Written, Exception? Error)> ObserveAsync(
        Task<AdrGenerationOutcome> pending)
    {
        try
        {
            var outcome = await pending;
            return (outcome.Written, null);
        }
        catch (Exception exception)
        {
            return (false, exception);
        }
    }

    private static void AssertNoCreationArtifacts(string root)
    {
        Assert.DoesNotContain(
            Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories),
            path => Path.GetExtension(path) == ".tmp"
                || Path.GetFileName(path).Contains(
                    "lock", StringComparison.OrdinalIgnoreCase)
                || Path.GetFileName(path).Contains(
                    "reservation", StringComparison.OrdinalIgnoreCase));
    }

    private static string CreateDirectory()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"adr-guard-creation-{Guid.NewGuid():N}");
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

    private sealed class FixedProvider : IAdrGenerationProvider
    {
        public Task<AdrGenerationResult> GenerateAsync(
            AdrGenerationRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new AdrGenerationResult(
                "The service needs caching.",
                "Use Redis.",
                "Operate Redis."));
        }
    }

    private sealed class RecordingProvider : IAdrGenerationProvider
    {
        internal int Calls { get; private set; }

        public Task<AdrGenerationResult> GenerateAsync(
            AdrGenerationRequest request,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new AdrGenerationResult(
                "Context.", "Decision.", "Consequences."));
        }
    }

    private sealed class RendezvousProvider(int expectedCalls)
        : IAdrGenerationProvider
    {
        private readonly TaskCompletionSource _allEntered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _calls;

        public async Task<AdrGenerationResult> GenerateAsync(
            AdrGenerationRequest request,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _calls) == expectedCalls)
            {
                _allEntered.TrySetResult();
            }

            await _allEntered.Task.WaitAsync(
                TimeSpan.FromSeconds(10),
                cancellationToken);

            return new AdrGenerationResult(
                "The service needs caching.",
                "Use Redis.",
                "Operate Redis.");
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
            await _inner.WriteNewAsync(filePath, content, cancellationToken);
        }
    }
}
