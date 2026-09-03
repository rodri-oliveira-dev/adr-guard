using AdrGuard.Generation;
using Xunit;

namespace AdrGuard.Tests.Generation;

public sealed class AdrGenerationServiceCancellationTests
{
    [Fact]
    public async Task GenerateAsyncPropagatesCallerCancellationToPersistence()
    {
        var root = CreateTempDirectory();

        try
        {
            using var cancellationSource =
                new CancellationTokenSource();

            var persistence =
                new CancelingPersistence(
                    cancellationSource);

            var service = new AdrGenerationService(
                CreateProvider(),
                persistence);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => service.GenerateAsync(
                    root,
                    "Use Redis",
                    "We need distributed caching.",
                    "en-US",
                    [],
                    includeExistingAdrs: false,
                    dryRun: false,
                    cancellationSource.Token));

            Assert.True(
                persistence.ReceivedCancelableToken);
            Assert.Empty(
                Directory.EnumerateFiles(
                    root,
                    "*.md"));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task GenerateAsyncDryRunNeverInvokesPersistence()
    {
        var root = CreateTempDirectory();

        try
        {
            var persistence =
                new RecordingPersistence();

            var service = new AdrGenerationService(
                CreateProvider(),
                persistence);

            var result = await service.GenerateAsync(
                root,
                "Use Redis",
                "We need distributed caching.",
                "en-US",
                [],
                includeExistingAdrs: false,
                dryRun: true,
                TestContext.Current.CancellationToken);

            Assert.False(result.Written);
            Assert.Equal(0, persistence.CallCount);
            Assert.Empty(
                Directory.EnumerateFiles(
                    root,
                    "*.md"));
            Assert.DoesNotContain(
                Directory.EnumerateFiles(root),
                filePath =>
                    Path.GetFileName(filePath)
                        .EndsWith(
                            AtomicAdrDraftFilePersistence
                                .TemporaryFileSuffix,
                            StringComparison.Ordinal));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static IAdrGenerationProvider CreateProvider() =>
        new FixedProvider(
            new AdrGenerationResult(
                "The service needs distributed caching.",
                "Use Redis for distributed caching.",
                "The team must operate Redis."));

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"adr-guard-service-cancellation-{Guid.NewGuid():N}");

        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(
        string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(
                path,
                recursive: true);
        }
    }

    private sealed class FixedProvider(
        AdrGenerationResult result)
        : IAdrGenerationProvider
    {
        public Task<AdrGenerationResult> GenerateAsync(
            AdrGenerationRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(result);
        }
    }

    private sealed class CancelingPersistence(
        CancellationTokenSource cancellationSource)
        : IAdrDraftFilePersistence
    {
        internal bool ReceivedCancelableToken
        {
            get;
            private set;
        }

        public Task WriteNewAsync(
            string filePath,
            string content,
            CancellationToken cancellationToken)
        {
            ReceivedCancelableToken =
                cancellationToken.CanBeCanceled;

            cancellationSource.Cancel();
            cancellationToken.ThrowIfCancellationRequested();

            throw new InvalidOperationException(
                "Cancellation was not propagated.");
        }
    }

    private sealed class RecordingPersistence
        : IAdrDraftFilePersistence
    {
        internal int CallCount
        {
            get;
            private set;
        }

        public Task WriteNewAsync(
            string filePath,
            string content,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.CompletedTask;
        }
    }
}
