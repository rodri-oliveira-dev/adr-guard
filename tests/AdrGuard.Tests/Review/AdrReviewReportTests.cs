using AdrGuard.Cli;
using AdrGuard.Generation;
using AdrGuard.Parsing;
using AdrGuard.Review;
using AdrGuard.Review.Reporting;
using System.Text.Json;
using Xunit;

namespace AdrGuard.Tests.Review;

public sealed class AdrReviewReportTests
{
    [Fact]
    public void JsonRoundTripPreservesStableVersionedContract()
    {
        var report = BuildReport(
            new AdrReviewResult(
                [
                    new AdrReviewFinding(
                        "clarity-and-rationale",
                        "observed-evidence",
                        "0001-use-cache.md",
                        "Use a cache.",
                        "The decision is explicitly stated.",
                        "Human reviewer should confirm the rationale is sufficient."),
                    new AdrReviewFinding(
                        "nonfunctional-requirements",
                        "missing-context",
                        null,
                        null,
                        "not enough information about latency or capacity requirements.",
                        "Document measurable nonfunctional requirements."),
                ]));

        var json = AdrReviewReportSerializer.Serialize(
            report);
        var deserialized =
            AdrReviewReportSerializer.Deserialize(
                json);
        var roundTrip =
            AdrReviewReportSerializer.Serialize(
                deserialized);

        Assert.Equal(
            json,
            roundTrip);

        using var document = JsonDocument.Parse(
            json);
        var root = document.RootElement;

        Assert.Equal(
            "1.0",
            root.GetProperty("schemaVersion")
                .GetString());
        Assert.Equal(
            "openai",
            root.GetProperty("provider")
                .GetProperty("name")
                .GetString());
        Assert.Equal(
            8,
            root.GetProperty("dimensions")
                .GetArrayLength());
        Assert.Equal(
            "needs-context",
            root.GetProperty("outcome")
                .GetString());
    }

