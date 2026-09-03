using AdrGuard.Cli;
using AdrGuard.Generation;
using Xunit;

namespace AdrGuard.Tests.Cli;

public sealed class DraftCommandIntegrationTests
{
    [Fact]
    public void DraftHelpReturnsSuccess()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = CliApplication.Run(["draft", "--help"], output, error);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("adr-guard draft", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("--title", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("--context", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("--culture", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("--provider", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("--model", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("--endpoint", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("--include-existing-adrs", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("12000", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("OPENAI_API_KEY", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("ANTHROPIC_API_KEY", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("GEMINI_API_KEY", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("ADR_GUARD_OPENAI_COMPATIBLE_API_KEY", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("en-US", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void DraftWithFakeProviderWritesValidatedProposedAdrUsingNextId()
    {
        var root = CreateTempDirectory();

        try
        {
            WriteAdr(root, "0001-use-postgresql.md", ValidMarkdown("Use PostgreSQL", "Accepted"));
            WriteAdr(root, "0003-use-redis.md", ValidMarkdown("Use Redis", "Accepted"));
            var originalFirstAdr = File.ReadAllText(Path.Combine(root, "0001-use-postgresql.md"));
            var provider = CreateValidProvider();
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = CliApplication.Run(
                [
                    "draft",
                    root,
                    "--title",
                    "Adopt a Message Broker",
                    "--context",
                    "We need to decouple producers and consumers.",
                ],
                output,
                error,
                provider);

            Assert.Equal(ExitCodes.Success, exitCode);
            Assert.Equal(string.Empty, error.ToString());
            Assert.NotNull(provider.LastRequest);
            Assert.Equal("Adopt a Message Broker", provider.LastRequest.Title);
            Assert.Equal(
                "We need to decouple producers and consumers.",
                provider.LastRequest.Context);
            Assert.Equal("en-US", provider.LastRequest.CultureName);

            var draftPath = Path.Combine(root, "0004-adopt-a-message-broker.md");
            Assert.True(File.Exists(draftPath));

            var content = File.ReadAllText(draftPath);
            Assert.Contains("# Adopt a Message Broker", content, StringComparison.Ordinal);
            Assert.Contains("## Status", content, StringComparison.Ordinal);
            Assert.Contains("Proposed", content, StringComparison.Ordinal);
            Assert.Contains("## Context", content, StringComparison.Ordinal);
            Assert.Contains("## Decision", content, StringComparison.Ordinal);
            Assert.Contains("## Consequences", content, StringComparison.Ordinal);
            Assert.Equal(
                originalFirstAdr,
                File.ReadAllText(Path.Combine(root, "0001-use-postgresql.md")));

            using var checkOutput = new StringWriter();
            using var checkError = new StringWriter();

            var checkExitCode = CliApplication.Run(
                ["check", root],
                checkOutput,
                checkError);

            Assert.Equal(ExitCodes.Success, checkExitCode);
            Assert.Equal(string.Empty, checkError.ToString());
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void DraftCanOptInToParsedExistingAdrContext()
    {
        var root = CreateTempDirectory();

        try
        {
            WriteAdr(
                root,
                "0001-use-postgresql.md",
                """
                # Use PostgreSQL

                ## Status
                Accepted

                ## Context
                We need relational persistence.

                ## Decision
                Use PostgreSQL for transactional data.

                ## Consequences
                The team operates PostgreSQL.
                """);

            WriteAdr(
                root,
                "0002-use-redis.md",
                """
                # Use Redis

                ## Status
                Accepted

                ## Context
                We need distributed caching.

                ## Decision
                Use Redis for distributed caching.

                ## Consequences
                Redis becomes an operational dependency.

                ## Related
                [Database decision](0001-use-postgresql.md)
                """);

            var provider = CreateValidProvider();
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = CliApplication.Run(
                [
                    "draft",
                    root,
                    "--title",
                    "Adopt a Message Broker",
                    "--context",
                    "We need asynchronous integration.",
                    "--include-existing-adrs",
                ],
                output,
                error,
                provider);

            Assert.Equal(ExitCodes.Success, exitCode);
            Assert.Equal(string.Empty, error.ToString());
            Assert.NotNull(provider.LastRequest);
            Assert.Contains(
                "User-supplied architectural context:",
                provider.LastRequest.Context,
                StringComparison.Ordinal);
            Assert.Contains(
                "We need asynchronous integration.",
                provider.LastRequest.Context,
                StringComparison.Ordinal);
            Assert.Contains(
                "ADR 0001",
                provider.LastRequest.Context,
                StringComparison.Ordinal);
            Assert.Contains(
                "Title: Use PostgreSQL",
                provider.LastRequest.Context,
                StringComparison.Ordinal);
            Assert.Contains(
                "Status: Accepted",
                provider.LastRequest.Context,
                StringComparison.Ordinal);
            Assert.Contains(
                "Use PostgreSQL for transactional data.",
                provider.LastRequest.Context,
                StringComparison.Ordinal);
            Assert.Contains(
                "- 0001-use-postgresql.md",
                provider.LastRequest.Context,
                StringComparison.Ordinal);
            Assert.Contains(
                "will be sent to the configured provider",
                output.ToString(),
                StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void DraftDoesNotIncludeExistingAdrContextWithoutExplicitOptIn()
    {
        var root = CreateTempDirectory();

        try
        {
            WriteAdr(root, "0001-use-postgresql.md", ValidMarkdown("Use PostgreSQL", "Accepted"));
            var provider = CreateValidProvider();
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = CliApplication.Run(
                [
                    "draft",
                    root,
                    "--title",
                    "Use Redis",
                    "--context",
                    "Only this inline context should be sent.",
                ],
                output,
                error,
                provider);

            Assert.Equal(ExitCodes.Success, exitCode);
            Assert.NotNull(provider.LastRequest);
            Assert.Equal(
                "Only this inline context should be sent.",
                provider.LastRequest.Context);
            Assert.DoesNotContain(
                "Existing ADR context enabled",
                output.ToString(),
                StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void DraftPropagatesExplicitCultureWithoutChangingAdrStructure()
    {
        var root = CreateTempDirectory();

        try
        {
            var provider = new FakeAdrGenerationProvider(
                new AdrGenerationResult(
                    "O serviço precisa de cache distribuído.",
                    "Usar Redis como cache distribuído.",
                    "A equipe deverá operar e monitorar o Redis."));
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = CliApplication.Run(
                [
                    "draft",
                    root,
                    "--title",
                    "Usar cache distribuído",
                    "--context",
                    "Precisamos reduzir a latência de leitura.",
                    "--culture",
                    "pt-br",
                ],
                output,
                error,
                provider);

            Assert.Equal(ExitCodes.Success, exitCode);
            Assert.Equal(string.Empty, error.ToString());
            Assert.NotNull(provider.LastRequest);
            Assert.Equal("pt-BR", provider.LastRequest.CultureName);

            var draftPath = Path.Combine(root, "0001-usar-cache-distribuido.md");
            Assert.True(File.Exists(draftPath));

            var content = File.ReadAllText(draftPath);
            Assert.Contains("# Usar cache distribuído", content, StringComparison.Ordinal);
            Assert.Contains("## Status", content, StringComparison.Ordinal);
            Assert.Contains("Proposed", content, StringComparison.Ordinal);
            Assert.Contains("## Context", content, StringComparison.Ordinal);
            Assert.Contains("## Decision", content, StringComparison.Ordinal);
            Assert.Contains("## Consequences", content, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void DraftRejectsInvalidCultureBeforeInvokingProviderOrWritingFile()
    {
        var root = CreateTempDirectory();

        try
        {
            var provider = CreateValidProvider();
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
                    "--culture",
                    "this-is-not-a-valid-culture-name",
                ],
                output,
                error,
                provider);

            Assert.Equal(ExitCodes.UsageError, exitCode);
            Assert.Equal(0, provider.CallCount);
            Assert.Empty(Directory.EnumerateFiles(root, "*.md"));
            Assert.Contains("Invalid culture", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void DraftRejectsInvalidGeneratedContentWithoutWritingFile()
    {
        var root = CreateTempDirectory();

        try
        {
            var provider = new FakeAdrGenerationProvider(
                new AdrGenerationResult(
                    "Context is present.",
                    string.Empty,
                    "Consequences are present."));
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = CliApplication.Run(
                [
                    "draft",
                    root,
                    "--title",
                    "Use PostgreSQL",
                    "--context",
                    "We need persistent relational storage.",
                ],
                output,
                error,
                provider);

            Assert.Equal(ExitCodes.ValidationFailed, exitCode);
            Assert.False(File.Exists(Path.Combine(root, "0001-use-postgresql.md")));
            Assert.Contains("ADR005", error.ToString(), StringComparison.Ordinal);
            Assert.Contains("Validation failed", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void DraftDoesNotInvokeProviderWhenExistingAdrsAreInvalid()
    {
        var root = CreateTempDirectory();

        try
        {
            WriteAdr(root, "0001-use-postgresql.md", ValidMarkdown("Use PostgreSQL", "Approved"));
            var provider = CreateValidProvider();
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

            Assert.Equal(ExitCodes.ValidationFailed, exitCode);
            Assert.Equal(0, provider.CallCount);
            Assert.False(File.Exists(Path.Combine(root, "0002-use-redis.md")));
            Assert.Contains("ADR004", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void DraftWithoutRuntimeProviderSelectionReturnsUsageError()
    {
        var root = CreateTempDirectory();

        try
        {
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
                error);

            Assert.Equal(ExitCodes.UsageError, exitCode);
            Assert.Empty(Directory.EnumerateFiles(root, "*.md"));
            Assert.Contains(
                "'draft' requires both --provider and --model.",
                error.ToString(),
                StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void DraftRejectsMissingRequiredArguments()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = CliApplication.Run(
            ["draft", "--title", "Use Redis"],
            output,
            error);

        Assert.Equal(ExitCodes.UsageError, exitCode);
        Assert.Contains("Invalid arguments for 'draft'", error.ToString(), StringComparison.Ordinal);
    }

    private static FakeAdrGenerationProvider CreateValidProvider() =>
        new(
            new AdrGenerationResult(
                "The service needs asynchronous integration.",
                "Use a message broker for asynchronous integration.",
                "The team must operate and monitor the broker."));

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"adr-guard-draft-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static void WriteAdr(
        string directoryPath,
        string fileName,
        string markdown) =>
        File.WriteAllText(Path.Combine(directoryPath, fileName), markdown);

    private static string ValidMarkdown(string title, string status) =>
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
        AdrGenerationResult result) : IAdrGenerationProvider
    {
        internal int CallCount { get; private set; }

        internal AdrGenerationRequest? LastRequest { get; private set; }

        public Task<AdrGenerationResult> GenerateAsync(
            AdrGenerationRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastRequest = request;
            return Task.FromResult(result);
        }
    }
}
