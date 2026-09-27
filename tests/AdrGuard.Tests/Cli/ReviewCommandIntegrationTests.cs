using AdrGuard.Cli;
using AdrGuard.Generation;
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
        Assert.Contains("UTF-8", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("150000 bytes", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Every transmitted source", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("--context-file <path>", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("--include-existing-adrs", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Exit codes:", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void ReviewInvalidUsageReturnsUsageError()
    {
        IReadOnlyList<string[]> invalidArguments =
        [
            ["review"],
            ["review", "0001-use-redis.md", "--provider", "openai"],
            ["review", "0001-use-redis.md", "--model", "test-model"],
        ];

        foreach (var args in invalidArguments)
        {
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = CliApplication.Run(
                args,
                output,
                error);

            Assert.Equal(ExitCodes.UsageError, exitCode);
            Assert.Equal(string.Empty, output.ToString());
            Assert.Contains("review", error.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.Contains("usage", error.ToString(), StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ReviewRejectsNonMarkdownTargetBeforeProviderInvocation()
    {
        var root = CreateTempDirectory();

        try
        {
            var target = Path.Combine(root, "decision.txt");
            File.WriteAllText(target, "not an ADR");

            var provider = new RecordingReviewProvider(
                ReviewResult("Should not run."));
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = CliApplication.Run(
                ["review", target, "--provider", "openai", "--model", "test-model"],
                output,
                error,
                TestContext.Current.CancellationToken,
                reviewProvider: provider);

            Assert.Equal(ExitCodes.UsageError, exitCode);
            Assert.Equal(0, provider.CallCount);
            Assert.Contains(".md extension", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ReviewRejectsUnsupportedProviderAsUsageError()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = CliApplication.Run(
            [
                "review",
                "0001-use-redis.md",
                "--provider",
                "unsupported",
                "--model",
                "test-model",
            ],
            output,
            error,
            TestContext.Current.CancellationToken);

        Assert.Equal(ExitCodes.UsageError, exitCode);
        Assert.Contains(
            "Unsupported AI provider",
            error.ToString(),
            StringComparison.Ordinal);
        Assert.Contains(
            "review --help",
            error.ToString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void ReviewProviderFailureIsOperationalAndDoesNotModifyAdrOrIndex()
    {
        var root = CreateTempDirectory();

        try
        {
            var target = Path.Combine(root, "0001-use-redis.md");
            var index = Path.Combine(root, "README.md");
            var originalAdr = ValidMarkdown();
            const string originalIndex = "# Existing ADR Index";
            File.WriteAllText(target, originalAdr);
            File.WriteAllText(index, originalIndex);

            var originalFiles = Directory
                .EnumerateFiles(root)
                .Select(Path.GetFileName)
                .Order(StringComparer.Ordinal)
                .ToArray();

            var provider = new FailingReviewProvider();
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = CliApplication.Run(
                ["review", target, "--provider", "openai", "--model", "test-model"],
                output,
                error,
                TestContext.Current.CancellationToken,
                reviewProvider: provider);

            Assert.Equal(ExitCodes.OperationalError, exitCode);
            Assert.Contains(
                "provider failed",
                error.ToString(),
                StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(
                "structurally invalid",
                error.ToString(),
                StringComparison.OrdinalIgnoreCase);
            Assert.Equal(originalAdr, File.ReadAllText(target));
            Assert.Equal(originalIndex, File.ReadAllText(index));
            Assert.Equal(
                originalFiles,
                Directory
                    .EnumerateFiles(root)
                    .Select(Path.GetFileName)
                    .Order(StringComparer.Ordinal)
                    .ToArray());
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ReviewValidAdrUsesProviderAndDoesNotModifyFile()
    {
        var root = CreateTempDirectory();

        try
        {
            var path = Path.Combine(root, "0001-use-redis.md");
            var index = Path.Combine(root, "README.md");
            var original = ValidMarkdown();
            const string originalIndex = "# Existing ADR Index";
            File.WriteAllText(path, original);
            File.WriteAllText(index, originalIndex);

            var provider = new RecordingReviewProvider(
                ReviewResult("Review completed."));
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = CliApplication.Run(
                ["review", path, "--provider", "openai", "--model", "test-model"],
                output,
                error,
                TestContext.Current.CancellationToken,
                reviewProvider: provider);

            Assert.Equal(ExitCodes.Success, exitCode);
            Assert.Equal(string.Empty, error.ToString());
            Assert.Equal(1, provider.CallCount);
            Assert.Equal(original, File.ReadAllText(path));
            Assert.Equal(originalIndex, File.ReadAllText(index));
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

            var exitCode = CliApplication.Run(
                ["review", path, "--provider", "openai", "--model", "test-model"],
                output,
                error,
                TestContext.Current.CancellationToken,
                reviewProvider: provider);

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

            var exitCode = CliApplication.Run(
                ["review", path, "--provider", "openai", "--model", "test-model"],
                output,
                error,
                TestContext.Current.CancellationToken,
                reviewProvider: provider);

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

            var exitCode = CliApplication.Run(
                ["review", target, "--provider", "openai", "--model", "test", "--context-file", context],
                output,
                error,
                TestContext.Current.CancellationToken,
                reviewProvider: provider);

            Assert.Equal(ExitCodes.Success, exitCode);
            Assert.Equal(1, provider.CallCount);
            Assert.Contains("Target ADR source [target]: 0001-use-redis.md", provider.LastRequest!.ProviderContext, StringComparison.Ordinal);
            Assert.Contains("Explicit context source [context-1]: requirements.txt", provider.LastRequest.ProviderContext, StringComparison.Ordinal);
            Assert.Contains("Latency must stay below 50 ms.", provider.LastRequest.ProviderContext, StringComparison.Ordinal);
            Assert.DoesNotContain("secret.txt", provider.LastRequest.ProviderContext, StringComparison.Ordinal);
            Assert.DoesNotContain(root, provider.LastRequest.ProviderContext, StringComparison.Ordinal);
            Assert.Contains("- target ADR [target]: 0001-use-redis.md", output.ToString(), StringComparison.Ordinal);
            Assert.Contains("- explicit context [context-1]: requirements.txt", output.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain(root, output.ToString(), StringComparison.Ordinal);
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

            var provider = new RecordingReviewProvider(
                ReviewResult("Reviewed."));
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = CliApplication.Run(
                ["review", target, "--provider", "openai", "--model", "test", "--include-existing-adrs"],
                output,
                error,
                TestContext.Current.CancellationToken,
                reviewProvider: provider);

            Assert.Equal(ExitCodes.Success, exitCode);
            Assert.Contains("Candidate ADR 0002", provider.LastRequest!.ProviderContext, StringComparison.Ordinal);
            Assert.Contains("Target ADR 0001", provider.LastRequest.ProviderContext, StringComparison.Ordinal);
            Assert.Contains("- existing ADR: 0002-use-postgres.md", output.ToString(), StringComparison.Ordinal);
            Assert.Contains("1 of 1 candidate ADR source(s) transmitted", output.ToString(), StringComparison.Ordinal);
            Assert.Contains("Warning:", output.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain(root, output.ToString(), StringComparison.Ordinal);
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

            var exitCode = CliApplication.Run(
                ["review", target, "--provider", "openai", "--model", "test", "--context-file", Path.Combine(root, "missing.txt")],
                output,
                error,
                TestContext.Current.CancellationToken,
                reviewProvider: provider);

            Assert.Equal(ExitCodes.OperationalError, exitCode);
            Assert.Equal(0, provider.CallCount);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void InvalidUtf8ContextFailsBeforeProviderInvocation()
    {
        var root = CreateTempDirectory();

        try
        {
            var target = Path.Combine(root, "0001-use-redis.md");
            var context = Path.Combine(root, "invalid.txt");
            File.WriteAllText(target, ValidMarkdown());
            File.WriteAllBytes(context, [0xC3, 0x28]);

            var provider = new RecordingReviewProvider(ReviewResult("Should not run."));
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = CliApplication.Run(
                ["review", target, "--provider", "openai", "--model", "test", "--context-file", context],
                output,
                error,
                TestContext.Current.CancellationToken,
                reviewProvider: provider);

            Assert.Equal(ExitCodes.OperationalError, exitCode);
            Assert.Equal(0, provider.CallCount);
            Assert.Contains("valid UTF-8", error.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("Review material sent", output.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void OversizedContextBytesFailBeforeProviderInvocation()
    {
        var root = CreateTempDirectory();

        try
        {
            var target = Path.Combine(root, "0001-use-redis.md");
            var context = Path.Combine(root, "oversized.txt");
            File.WriteAllText(target, ValidMarkdown());
            File.WriteAllBytes(
                context,
                Enumerable.Repeat(
                        (byte)'x',
                        checked((int)AdrReviewExplicitContextValidator.MaximumContextFileBytes + 1))
                    .ToArray());

            var provider = new RecordingReviewProvider(ReviewResult("Should not run."));
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = CliApplication.Run(
                ["review", target, "--provider", "openai", "--model", "test", "--context-file", context],
                output,
                error,
                TestContext.Current.CancellationToken,
                reviewProvider: provider);

            Assert.Equal(ExitCodes.OperationalError, exitCode);
            Assert.Equal(0, provider.CallCount);
            Assert.Contains("byte per-file limit", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void FinalPromptOverflowFailsBeforeProviderInvocation()
    {
        var root = CreateTempDirectory();

        try
        {
            var target = Path.Combine(root, "0001-use-redis.md");
            var firstContext = Path.Combine(root, "first.txt");
            var secondContext = Path.Combine(root, "second.txt");

            File.WriteAllText(
                target,
                ValidMarkdown().Replace(
                    "Use Redis.",
                    new string('d', 23000),
                    StringComparison.Ordinal));
            File.WriteAllText(firstContext, new string('a', 49000));
            File.WriteAllText(secondContext, new string('b', 49000));

            var provider = new RecordingReviewProvider(ReviewResult("Should not run."));
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = CliApplication.Run(
                [
                    "review",
                    target,
                    "--provider",
                    "openai",
                    "--model",
                    "test",
                    "--context-file",
                    firstContext,
                    "--context-file",
                    secondContext,
                ],
                output,
                error,
                TestContext.Current.CancellationToken,
                reviewProvider: provider);

            Assert.Equal(ExitCodes.OperationalError, exitCode);
            Assert.Equal(0, provider.CallCount);
            Assert.Contains("final prompt limit", error.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("Review material sent", output.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task ExistingAdrSectionsShareAdvertisedCharacterBudget()
    {
        var root = CreateTempDirectory();

        try
        {
            var target = Path.Combine(
                root,
                "0001-target.md");
            var first = Path.Combine(
                root,
                "0002-first.md");
            var second = Path.Combine(
                root,
                "0003-second.md");

            File.WriteAllText(
                target,
                ValidMarkdown().Replace(
                    "Use Redis",
                    "Target Decision",
                    StringComparison.Ordinal));
            File.WriteAllText(
                first,
                ValidMarkdown().Replace(
                    "Use Redis.",
                    new string('a', 5500),
                    StringComparison.Ordinal));
            File.WriteAllText(
                second,
                ValidMarkdown().Replace(
                    "Use Redis.",
                    new string('b', 5500),
                    StringComparison.Ordinal));

            var context = await AdrReviewContextBuilder.BuildAsync(
                target,
                File.ReadAllText(target),
                [],
                includeExistingAdrs: true,
                TestContext.Current.CancellationToken);

            var transmittedExistingAdrCharacters =
                (context.ExistingAdrs?.Content.Length ?? 0)
                + (context.CrossAdrEvidence?.Content.Length ?? 0);

            Assert.InRange(
                transmittedExistingAdrCharacters,
                1,
                ExistingAdrContextBuilder.MaximumContextCharacters);
            Assert.NotNull(
                context.CrossAdrEvidence);
        }
        finally
        {
            Directory.Delete(
                root,
                true);
        }
    }

    [Fact]
    public void BoundedExistingAdrSelectionDisclosesOnlyTransmittedSourcesInDeterministicOrder()
    {
        var root = CreateTempDirectory();

        try
        {
            var target = Path.Combine(root, "0001-target.md");
            File.WriteAllText(
                target,
                ValidMarkdown().Replace("Use Redis", "Target Decision", StringComparison.Ordinal));

            File.WriteAllText(
                Path.Combine(root, "0004-later.md"),
                ValidMarkdown().Replace("Use Redis", "Later Decision", StringComparison.Ordinal));
            File.WriteAllText(
                Path.Combine(root, "0003-too-large.md"),
                ValidMarkdown()
                    .Replace(
                        "Use Redis.",
                        new string('z', AdrCrossAdrEvidenceBuilder.MaximumCharacters),
                        StringComparison.Ordinal)
                    .Replace("# Use Redis", "# Too Large", StringComparison.Ordinal));
            File.WriteAllText(
                Path.Combine(root, "0002-included.md"),
                ValidMarkdown().Replace("Use Redis", "Included Decision", StringComparison.Ordinal));

            var provider = new RecordingReviewProvider(
                ReviewResult(
                    "Reviewed.",
                    "0001-target.md"));
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = CliApplication.Run(
                ["review", target, "--provider", "openai", "--model", "test", "--include-existing-adrs"],
                output,
                error,
                TestContext.Current.CancellationToken,
                reviewProvider: provider);

            Assert.Equal(ExitCodes.Success, exitCode);
            Assert.Equal(1, provider.CallCount);
            Assert.Contains("0002-included.md", provider.LastRequest!.ProviderContext, StringComparison.Ordinal);
            Assert.DoesNotContain("0003-too-large.md", provider.LastRequest.ProviderContext, StringComparison.Ordinal);
            Assert.DoesNotContain("0004-later.md", provider.LastRequest.ProviderContext, StringComparison.Ordinal);

            var disclosure = output.ToString();
            Assert.Contains("- existing ADR: 0002-included.md", disclosure, StringComparison.Ordinal);
            Assert.DoesNotContain("- existing ADR: 0003-too-large.md", disclosure, StringComparison.Ordinal);
            Assert.DoesNotContain("- existing ADR: 0004-later.md", disclosure, StringComparison.Ordinal);
            Assert.Contains("1 of 3 candidate ADR source(s) transmitted (bounded)", disclosure, StringComparison.Ordinal);
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

            var exitCode = CliApplication.Run(
                ["review", target, "--provider", "openai", "--model", "test"],
                output,
                error,
                TestContext.Current.CancellationToken,
                reviewProvider: provider);

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

            var exitCode = CliApplication.Run(
                ["review", target, "--provider", "openai", "--model", "test", "--include-existing-adrs"],
                output,
                error,
                TestContext.Current.CancellationToken,
                reviewProvider: provider);

            Assert.Equal(ExitCodes.Success, exitCode);
            Assert.Contains("Target ADR 0001", provider.LastRequest!.ProviderContext, StringComparison.Ordinal);
            Assert.Contains("Candidate ADR 0002", provider.LastRequest.ProviderContext, StringComparison.Ordinal);
            Assert.Contains("Status: Accepted", provider.LastRequest.ProviderContext, StringComparison.Ordinal);
            Assert.Contains("Status: Proposed", provider.LastRequest.ProviderContext, StringComparison.Ordinal);
            Assert.Contains("0002-avoid-redis.md", provider.LastRequest.ProviderContext, StringComparison.Ordinal);
            Assert.Contains("potential-risk", output.ToString(), StringComparison.Ordinal);
            Assert.Contains("[target] 0001-use-redis.md", output.ToString(), StringComparison.Ordinal);
            Assert.Contains("[existing-1] 0002-avoid-redis.md", output.ToString(), StringComparison.Ordinal);
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

            var exitCode = CliApplication.Run(
                ["review", target, "--provider", "openai", "--model", "test"],
                output,
                error,
                TestContext.Current.CancellationToken,
                reviewProvider: provider);

            Assert.Equal(ExitCodes.Success, exitCode);
            Assert.Equal(1, provider.CallCount);
            Assert.DoesNotContain("Cross-ADR comparison evidence", provider.LastRequest!.ProviderContext, StringComparison.Ordinal);
            Assert.DoesNotContain("Use Postgres.", provider.LastRequest.ProviderContext, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static AdrReviewResult ReviewResult(
        string explanation,
        string sourceName = "0001-use-redis.md")
    {
        var findings = AdrReviewContract.Dimensions
            .Select((dimension, index) =>
                new AdrReviewFinding(
                    dimension,
                    index == 5 ? "not-applicable" : index == 2 ? "missing-context" : "observed-evidence",
                    index == 5 ? null : sourceName,
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

    private sealed class FailingReviewProvider : IAdrReviewProvider
    {
        public Task<AdrReviewResult> ReviewAsync(
            AdrReviewRequest request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException(
                "Synthetic provider failure.");
    }

    private sealed class CancelingReviewProvider : IAdrReviewProvider
    {
        public Task<AdrReviewResult> ReviewAsync(
            AdrReviewRequest request,
            CancellationToken cancellationToken) =>
            throw new OperationCanceledException(cancellationToken);
    }
}