    [Fact]
    public void ReportSupportsSuccessfulReviewWithNoFollowUpFindings()
    {
        var result = new AdrReviewResult(
            AdrReviewContract.Dimensions
                .Select(dimension =>
                    new AdrReviewFinding(
                        dimension,
                        "observed-evidence",
                        "0001-use-cache.md",
                        "Use a cache.",
                        "Evidence was observed.",
                        "Human reviewer should verify the evidence."))
                .ToArray());

        var report = BuildReport(result);
        var text = AdrReviewTextRenderer.Render(
            report);

        Assert.Equal(
            "no-follow-up-findings",
            report.Outcome);
        Assert.Empty(
            report.Findings);
        Assert.Contains(
            "No follow-up findings were produced.",
            text,
            StringComparison.Ordinal);
        Assert.Contains(
            "This is not an architectural approval.",
            text,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ReportOrdersMultipleFollowUpFindingsByDimension()
    {
        var report = BuildReport(
            new AdrReviewResult(
                [
                    new AdrReviewFinding(
                        "risks-and-consequences",
                        "potential-risk",
                        "0001-use-cache.md",
                        "Cache operation must be defined.",
                        "Operational ownership may be unclear.",
                        "Assign an owner and failure procedure."),
                    new AdrReviewFinding(
                        "considered-alternatives",
                        "recommendation-for-human-investigation",
                        "0001-use-cache.md",
                        "Use a cache.",
                        "The ADR does not show whether alternatives were compared.",
                        "Confirm what alternatives were evaluated."),
                ]));

        Assert.Equal(
            2,
            report.Findings.Length);
        Assert.Equal(
            "considered-alternatives",
            report.Findings[0].Dimension);
        Assert.Equal(
            "risks-and-consequences",
            report.Findings[1].Dimension);
        Assert.All(
            report.Findings,
            finding => Assert.Equal(
                "recommended",
                finding.FollowUpPriority));
        Assert.Equal(
            "follow-up-suggested",
            report.Outcome);
    }

    [Fact]
    public void MissingSourceRemainsExplicitWithoutFabricatedEvidence()
    {
        var report = BuildReport(
            new AdrReviewResult(
                [
                    new AdrReviewFinding(
                        "measurable-verification-criteria",
                        "missing-context",
                        null,
                        null,
                        "not enough information to identify measurable acceptance criteria.",
                        "Define a measurable verification criterion."),
                ]));

        var finding = Assert.Single(
            report.Findings);

        Assert.Empty(
            finding.Evidence);
        Assert.Equal(
            "not-enough-information",
            finding.Uncertainty);
        Assert.Contains(
            report.Limitations,
            limitation => limitation.Contains(
                "no verified source evidence",
                StringComparison.Ordinal));
    }

    [Fact]
    public void UnselectedOrAbsoluteFindingSourceIsRejected()
    {
        var unknown = new AdrReviewResult(
            [
                new AdrReviewFinding(
                    "clarity-and-rationale",
                    "potential-risk",
                    "invented.md",
                    "Invented.",
                    "Unknown source.",
                    "Verify the source."),
            ]);

        var absolute = new AdrReviewResult(
            [
                new AdrReviewFinding(
                    "clarity-and-rationale",
                    "potential-risk",
                    "/tmp/0001-use-cache.md",
                    "Use a cache.",
                    "Absolute path should not be accepted.",
                    "Use selected source attribution."),
            ]);

        Assert.Throws<InvalidOperationException>(
            () => BuildReport(unknown));
        Assert.Throws<InvalidOperationException>(
            () => BuildReport(absolute));
    }

    [Fact]
    public void JsonCliOutputIsPureJsonAndDisclosureUsesDiagnostics()
    {
        var root = CreateTempDirectory();

        try
        {
            var target = Path.Combine(
                root,
                "0001-use-cache.md");
            File.WriteAllText(
                target,
                ValidMarkdown());

            var provider = new StaticReviewProvider(
                CompleteObservedResult());
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = CliApplication.Run(
                [
                    "review",
                    target,
                    "--provider",
                    "openai",
                    "--model",
                    "test-model",
                    "--format",
                    "json",
                ],
                output,
                error,
                TestContext.Current.CancellationToken,
                reviewProvider: provider);

            Assert.Equal(
                ExitCodes.Success,
                exitCode);

            using var document = JsonDocument.Parse(
                output.ToString());

            Assert.Equal(
                "1.0",
                document.RootElement
                    .GetProperty("schemaVersion")
                    .GetString());
            Assert.Contains(
                "Review material sent",
                error.ToString(),
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "Review material sent",
                output.ToString(),
                StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(
                root,
                true);
        }
    }

    [Fact]
    public void OutputWriteIsExplicitAtomicAndRequiresOverwrite()
    {
        var root = CreateTempDirectory();

        try
        {
            var target = Path.Combine(
                root,
                "0001-use-cache.md");
            var index = Path.Combine(
                root,
                "README.md");
            var reportPath = Path.Combine(
                root,
                "review.json");
            var originalAdr = ValidMarkdown();
            const string originalIndex =
                "# Existing index";

            File.WriteAllText(
                target,
                originalAdr);
            File.WriteAllText(
                index,
                originalIndex);

            var provider = new StaticReviewProvider(
                CompleteObservedResult());

            var first = RunJsonReview(
                target,
                reportPath,
                provider,
                overwrite: false);

            Assert.Equal(
                ExitCodes.Success,
                first.Code);
            Assert.True(
                File.Exists(reportPath));
            Assert.Equal(
                first.Output.TrimEnd(),
                File.ReadAllText(reportPath));
            Assert.Equal(
                1,
                provider.CallCount);

            var second = RunJsonReview(
                target,
                reportPath,
                provider,
                overwrite: false);

            Assert.Equal(
                ExitCodes.OperationalError,
                second.Code);
            Assert.Contains(
                "--overwrite",
                second.Error,
                StringComparison.Ordinal);
            Assert.Equal(
                1,
                provider.CallCount);

            var third = RunJsonReview(
                target,
                reportPath,
                provider,
                overwrite: true);

            Assert.Equal(
                ExitCodes.Success,
                third.Code);
            Assert.Equal(
                2,
                provider.CallCount);
            Assert.Equal(
                originalAdr,
                File.ReadAllText(target));
            Assert.Equal(
                originalIndex,
                File.ReadAllText(index));
        }
        finally
        {
            Directory.Delete(
                root,
                true);
        }
    }

    [Fact]
    public void OutputCannotReplaceTargetThroughSymbolicLinkAlias()
    {
        var root = CreateTempDirectory();

        try
        {
            var realTarget = Path.Combine(
                root,
                "0002-real.md");
            var aliasTarget = Path.Combine(
                root,
                "0001-alias.md");
            var original = ValidMarkdown();

            File.WriteAllText(
                realTarget,
                original);
            File.CreateSymbolicLink(
                aliasTarget,
                realTarget);

            var exception = Assert.Throws<InvalidOperationException>(
                () => AdrReviewReportFileWriter.ValidateDestination(
                    realTarget,
                    AdrReviewOutputFormat.Text,
                    aliasTarget,
                    overwrite: true));

            Assert.Contains(
                "cannot overwrite",
                exception.Message,
                StringComparison.Ordinal);
            Assert.Equal(
                original,
                File.ReadAllText(realTarget));
        }
        finally
        {
            Directory.Delete(
                root,
                true);
        }
    }

    [Fact]
    public void EvidenceFilenameMatchingUsesCompleteTokens()
    {
        var markdown = ValidMarkdown();
        var targetPath = Path.Combine(
            Path.GetTempPath(),
            "0001-use-cache.md");
        var target = AdrMarkdownParser.Parse(
            targetPath,
            markdown);
        var context = new AdrReviewContext(
            "0001-use-cache.md",
            markdown,
            [
                new ExplicitContextFile(
                    Path.Combine(
                        Path.GetTempPath(),
                        "notes-0001-use-cache.md"),
                    "Supporting notes."),
            ],
            null,
            null);
        var result = new AdrReviewResult(
            [
                new AdrReviewFinding(
                    "clarity-and-rationale",
                    "observed-evidence",
                    "notes-0001-use-cache.md",
                    "Supporting notes.",
                    "The supporting context contains the evidence.",
                    "Human reviewer should verify the supporting context."),
            ]);

        var report = AdrReviewReportBuilder.Build(
            target,
            context,
            result,
            "openai",
            "test-model",
            new DateTimeOffset(
                2026,
                9,
                27,
                12,
                0,
                0,
                TimeSpan.Zero));

        var assessment = Assert.Single(
            report.Dimensions
                .Single(dimension =>
                    string.Equals(
                        dimension.Name,
                        "clarity-and-rationale",
                        StringComparison.Ordinal))
                .Assessments);
        var evidence = Assert.Single(
            assessment.Evidence);

        Assert.Equal(
            "context-1",
            evidence.SourceId);
        Assert.Equal(
            "notes-0001-use-cache.md",
            evidence.Path);
    }

    [Theory]
    [InlineData("0001-use-cache.md")]
    [InlineData("README.md")]
    public void OutputCannotReplaceAdrOrIndex(
        string outputFileName)
    {
        var root = CreateTempDirectory();

        try
        {
            var target = Path.Combine(
                root,
                "0001-use-cache.md");
            File.WriteAllText(
                target,
                ValidMarkdown());
            File.WriteAllText(
                Path.Combine(root, "README.md"),
                "# index");

            var provider = new StaticReviewProvider(
                CompleteObservedResult());
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = CliApplication.Run(
                [
                    "review",
                    target,
                    "--provider",
                    "openai",
                    "--model",
                    "test-model",
                    "--output",
                    Path.Combine(root, outputFileName),
                    "--overwrite",
                ],
                output,
                error,
                TestContext.Current.CancellationToken,
                reviewProvider: provider);

            Assert.Equal(
                ExitCodes.OperationalError,
                exitCode);
            Assert.Equal(
                0,
                provider.CallCount);
            Assert.Contains(
                "cannot overwrite",
                error.ToString(),
                StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(
                root,
                true);
        }
    }

    private static AdrReviewReport BuildReport(
        AdrReviewResult result)
    {
        var markdown = ValidMarkdown();
        var path = Path.Combine(
            Path.GetTempPath(),
            "0001-use-cache.md");
        var target = AdrMarkdownParser.Parse(
            path,
            markdown);
        var context = new AdrReviewContext(
            "0001-use-cache.md",
            markdown,
            [],
            null,
            null);

        return AdrReviewReportBuilder.Build(
            target,
            context,
            result,
            "openai",
            "test-model",
            new DateTimeOffset(
                2026,
                9,
                26,
                12,
                0,
                0,
                TimeSpan.Zero));
    }

    private static AdrReviewResult CompleteObservedResult() =>
        new(
            AdrReviewContract.Dimensions
                .Select(dimension =>
                    new AdrReviewFinding(
                        dimension,
                        "observed-evidence",
                        "0001-use-cache.md",
                        "Use a cache.",
                        "Evidence was observed.",
                        "Human reviewer should verify the evidence."))
                .ToArray());

    private static (
        int Code,
        string Output,
        string Error)
        RunJsonReview(
            string target,
            string reportPath,
            StaticReviewProvider provider,
            bool overwrite)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var args = new List<string>
        {
            "review",
            target,
            "--provider",
            "openai",
            "--model",
            "test-model",
            "--format",
            "json",
            "--output",
            reportPath,
        };

        if (overwrite)
        {
            args.Add(
                "--overwrite");
        }

        var exitCode = CliApplication.Run(
            args,
            output,
            error,
            TestContext.Current.CancellationToken,
            reviewProvider: provider);

        return (
            exitCode,
            output.ToString(),
            error.ToString());
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"adr-guard-review-report-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static string ValidMarkdown() =>
        """
        # Use Cache

        ## Status
        Proposed

        ## Context
        We need caching.

        ## Decision
        Use a cache.

        ## Consequences
        Cache operation must be defined.
        """;

    private sealed class StaticReviewProvider(
        AdrReviewResult result)
        : IAdrReviewProvider
    {
        internal int CallCount { get; private set; }

        public Task<AdrReviewResult> ReviewAsync(
            AdrReviewRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(result);
        }
    }
}
