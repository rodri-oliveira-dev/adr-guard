using AdrGuard.Cli;
using AdrGuard.Generation;
using Xunit;

namespace AdrGuard.Tests.Cli;

public sealed class DraftWorkflowIntegrationTests
{
    [Fact]
    public void PersistedDraftRemainsCompatibleWithCheckAndIndex()
    {
        var root = CreateTempDirectory();

        try
        {
            WriteAdr(
                root,
                "0001-use-postgresql.md",
                ValidMarkdown("Use PostgreSQL", "Accepted"));

            var provider = new FakeAdrGenerationProvider(
                new AdrGenerationResult(
                    "The service needs asynchronous integration.",
                    "Use a message broker for asynchronous integration.",
                    "The team must operate and monitor the broker."));

            using var draftOutput = new StringWriter();
            using var draftError = new StringWriter();

            var draftExitCode = CliApplication.Run(
                [
                    "draft",
                    root,
                    "--title",
                    "Adopt a Message Broker",
                    "--context",
                    "We need to decouple producers and consumers.",
                ],
                draftOutput,
                draftError,
                provider);

            Assert.Equal(ExitCodes.Success, draftExitCode);
            Assert.Equal(string.Empty, draftError.ToString());

            var draftPath = Path.Combine(
                root,
                "0002-adopt-a-message-broker.md");
            Assert.True(File.Exists(draftPath));

            using var checkOutput = new StringWriter();
            using var checkError = new StringWriter();

            var checkExitCode = CliApplication.Run(
                ["check", root],
                checkOutput,
                checkError);

            Assert.Equal(ExitCodes.Success, checkExitCode);
            Assert.Equal(string.Empty, checkError.ToString());

            using var indexOutput = new StringWriter();
            using var indexError = new StringWriter();

            var indexExitCode = CliApplication.Run(
                ["index", root],
                indexOutput,
                indexError);

            Assert.Equal(ExitCodes.Success, indexExitCode);
            Assert.Equal(string.Empty, indexError.ToString());

            var indexContent = File.ReadAllText(
                Path.Combine(root, "README.md"));

            Assert.Contains(
                "[0002](0002-adopt-a-message-broker.md)",
                indexContent,
                StringComparison.Ordinal);
            Assert.Contains(
                "Adopt a Message Broker",
                indexContent,
                StringComparison.Ordinal);
            Assert.Contains(
                "Proposed",
                indexContent,
                StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void PersistedDraftNeverOverwritesFileCreatedDuringGeneration()
    {
        var root = CreateTempDirectory();

        try
        {
            var candidatePath = Path.Combine(
                root,
                "0001-use-redis.md");
            const string competingContent =
                "This file was created concurrently and must not be overwritten.";

            var provider = new CallbackAdrGenerationProvider(
                () => File.WriteAllText(
                    candidatePath,
                    competingContent),
                new AdrGenerationResult(
                    "The service needs distributed caching.",
                    "Use Redis for distributed caching.",
                    "The team must operate Redis."));

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
                provider);

            Assert.Equal(ExitCodes.OperationalError, exitCode);
            Assert.Equal(
                competingContent,
                File.ReadAllText(candidatePath));
            Assert.Contains(
                "already exists",
                error.ToString(),
                StringComparison.OrdinalIgnoreCase);
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

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"adr-guard-draft-workflow-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(
                path,
                recursive: true);
        }
    }

    private static void WriteAdr(
        string directoryPath,
        string fileName,
        string markdown) =>
        File.WriteAllText(
            Path.Combine(directoryPath, fileName),
            markdown);

    private static string ValidMarkdown(
        string title,
        string status) =>
        $"""
        # {title}

        ## Status
        {status}

        ## Context
        Context.

        ## Decision
        Decision.

        ## Consequences
        Consequences.
        """;

    private sealed class FakeAdrGenerationProvider(
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

    private sealed class CallbackAdrGenerationProvider(
        Action callback,
        AdrGenerationResult result)
        : IAdrGenerationProvider
    {
        public Task<AdrGenerationResult> GenerateAsync(
            AdrGenerationRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            callback();
            return Task.FromResult(result);
        }
    }
}
