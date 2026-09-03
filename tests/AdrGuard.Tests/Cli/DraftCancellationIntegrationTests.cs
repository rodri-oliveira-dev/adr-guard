using AdrGuard.Cli;
using AdrGuard.Generation;
using AdrGuard.Generation.Http;
using Xunit;

namespace AdrGuard.Tests.Cli;

public sealed class DraftCancellationIntegrationTests
{
    [Fact]
    public void DraftPropagatesCallerCancellationToProviderWithoutArtifacts()
    {
        var root = CreateTempDirectory();

        try
        {
            using var cancellationSource =
                new CancellationTokenSource();

            var provider =
                new CancelingAdrGenerationProvider(
                    cancellationSource);

            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = CliApplication.Run(
                [
                    "draft",
                    root,
                    "--title",
                    "Use Redis",
                    "--context",
                    "We need distributed caching.",
                ],
                output,
                error,
                cancellationSource.Token,
                generationProvider: provider);

            Assert.Equal(
                ExitCodes.OperationalError,
                exitCode);
            Assert.True(provider.ReceivedCancelableToken);
            Assert.Empty(
                Directory.EnumerateFiles(
                    root,
                    "*.md"));
            AssertNoTemporaryFiles(root);
            Assert.Contains(
                "canceled",
                error.ToString(),
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void DraftProviderTimeoutLeavesNoArtifacts()
    {
        var root = CreateTempDirectory();

        try
        {
            var provider =
                new FailingAdrGenerationProvider();

            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = CliApplication.Run(
                [
                    "draft",
                    root,
                    "--title",
                    "Use Redis",
                    "--context",
                    "We need distributed caching.",
                ],
                output,
                error,
                generationProvider: provider);

            Assert.Equal(
                ExitCodes.OperationalError,
                exitCode);
            Assert.Empty(
                Directory.EnumerateFiles(
                    root,
                    "*.md"));
            AssertNoTemporaryFiles(root);
            Assert.Contains(
                "timed out",
                error.ToString(),
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static void AssertNoTemporaryFiles(
        string directoryPath)
    {
        Assert.DoesNotContain(
            Directory.EnumerateFiles(directoryPath),
            filePath =>
                Path.GetFileName(filePath)
                    .EndsWith(
                        AtomicAdrDraftFilePersistence
                            .TemporaryFileSuffix,
                        StringComparison.Ordinal));
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"adr-guard-cancellation-{Guid.NewGuid():N}");

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

    private sealed class FailingAdrGenerationProvider
        : IAdrGenerationProvider
    {
        public Task<AdrGenerationResult> GenerateAsync(
            AdrGenerationRequest request,
            CancellationToken cancellationToken) =>
            Task.FromException<AdrGenerationResult>(
                new AiProviderException(
                    AiProviderErrorKind.Timeout,
                    "AI provider timed out."));
    }

    private sealed class CancelingAdrGenerationProvider(
        CancellationTokenSource cancellationSource)
        : IAdrGenerationProvider
    {
        internal bool ReceivedCancelableToken
        {
            get;
            private set;
        }

        public Task<AdrGenerationResult> GenerateAsync(
            AdrGenerationRequest request,
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
}
