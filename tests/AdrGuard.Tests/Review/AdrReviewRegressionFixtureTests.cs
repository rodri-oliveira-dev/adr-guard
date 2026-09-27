using AdrGuard.Cli;
using AdrGuard.Generation.Http;
using AdrGuard.Review;
using AdrGuard.Review.Providers;
using AdrGuard.Review.Reporting;
using System.Text.Json;
using Xunit;

namespace AdrGuard.Tests.Review;

public sealed class AdrReviewRegressionFixtureTests
{
    [Fact]
    public void CompleteFixtureProducesVersionedEightDimensionReportWithoutMutation()
    {
        var root = CreateWorkspace(
            "0001-use-cache.md");
        var index = Path.Combine(
            root,
            "README.md");
        File.WriteAllText(
            index,
            "# Existing index");

        try
        {
            var before = Snapshot(root);
            var provider = FixtureReviewProvider.Response(
                "complete.json");

            var result = Run(
                provider,
                "review",
                Path.Combine(
                    root,
                    "0001-use-cache.md"),
                "--provider",
                "openai",
                "--model",
                "deterministic-v1",
                "--format",
                "json");

            Assert.Equal(
                ExitCodes.Success,
                result.Code);
            Assert.Equal(
                1,
                provider.CallCount);

            var report =
                AdrReviewReportSerializer.Deserialize(
                    result.Output);

            Assert.Equal(
                AdrReviewReportBuilder.SchemaVersion,
                report.SchemaVersion);
            Assert.Equal(
                AdrReviewContract.Dimensions,
                report.Dimensions
                    .Select(dimension => dimension.Name)
                    .ToArray());
            Assert.Equal(
                AdrReviewContract.Dimensions.Length,
                report.Dimensions.Length);
            Assert.Equal(
                "no-follow-up-findings",
                report.Outcome);
            Assert.Empty(
                report.Findings);
            Assert.All(
                report.Dimensions,
                dimension =>
                    Assert.Single(
                        dimension.Assessments));

            AssertSnapshot(
                before,
                root);
        }
        finally
        {
            Directory.Delete(
                root,
                true);
        }
    }

    [Theory]
    [InlineData(
        "sparse.json",
        "needs-context",
        "not-enough-information")]
    [InlineData(
        "non-applicable.json",
        "no-follow-up-findings",
        null)]
    public void FixtureOutcomesRepresentContractStateNotArchitecturalTruth(
        string responseFixture,
        string expectedOutcome,
        string? expectedUncertainty)
    {
        var root = CreateWorkspace(
            "0001-use-cache.md");

        try
        {
            var provider =
                FixtureReviewProvider.Response(
                    responseFixture);

            var result = Run(
                provider,
                "review",
                Path.Combine(
                    root,
                    "0001-use-cache.md"),
                "--provider",
                "openai",
                "--model",
                "deterministic-v1",
                "--format",
                "json");

            Assert.Equal(
                ExitCodes.Success,
                result.Code);

            var report =
                AdrReviewReportSerializer.Deserialize(
                    result.Output);

            Assert.Equal(
                expectedOutcome,
                report.Outcome);

            if (expectedUncertainty is null)
            {
                Assert.Empty(
                    report.Findings);
                Assert.All(
                    report.Dimensions
                        .SelectMany(
                            dimension =>
                                dimension.Assessments),
                    assessment =>
                        Assert.Equal(
                            "not-applicable",
                            assessment.Classification));
            }
            else
            {
                Assert.Contains(
                    report.Dimensions
                        .SelectMany(
                            dimension =>
                                dimension.Assessments),
                    assessment =>
                        string.Equals(
                            assessment.Uncertainty,
                            expectedUncertainty,
                            StringComparison.Ordinal));
                Assert.Contains(
                    report.Findings,
                    finding =>
                        string.Equals(
                            finding.Classification,
                            "missing-context",
                            StringComparison.Ordinal));
            }
        }
        finally
        {
            Directory.Delete(
                root,
                true);
        }
    }

