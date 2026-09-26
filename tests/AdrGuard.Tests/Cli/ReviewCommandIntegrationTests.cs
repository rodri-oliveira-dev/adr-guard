using AdrGuard.Cli;
using AdrGuard.Review;
using Xunit;

namespace AdrGuard.Tests.Cli;

public sealed class ReviewCommandIntegrationTests
{
    [Fact]
    public void ReviewHelpReturnsSuccess()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = CliApplication.Run(
            ["review", "--help"],
            output,
            error);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("adr-guard review", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("advisory", output.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void ReviewValidAdrUsesProviderAndDoesNotModifyFile()
    {
        var root = CreateTempDirectory();

        try
        {
            var path = Path.Combine(root, "0001-use-redis.md");
            var original = ValidMarkdown();
            File.WriteAllText(path, original);

            var provider = new RecordingReviewProvider(
                new AdrReviewResult("Review completed."));
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = CliApplication.RunReviewForTests(
                ["review", path, "--provider", "openai", "--model", "test-model"],
                output,
                error,
                provider);

            Assert.Equal(ExitCodes.Success, exitCode);
            Assert.Equal(string.Empty, error.ToString());
            Assert.Equal(1, provider.CallCount);
            Assert.Equal(original, File.ReadAllText(path));
            Assert.Contains("Review completed.", output.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ReviewInvalidAdrDoesNotInvokeProvider()
    {
        var root = CreateTempDirectory();

        try
        {
            var path = Path.Combine(root, "0001-use-redis.md");
            File.WriteAllText(path, "# Use Redis");

            var provider = new RecordingReviewProvider(
                new AdrReviewResult("Should not be used."));
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = CliApplication.RunReviewForTests(
                ["review", path, "--provider", "openai", "--model", "test-model"],
                output,
                error,
                provider);

            Assert.Equal(ExitCodes.ValidationFailed, exitCode);
            Assert.Equal(0, provider.CallCount);
            Assert.Contains("structurally invalid", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ReviewCancellationHasOperationalExit()
    {
        var root = CreateTempDirectory();

        try
        {
            var path = Path.Combine(root, "0001-use-redis.md");
            File.WriteAllText(path, ValidMarkdown());

            var provider = new CancelingReviewProvider();
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = CliApplication.RunReviewForTests(
                ["review", path, "--provider", "openai", "--model", "test-model"],
                output,
                error,
                provider);

            Assert.Equal(ExitCodes.OperationalError, exitCode);
            Assert.Contains("canceled", error.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ReviewUsesOnlyExplicitContextFilesAndDisclosesSources()
    {
        var root = CreateTempDirectory();

        try
        {
            var target = Path.Combine(root, "0001-use-redis.md");
            var context = Path.Combine(root, "requirements.txt");
            File.WriteAllText(target, ValidMarkdown());
            File.WriteAllText(context, "Latency must stay below 50 ms.");
            File.WriteAllText(Path.Combine(root, "secret.txt"), "must not be discovered");

            var provider = new RecordingReviewProvider(new AdrReviewResult("Reviewed."));
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = CliApplication.RunReviewForTests(
                ["review", target, "--provider", "openai", "--model", "test", "--context-file", context],
                output,
                error,
                provider);

            Assert.Equal(ExitCodes.Success, exitCode);
            Assert.Equal(1, provider.CallCount);
            Assert.Contains("requirements.txt", provider.LastRequest!.ProviderContext, StringComparison.Ordinal);
            Assert.Contains("Latency must stay below 50 ms.", provider.LastRequest.ProviderContext, StringComparison.Ordinal);
            Assert.DoesNotContain("secret.txt", provider.LastRequest.ProviderContext, StringComparison.Ordinal);
            Assert.DoesNotContain(root, provider.LastRequest.ProviderContext, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ReviewIncludesExistingAdrsOnlyAfterOptInAndDeduplicatesTarget()
    {
        var root = CreateTempDirectory();

        try
        {
            var target = Path.Combine(root, "0001-use-redis.md");
            var sibling = Path.Combine(root, "0002-use-postgres.md");
            File.WriteAllText(target, ValidMarkdown());
            File.WriteAllText(sibling, ValidMarkdown().Replace("Use Redis", "Use Postgres", StringComparison.Ordinal));

            var provider = new RecordingReviewProvider(new AdrReviewResult("Reviewed."));
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = CliApplication.RunReviewForTests(
                ["review", target, "--provider", "openai", "--model", "test", "--include-existing-adrs"],
                output,
                error,
                provider);

            Assert.Equal(ExitCodes.Success, exitCode);
            Assert.Contains("ADR 0002", provider.LastRequest!.ProviderContext, StringComparison.Ordinal);
            Assert.DoesNotContain("ADR 0001", provider.LastRequest.ProviderContext, StringComparison.Ordinal);
            Assert.Contains("Warning:", output.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void MissingContextFileFailsBeforeProviderInvocation()
    {
        var root = CreateTempDirectory();

        try
        {
            var target = Path.Combine(root, "0001-use-redis.md");
            File.WriteAllText(target, ValidMarkdown());

            var provider = new RecordingReviewProvider(new AdrReviewResult("Should not run."));
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = CliApplication.RunReviewForTests(
                ["review", target, "--provider", "openai", "--model", "test", "--context-file", Path.Combine(root, "missing.txt")],
                output,
                error,
                provider);

            Assert.Equal(ExitCodes.OperationalError, exitCode);
            Assert.Equal(0, provider.CallCount);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"adr-guard-review-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static string ValidMarkdown() =>
        """
        # Use Redis

        ## Status
        Proposed

        ## Context
        We need distributed caching.

        ## Decision
        Use Redis.

        ## Consequences
        Redis must be operated and monitored.
        """;

    private sealed class RecordingReviewProvider(
        AdrReviewResult result) : IAdrReviewProvider
    {
        internal int CallCount { get; private set; }

        internal AdrReviewRequest? LastRequest { get; private set; }

        public Task<AdrReviewResult> ReviewAsync(
            AdrReviewRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastRequest = request;
            return Task.FromResult(result);
        }
    }

    private sealed class CancelingReviewProvider : IAdrReviewProvider
    {
        public Task<AdrReviewResult> ReviewAsync(
            AdrReviewRequest request,
            CancellationToken cancellationToken) =>
            throw new OperationCanceledException(cancellationToken);
    }
}
