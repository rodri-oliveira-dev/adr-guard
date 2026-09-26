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
                ReviewResult("Review completed."));
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = CliApplication.RunReviewForTests(
                ["review", path, "--provider", "openai", "--model", "test-model"],
                output,
                error,
                provider,
                TestContext.Current.CancellationToken);

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
                ReviewResult("Should not be used."));
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = CliApplication.RunReviewForTests(
                ["review", path, "--provider", "openai", "--model", "test-model"],
                output,
                error,
                provider,
                TestContext.Current.CancellationToken);

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
                provider,
                TestContext.Current.CancellationToken);

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

            var provider = new RecordingReviewProvider(ReviewResult("Reviewed."));
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = CliApplication.RunReviewForTests(
                ["review", target, "--provider", "openai", "--model", "test", "--context-file", context],
                output,
                error,
                provider,
                TestContext.Current.CancellationToken);

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

            var provider = new RecordingReviewProvider(ReviewResult("Reviewed."));
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = CliApplication.RunReviewForTests(
                ["review", target, "--provider", "openai", "--model", "test", "--include-existing-adrs"],
                output,
                error,
                provider,
                TestContext.Current.CancellationToken);

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

            var provider = new RecordingReviewProvider(ReviewResult("Should not run."));
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = CliApplication.RunReviewForTests(
                ["review", target, "--provider", "openai", "--model", "test", "--context-file", Path.Combine(root, "missing.txt")],
                output,
                error,
                provider,
                TestContext.Current.CancellationToken);

            Assert.Equal(ExitCodes.OperationalError, exitCode);
            Assert.Equal(0, provider.CallCount);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void CrossAdrEvidenceIsAbsentWithoutOptIn()
    {
        var root = CreateTempDirectory();

        try
        {
            var target = Path.Combine(root, "0001-use-redis.md");
            var sibling = Path.Combine(root, "0002-avoid-redis.md");
            File.WriteAllText(target, ValidMarkdown());
            File.WriteAllText(
                sibling,
                ValidMarkdown()
                    .Replace("Use Redis", "Avoid Redis", StringComparison.Ordinal)
                    .Replace("Use Redis.", "Do not use Redis.", StringComparison.Ordinal));

            var provider = new RecordingReviewProvider(ReviewResult("Reviewed."));
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = CliApplication.RunReviewForTests(
                ["review", target, "--provider", "openai", "--model", "test"],
                output,
                error,
                provider,
                TestContext.Current.CancellationToken);

            Assert.Equal(ExitCodes.Success, exitCode);
            Assert.DoesNotContain(
                "Cross-ADR comparison evidence",
                provider.LastRequest!.ProviderContext,
                StringComparison.Ordinal);
            Assert.DoesNotContain("0002-avoid-redis.md", provider.LastRequest.ProviderContext, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void CrossAdrEvidenceIncludesBothIdsStatusesDecisionsAndLinksWhenOptedIn()
    {
        var root = CreateTempDirectory();

        try
        {
            var target = Path.Combine(root, "0001-use-redis.md");
            var sibling = Path.Combine(root, "0002-avoid-redis.md");

            File.WriteAllText(
                target,
                """
                # Use Redis

                ## Status
                Accepted

                ## Context
                Shared caching is required.

                ## Decision
                Use Redis for shared caching.

                ## Consequences
                Redis must be operated and monitored.

                ## Related
                See [ADR 0002](0002-avoid-redis.md).
                """);

            File.WriteAllText(
                sibling,
                """
                # Avoid Redis

                ## Status
                Proposed

                ## Context
                Managed dependencies should be minimized.

                ## Decision
                Do not use Redis for shared caching.

                ## Consequences
                Use application-local caching only.
                """);

            var provider = new RecordingReviewProvider(
                new AdrReviewResult(
                [
                    new AdrReviewFinding(
                        "architectural-consistency",
                        "potential-risk",
                        "0001-use-redis.md <-> 0002-avoid-redis.md",
                        "Use Redis for shared caching. / Do not use Redis for shared caching.",
                        "The selected decisions are textually inconsistent for the same stated caching scope.",
                        "Confirm intended scope and decide whether one ADR supersedes or narrows the other."),
                ]));

            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = CliApplication.RunReviewForTests(
                ["review", target, "--provider", "openai", "--model", "test", "--include-existing-adrs"],
                output,
                error,
                provider,
                TestContext.Current.CancellationToken);

            Assert.Equal(ExitCodes.Success, exitCode);
            Assert.Contains("Target ADR 0001", provider.LastRequest!.ProviderContext, StringComparison.Ordinal);
            Assert.Contains("Candidate ADR 0002", provider.LastRequest.ProviderContext, StringComparison.Ordinal);
            Assert.Contains("Status: Accepted", provider.LastRequest.ProviderContext, StringComparison.Ordinal);
            Assert.Contains("Status: Proposed", provider.LastRequest.ProviderContext, StringComparison.Ordinal);
            Assert.Contains("0002-avoid-redis.md", provider.LastRequest.ProviderContext, StringComparison.Ordinal);
            Assert.Contains("potential-risk", output.ToString(), StringComparison.Ordinal);
            Assert.Contains("0001-use-redis.md <-> 0002-avoid-redis.md", output.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void CrossAdrContractQualifiesSupersededAndDifferentScopeCases()
    {
        var instructions = AdrReviewContract.BuildInstructions();

        Assert.Contains("Deprecated and Superseded", instructions, StringComparison.Ordinal);
        Assert.Contains("different scopes or time periods", instructions, StringComparison.Ordinal);
        Assert.Contains("not enough information", instructions, StringComparison.Ordinal);
        Assert.Contains("name both ADR IDs", instructions, StringComparison.Ordinal);
    }

    [Fact]
    public void ReviewAllowsValidSiblingReferenceWithoutTransmittingSiblingByDefault()
    {
        var root = CreateTempDirectory();

        try
        {
            var target = Path.Combine(root, "0001-use-redis.md");
            var sibling = Path.Combine(root, "0002-use-postgres.md");

            File.WriteAllText(
                target,
                ValidMarkdown()
                    + Environment.NewLine
                    + Environment.NewLine
                    + "## Related"
                    + Environment.NewLine
                    + "[ADR 0002](0002-use-postgres.md)");
            File.WriteAllText(
                sibling,
                ValidMarkdown().Replace("Use Redis", "Use Postgres", StringComparison.Ordinal));

            var provider = new RecordingReviewProvider(ReviewResult("Reviewed."));
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = CliApplication.RunReviewForTests(
                ["review", target, "--provider", "openai", "--model", "test"],
                output,
                error,
                provider,
                TestContext.Current.CancellationToken);

            Assert.Equal(ExitCodes.Success, exitCode);
            Assert.Equal(1, provider.CallCount);
            Assert.DoesNotContain("0002-use-postgres.md", provider.LastRequest!.ProviderContext, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static AdrReviewResult ReviewResult(string explanation)
    {
        var findings = AdrReviewContract.Dimensions
            .Select((dimension, index) =>
                new AdrReviewFinding(
                    dimension,
                    index == 5 ? "not-applicable" : index == 2 ? "missing-context" : "observed-evidence",
                    index == 5 ? null : "0001-use-redis.md",
                    index == 5 ? null : "Decision excerpt",
                    index == 2 ? "not enough information" : explanation,
                    "Human reviewer should verify this dimension."))
            .ToArray();

        return new AdrReviewResult(findings);
    }

    [Fact]
    public void ReviewContractCoversEightDimensionsAndUncertainty()
    {
        var result = ReviewResult("Evidence observed.");
        var rendered = result.ToHumanReadable();

        Assert.Equal(8, result.Findings.Count);
        Assert.All(AdrReviewContract.Dimensions, dimension =>
            Assert.Contains(dimension, rendered, StringComparison.Ordinal));
        Assert.Contains("not enough information", rendered, StringComparison.Ordinal);
        Assert.Contains("not-applicable", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("approved", rendered, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rejected", rendered, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReviewContractInstructionsRequireGroundedEvidence()
    {
        var instructions = AdrReviewContract.BuildInstructions();

        Assert.Contains("actual selected source", instructions, StringComparison.Ordinal);
        Assert.Contains("Never invent line numbers", instructions, StringComparison.Ordinal);
        Assert.Contains("not enough information", instructions, StringComparison.Ordinal);
        Assert.Contains("Never approve/reject", instructions, StringComparison.Ordinal);
        Assert.Contains("fallible", instructions, StringComparison.Ordinal);
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