    [Fact]
    public void CrossAdrFixtureHasDeterministicOrderingSupersededContextAndSourceAttribution()
    {
        var root = CreateWorkspace(
            "0001-use-cache.md",
            "0002-use-postgres-cache.md",
            "0003-legacy-cache.md",
            "0004-use-redis-cache.md");
        var selectedContext = CopyFixture(
            root,
            "context",
            "requirements.txt");
        _ = CopyFixture(
            root,
            "context",
            "unselected-secret.txt");

        try
        {
            var provider =
                FixtureReviewProvider.Response(
                    "cross-adr.json");

            var result = Run(
                provider,
                "review",
                Path.Combine(
                    root,
                    "0001-use-cache.md"),
                "--provider",
                "openai",
                "--model",
                "deterministic-v1",
                "--context-file",
                selectedContext,
                "--include-existing-adrs",
                "--format",
                "json");

            Assert.Equal(
                ExitCodes.Success,
                result.Code);

            Assert.NotNull(
                provider.LastRequest);
            var providerContext =
                provider.LastRequest!.ProviderContext;

            using var envelope =
                JsonDocument.Parse(
                    providerContext);
            var sources = envelope.RootElement
                .GetProperty("sources")
                .EnumerateArray()
                .ToArray();
            var existingContent = sources
                .Single(source =>
                    source.GetProperty("sourceId")
                        .GetString()
                    == "existing-adrs")
                .GetProperty("content")
                .GetString()
                ?? string.Empty;

            var postgresPosition =
                existingContent.IndexOf(
                    "ADR 0002",
                    StringComparison.Ordinal);
            var legacyPosition =
                existingContent.IndexOf(
                    "ADR 0003",
                    StringComparison.Ordinal);
            var redisPosition =
                existingContent.IndexOf(
                    "ADR 0004",
                    StringComparison.Ordinal);

            Assert.True(
                postgresPosition >= 0);
            Assert.True(
                legacyPosition
                > postgresPosition);
            Assert.True(
                redisPosition
                > legacyPosition);
            Assert.Contains(
                "Status: Superseded",
                existingContent,
                StringComparison.Ordinal);
            Assert.Contains(
                sources,
                source =>
                    source.GetProperty("name")
                        .GetString()
                    == "requirements.txt");
            Assert.DoesNotContain(
                sources,
                source =>
                    source.GetProperty("name")
                        .GetString()
                    == "unselected-secret.txt");

            var report =
                AdrReviewReportSerializer.Deserialize(
                    result.Output);
            var consistency =
                Assert.Single(
                    report.Dimensions.Single(
                        dimension =>
                            dimension.Name
                            == "architectural-consistency")
                        .Assessments);

            Assert.Equal(
                "potential-risk",
                consistency.Classification);
            Assert.Equal(
                ["target", "existing-1"],
                consistency.Evidence
                    .Select(
                        evidence =>
                            evidence.SourceId)
                    .ToArray());
            Assert.Equal(
                [
                    "0001-use-cache.md",
                    "0002-use-postgres-cache.md",
                ],
                consistency.Evidence
                    .Select(
                        evidence =>
                            evidence.Path)
                    .ToArray());

            Assert.Contains(
                report.InputScope.ExistingAdrs,
                source =>
                    source.Path
                    == "0003-legacy-cache.md");
            Assert.Contains(
                report.InputScope.ExistingAdrs,
                source =>
                    source.Path
                    == "0004-use-redis-cache.md");
        }
        finally
        {
            Directory.Delete(
                root,
                true);
        }
    }

    [Theory]
    [InlineData("empty-findings.json")]
    [InlineData("malformed.json")]
    [InlineData("malicious-extra-property.json")]
    public void InvalidProviderFixturesCannotBecomeSuccessfulCleanReview(
        string responseFixture)
    {
        var root = CreateWorkspace(
            "0001-use-cache.md");

        try
        {
            var provider =
                FixtureReviewProvider.Response(
                    responseFixture);

            var result = Run(
                provider,
                "review",
                Path.Combine(
                    root,
                    "0001-use-cache.md"),
                "--provider",
                "openai",
                "--model",
                "deterministic-v1");

            Assert.Equal(
                ExitCodes.OperationalError,
                result.Code);
            Assert.Equal(
                1,
                provider.CallCount);
            Assert.Contains(
                "provider failed",
                result.Error,
                StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(
                "# ADR Technical Review",
                result.Output,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "no-follow-up-findings",
                result.Output,
                StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(
                root,
                true);
        }
    }

    [Theory]
    [InlineData(
        FixtureProviderFailure.Network)]
    [InlineData(
        FixtureProviderFailure.Timeout)]
    public void TransportAndTimeoutFailuresAreOperationalNotClean(
        FixtureProviderFailure failure)
    {
        var root = CreateWorkspace(
            "0001-use-cache.md");

        try
        {
            var provider =
                FixtureReviewProvider.Failure(
                    failure);

            var result = Run(
                provider,
                "review",
                Path.Combine(
                    root,
                    "0001-use-cache.md"),
                "--provider",
                "openai",
                "--model",
                "deterministic-v1");

            Assert.Equal(
                ExitCodes.OperationalError,
                result.Code);
            Assert.Equal(
                1,
                provider.CallCount);
            Assert.Contains(
                "provider failed",
                result.Error,
                StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(
                "no-follow-up-findings",
                result.Output,
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
    public void CancellationIsOperationalAndReviewRemainsReadOnly()
    {
        var root = CreateWorkspace(
            "0001-use-cache.md",
            "0002-use-postgres-cache.md");
        File.WriteAllText(
            Path.Combine(
                root,
                "README.md"),
            "# Existing index");

        try
        {
            var before = Snapshot(root);
            var provider =
                FixtureReviewProvider.Failure(
                    FixtureProviderFailure.Canceled);

            var result = Run(
                provider,
                "review",
                Path.Combine(
                    root,
                    "0001-use-cache.md"),
                "--provider",
                "openai",
                "--model",
                "deterministic-v1",
                "--include-existing-adrs");

            Assert.Equal(
                ExitCodes.OperationalError,
                result.Code);
            Assert.Contains(
                "canceled",
                result.Error,
                StringComparison.OrdinalIgnoreCase);
            AssertSnapshot(
                before,
                root);
        }
        finally
        {
            Directory.Delete(
                root,
                true);
        }
    }

    [Fact]
    public void DeterministicEnforcementIsIndependentOfMockProviderLanguage()
    {
        var root = CreateWorkspace(
            "0001-use-cache.md");
        var policy = CopyFixture(
            root,
            "policies",
            "require-security-section.json");

        try
        {
            var neutralProvider =
                FixtureReviewProvider.Response(
                    "complete.json");
            var uncertainProvider =
                FixtureReviewProvider.Response(
                    "sparse.json");

            var neutral = Run(
                neutralProvider,
                "review",
                Path.Combine(
                    root,
                    "0001-use-cache.md"),
                "--provider",
                "openai",
                "--model",
                "neutral-language",
                "--policy",
                "enforce",
                "--policy-file",
                policy);

            var uncertain = Run(
                uncertainProvider,
                "review",
                Path.Combine(
                    root,
                    "0001-use-cache.md"),
                "--provider",
                "openai",
                "--model",
                "uncertain-language",
                "--policy",
                "enforce",
                "--policy-file",
                policy);

            Assert.Equal(
                ExitCodes.PolicyFailed,
                neutral.Code);
            Assert.Equal(
                neutral.Code,
                uncertain.Code);
            Assert.Equal(
                0,
                neutralProvider.CallCount);
            Assert.Equal(
                0,
                uncertainProvider.CallCount);
            Assert.Contains(
                "security-section-required",
                neutral.Error,
                StringComparison.Ordinal);
            Assert.Contains(
                "security-section-required",
                uncertain.Error,
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
    public void AdvisoryPolicyViolationRunsMockProviderAndRemainsSuccessful()
    {
        var root = CreateWorkspace(
            "0001-use-cache.md");
        var policy = CopyFixture(
            root,
            "policies",
            "require-security-section.json");

        try
        {
            var provider =
                FixtureReviewProvider.Response(
                    "complete.json");

            var result = Run(
                provider,
                "review",
                Path.Combine(
                    root,
                    "0001-use-cache.md"),
                "--provider",
                "openai",
                "--model",
                "deterministic-v1",
                "--policy-file",
                policy);

            Assert.Equal(
                ExitCodes.Success,
                result.Code);
            Assert.Equal(
                1,
                provider.CallCount);
            Assert.Contains(
                "Review policy: advisory",
                result.Output,
                StringComparison.Ordinal);
            Assert.Contains(
                "security-section-required",
                result.Output,
                StringComparison.Ordinal);
            Assert.Contains(
                "# ADR Technical Review",
                result.Output,
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
    public void StructuralFailureIsExitOneAndDoesNotInvokeProvider()
    {
        var root = CreateWorkspace(
            "0009-invalid.md");

        try
        {
            var provider =
                FixtureReviewProvider.Response(
                    "complete.json");

            var result = Run(
                provider,
                "review",
                Path.Combine(
                    root,
                    "0009-invalid.md"),
                "--provider",
                "openai",
                "--model",
                "deterministic-v1");

            Assert.Equal(
                ExitCodes.ValidationFailed,
                result.Code);
            Assert.Equal(
                0,
                provider.CallCount);
            Assert.Contains(
                "structurally invalid",
                result.Error,
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
    public void CliParsingFailuresAreUsageErrorsBeforeMockProvider()
    {
        var root = CreateWorkspace(
            "0001-use-cache.md");

        try
        {
            var target = Path.Combine(
                root,
                "0001-use-cache.md");

            IReadOnlyList<string[]> invalidArguments =
            [
                [
                    "review",
                    target,
                    "--provider",
                    "openai",
                    "--model",
                    "deterministic-v1",
                    "--format",
                    "json",
                    "--format",
                    "text",
                ],
                [
                    "review",
                    target,
                    "--provider",
                    "openai",
                    "--model",
                    "deterministic-v1",
                    "--policy",
                    "advisory",
                    "--policy",
                    "enforce",
                ],
                [
                    "review",
                    target,
                    "--provider",
                    "openai",
                    "--model",
                    "deterministic-v1",
                    "--policy",
                    "enforce",
                ],
                [
                    "review",
                    target,
                    "--provider",
                    "openai",
                    "--model",
                    "deterministic-v1",
                    "--unknown-review-option",
                ],
            ];

            foreach (var arguments
                     in invalidArguments)
            {
                var provider =
                    FixtureReviewProvider.Response(
                        "complete.json");
                var result = Run(
                    provider,
                    arguments);

                Assert.Equal(
                    ExitCodes.UsageError,
                    result.Code);
                Assert.Equal(
                    0,
                    provider.CallCount);
            }
        }
        finally
        {
            Directory.Delete(
                root,
                true);
        }
    }

    private static (
        int Code,
        string Output,
        string Error)
        Run(
            IAdrReviewProvider provider,
            params string[] arguments)
    {
        using var output =
            new StringWriter();
        using var error =
            new StringWriter();

        var code = CliApplication.Run(
            arguments,
            output,
            error,
            TestContext.Current.CancellationToken,
            environmentVariableReader:
                _ => null,
            reviewProvider:
                provider);

        return (
            code,
            output.ToString(),
            error.ToString());
    }

    private static string CreateWorkspace(
        params string[] adrFiles)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"adr-guard-review-regression-{Guid.NewGuid():N}");
        Directory.CreateDirectory(
            root);

        foreach (var adrFile in adrFiles)
        {
            _ = CopyFixture(
                root,
                "adrs",
                adrFile);
        }

        return root;
    }

    private static string CopyFixture(
        string destinationDirectory,
        string category,
        string fileName)
    {
        var source = Fixture(
            category,
            fileName);
        var destination = Path.Combine(
            destinationDirectory,
            fileName);

        File.Copy(
            source,
            destination);

        return destination;
    }

    private static string Fixture(
        string category,
        string fileName) =>
        Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "Review",
            category,
            fileName);

    private static Dictionary<
        string,
        byte[]> Snapshot(
        string root) =>
        Directory
            .EnumerateFiles(
                root,
                "*",
                SearchOption.AllDirectories)
            .OrderBy(
                file => file,
                StringComparer.Ordinal)
            .ToDictionary(
                file => Path.GetRelativePath(
                    root,
                    file),
                File.ReadAllBytes,
                StringComparer.Ordinal);

    private static void AssertSnapshot(
        IReadOnlyDictionary<
            string,
            byte[]> expected,
        string root)
    {
        var actual = Snapshot(root);

        Assert.Equal(
            expected.Keys,
            actual.Keys);

        foreach (var key in expected.Keys)
        {
            Assert.Equal(
                expected[key],
                actual[key]);
        }
    }

    public enum FixtureProviderFailure
    {
        Network,
        Timeout,
        Canceled,
    }

    private sealed class FixtureReviewProvider
        : IAdrReviewProvider
    {
        private readonly string? _responseFixture;
        private readonly FixtureProviderFailure? _failure;

        private FixtureReviewProvider(
            string? responseFixture,
            FixtureProviderFailure? failure)
        {
            _responseFixture =
                responseFixture;
            _failure =
                failure;
        }

        internal int CallCount { get; private set; }

        internal AdrReviewRequest? LastRequest { get; private set; }

        internal static FixtureReviewProvider Response(
            string responseFixture) =>
            new(
                responseFixture,
                null);

        internal static FixtureReviewProvider Failure(
            FixtureProviderFailure failure) =>
            new(
                null,
                failure);

        public Task<AdrReviewResult> ReviewAsync(
            AdrReviewRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(
                request);

            cancellationToken
                .ThrowIfCancellationRequested();

            CallCount++;
            LastRequest = request;

            if (_failure is
                FixtureProviderFailure.Network)
            {
                throw new AiProviderException(
                    AiProviderErrorKind.Network,
                    "Fixture provider network failure.");
            }

            if (_failure is
                FixtureProviderFailure.Timeout)
            {
                throw new AiProviderException(
                    AiProviderErrorKind.Timeout,
                    "Fixture provider timed out.");
            }

            if (_failure is
                FixtureProviderFailure.Canceled)
            {
                throw new OperationCanceledException(
                    "Fixture provider canceled.");
            }

            var json = File.ReadAllText(
                Fixture(
                    "responses",
                    _responseFixture
                    ?? throw new InvalidOperationException(
                        "Fixture response was not configured.")));

            return Task.FromResult(
                AdrReviewJsonContract.Parse(
                    json,
                    "Fixture provider"));
        }
    }
}
