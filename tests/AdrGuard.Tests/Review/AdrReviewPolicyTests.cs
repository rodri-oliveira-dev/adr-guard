using AdrGuard.Cli;
using AdrGuard.Review;
using AdrGuard.Review.Policy;
using System.Text.Json;
using Xunit;

namespace AdrGuard.Tests.Review;

public sealed class AdrReviewPolicyTests
{
    [Fact]
    public void AdvisoryPolicyViolationIsReportedButDoesNotFail()
    {
        var root = CreateTempDirectory();

        try
        {
            var target = WriteTarget(root);
            var policy = WritePolicy(
                root,
                """
                {
                  "schemaVersion": "1.0",
                  "rules": [
                    {
                      "name": "ops-notes",
                      "type": "required-section-content",
                      "section": "Operational Notes"
                    }
                  ]
                }
                """);
            var provider = new StaticReviewProvider(
                CompleteObservedResult());

            var result = Run(
                provider,
                "review",
                target,
                "--provider",
                "openai",
                "--model",
                "test-model",
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
                "[ops-notes]",
                result.Output,
                StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void EnforceFailsOnNamedDeterministicRuleBeforeProvider()
    {
        var root = CreateTempDirectory();

        try
        {
            var target = WriteTarget(root);
            var policy = WritePolicy(
                root,
                """
                {
                  "schemaVersion": "1.0",
                  "rules": [
                    {
                      "name": "security-context",
                      "type": "required-context-file",
                      "path": "security.md"
                    }
                  ]
                }
                """);
            var firstProvider = new StaticReviewProvider(
                CompleteObservedResult());
            var secondProvider = new StaticReviewProvider(
                CompleteRiskResult());

            var first = Run(
                firstProvider,
                "review",
                target,
                "--provider",
                "openai",
                "--model",
                "model-a",
                "--policy",
                "enforce",
                "--policy-file",
                policy);
            var second = Run(
                secondProvider,
                "review",
                target,
                "--provider",
                "anthropic",
                "--model",
                "model-b",
                "--policy",
                "enforce",
                "--policy-file",
                policy);

            Assert.Equal(
                ExitCodes.PolicyFailed,
                first.Code);
            Assert.Equal(
                first.Code,
                second.Code);
            Assert.Equal(
                0,
                firstProvider.CallCount);
            Assert.Equal(
                0,
                secondProvider.CallCount);
            Assert.Contains(
                "[security-context]",
                first.Error,
                StringComparison.Ordinal);
            Assert.Contains(
                "required-context-file",
                first.Error,
                StringComparison.Ordinal);
            Assert.Contains(
                "security.md",
                first.Error,
                StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void RequiredContextFilePassesWhenExistingFileIsExplicitlySelected()
    {
        var root = CreateTempDirectory();

        try
        {
            var target = WriteTarget(root);
            var securityContext = Path.Combine(
                root,
                "security.md");
            File.WriteAllText(
                securityContext,
                """
                # Security Context

                Security requirements are explicitly selected for this review.
                """);
            var policy = WritePolicy(
                root,
                """
                {
                  "schemaVersion": "1.0",
                  "rules": [
                    {
                      "name": "security-context",
                      "type": "required-context-file",
                      "path": "security.md"
                    }
                  ]
                }
                """);
            var provider = new StaticReviewProvider(
                CompleteObservedResult());

            var result = Run(
                provider,
                "review",
                target,
                "--provider",
                "openai",
                "--model",
                "test-model",
                "--context-file",
                securityContext,
                "--policy",
                "enforce",
                "--policy-file",
                policy);

            Assert.Equal(
                ExitCodes.Success,
                result.Code);
            Assert.Equal(
                1,
                provider.CallCount);
            Assert.DoesNotContain(
                "[security-context]",
                result.Error,
                StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void EnforceFailurePrecedesRealProviderConstructionWithoutCredentials()
    {
        var root = CreateTempDirectory();

        try
        {
            var target = WriteTarget(root);
            var policy = WritePolicy(
                root,
                """
                {
                  "schemaVersion": "1.0",
                  "rules": [
                    {
                      "name": "security-notes",
                      "type": "required-section-content",
                      "section": "Security"
                    }
                  ]
                }
                """);
            var httpClientFactoryCalls = 0;
            using var output = new StringWriter();
            using var error = new StringWriter();

            var code = CliApplication.Run(
                [
                    "review",
                    target,
                    "--provider",
                    "openai",
                    "--model",
                    "test-model",
                    "--policy",
                    "enforce",
                    "--policy-file",
                    policy,
                ],
                output,
                error,
                TestContext.Current.CancellationToken,
                httpClientFactory: () =>
                {
                    httpClientFactoryCalls++;
                    return new HttpClient();
                },
                environmentVariableReader: _ => null);

            Assert.Equal(
                ExitCodes.PolicyFailed,
                code);
            Assert.Equal(
                0,
                httpClientFactoryCalls);
            Assert.Contains(
                "[security-notes]",
                error.ToString(),
                StringComparison.Ordinal);
            Assert.Contains(
                "provider was not invoked",
                error.ToString(),
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void RequiredSectionContentAcceptsAnyNonEmptyMatchingSection()
    {
        var root = CreateTempDirectory();

        try
        {
            var target = Path.Combine(
                root,
                "0001-use-cache.md");
            File.WriteAllText(
                target,
                """
                # Use Cache

                ## Status
                Proposed

                ## Context
                We need caching.

                ## Operational Notes

                ## Operational Notes
                Runbook and ownership are documented.

                ## Decision
                Use a cache.

                ## Consequences
                Cache operation must be defined.
                """);
            var policy = WritePolicy(
                root,
                """
                {
                  "schemaVersion": "1.0",
                  "rules": [
                    {
                      "name": "operational-notes",
                      "type": "required-section-content",
                      "section": "Operational Notes"
                    }
                  ]
                }
                """);
            var provider = new StaticReviewProvider(
                CompleteObservedResult());

            var result = Run(
                provider,
                "review",
                target,
                "--provider",
                "openai",
                "--model",
                "test-model",
                "--policy",
                "enforce",
                "--policy-file",
                policy);

            Assert.Equal(
                ExitCodes.Success,
                result.Code);
            Assert.Equal(
                1,
                provider.CallCount);
            Assert.DoesNotContain(
                "[operational-notes]",
                result.Error,
                StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void PassingEnforcementIsIndependentOfModelWording()
    {
        var root = CreateTempDirectory();

        try
        {
            var target = WriteTarget(root);
            var policy = WritePolicy(
                root,
                """
                {
                  "schemaVersion": "1.0",
                  "rules": [
                    {
                      "name": "context-present",
                      "type": "required-section-content",
                      "section": "Context"
                    }
                  ]
                }
                """);
            var observedProvider = new StaticReviewProvider(
                CompleteObservedResult());
            var riskProvider = new StaticReviewProvider(
                CompleteRiskResult());

            var observed = Run(
                observedProvider,
                "review",
                target,
                "--provider",
                "openai",
                "--model",
                "model-a",
                "--policy",
                "enforce",
                "--policy-file",
                policy);
            var risk = Run(
                riskProvider,
                "review",
                target,
                "--provider",
                "openai",
                "--model",
                "model-b",
                "--policy",
                "enforce",
                "--policy-file",
                policy);

            Assert.Equal(
                ExitCodes.Success,
                observed.Code);
            Assert.Equal(
                observed.Code,
                risk.Code);
            Assert.Equal(
                1,
                observedProvider.CallCount);
            Assert.Equal(
                1,
                riskProvider.CallCount);
            Assert.Contains(
                "critical high-risk wording",
                risk.Output,
                StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void EnforceRequiresExplicitPolicyFile()
    {
        var root = CreateTempDirectory();

        try
        {
            var target = WriteTarget(root);
            var provider = new StaticReviewProvider(
                CompleteObservedResult());

            var result = Run(
                provider,
                "review",
                target,
                "--provider",
                "openai",
                "--model",
                "test-model",
                "--policy",
                "enforce");

            Assert.Equal(
                ExitCodes.UsageError,
                result.Code);
            Assert.Equal(
                0,
                provider.CallCount);
            Assert.Contains(
                "--policy-file",
                result.Error,
                StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void PolicyConfigurationIsStrictlyValidated()
    {
        var root = CreateTempDirectory();

        try
        {
            var target = WriteTarget(root);
            var policy = WritePolicy(
                root,
                """
                {
                  "schemaVersion": "1.0",
                  "rules": [
                    {
                      "name": "unknown",
                      "type": "model-severity",
                      "section": "Decision"
                    }
                  ]
                }
                """);
            var provider = new StaticReviewProvider(
                CompleteObservedResult());

            var result = Run(
                provider,
                "review",
                target,
                "--provider",
                "openai",
                "--model",
                "test-model",
                "--policy-file",
                policy);

            Assert.Equal(
                ExitCodes.UsageError,
                result.Code);
            Assert.Equal(
                0,
                provider.CallCount);
            Assert.Contains(
                "unsupported deterministic type",
                result.Error,
                StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void PolicyLoaderRejectsAbsoluteRequiredContextPath()
    {
        var root = CreateTempDirectory();

        try
        {
            var target = WriteTarget(root);
            var absolutePath = Path.Combine(
                root,
                "security.md");
            var policy = WritePolicy(
                root,
                JsonSerializer.Serialize(
                    new
                    {
                        schemaVersion = "1.0",
                        rules = new[]
                        {
                            new
                            {
                                name = "absolute-context",
                                type = "required-context-file",
                                path = absolutePath,
                            },
                        },
                    }));
            var provider = new StaticReviewProvider(
                CompleteObservedResult());

            var result = Run(
                provider,
                "review",
                target,
                "--provider",
                "openai",
                "--model",
                "test-model",
                "--policy-file",
                policy);

            Assert.Equal(
                ExitCodes.UsageError,
                result.Code);
            Assert.Equal(
                0,
                provider.CallCount);
            Assert.Contains(
                "relative 'path'",
                result.Error,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void PolicyLoaderRejectsUnsupportedContextExtension()
    {
        var result = RunWithPolicy(
            """
            {
              "schemaVersion": "1.0",
              "rules": [
                {
                  "name": "security-context",
                  "type": "required-context-file",
                  "path": "security.json"
                }
              ]
            }
            """);

        Assert.Equal(
            ExitCodes.UsageError,
            result.Code);
        Assert.Contains(
            ".md or .txt",
            result.Error,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PolicyLoaderRejectsDuplicateRuleNames()
    {
        var result = RunWithPolicy(
            """
            {
              "schemaVersion": "1.0",
              "rules": [
                {
                  "name": "duplicate",
                  "type": "required-section-content",
                  "section": "Context"
                },
                {
                  "name": "duplicate",
                  "type": "required-section-content",
                  "section": "Decision"
                }
              ]
            }
            """);

        Assert.Equal(
            ExitCodes.UsageError,
            result.Code);
        Assert.Contains(
            "invalid or duplicated",
            result.Error,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PolicyLoaderRejectsUnknownJsonProperty()
    {
        var result = RunWithPolicy(
            """
            {
              "schemaVersion": "1.0",
              "unsupported": true,
              "rules": [
                {
                  "name": "context",
                  "type": "required-section-content",
                  "section": "Context"
                }
              ]
            }
            """);

        Assert.Equal(
            ExitCodes.UsageError,
            result.Code);
        Assert.Contains(
            "unsupported properties",
            result.Error,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PolicyLoaderRejectsUnsupportedSchemaVersion()
    {
        var result = RunWithPolicy(
            """
            {
              "schemaVersion": "2.0",
              "rules": [
                {
                  "name": "context",
                  "type": "required-section-content",
                  "section": "Context"
                }
              ]
            }
            """);

        Assert.Equal(
            ExitCodes.UsageError,
            result.Code);
        Assert.Contains(
            "Unsupported review policy schema version",
            result.Error,
            StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidPolicyModeIsUsageErrorBeforeProvider()
    {
        var root = CreateTempDirectory();

        try
        {
            var target = WriteTarget(root);
            var provider = new StaticReviewProvider(
                CompleteObservedResult());

            var result = Run(
                provider,
                "review",
                target,
                "--provider",
                "openai",
                "--model",
                "test-model",
                "--policy",
                "strict");

            Assert.Equal(
                ExitCodes.UsageError,
                result.Code);
            Assert.Equal(
                0,
                provider.CallCount);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void AdvisoryPolicyWithJsonFormatKeepsStdoutAsValidJson()
    {
        var root = CreateTempDirectory();

        try
        {
            var target = WriteTarget(root);
            var policy = WritePolicy(
                root,
                """
                {
                  "schemaVersion": "1.0",
                  "rules": [
                    {
                      "name": "ops-notes",
                      "type": "required-section-content",
                      "section": "Operational Notes"
                    }
                  ]
                }
                """);
            var provider = new StaticReviewProvider(
                CompleteObservedResult());

            var result = Run(
                provider,
                "review",
                target,
                "--provider",
                "openai",
                "--model",
                "test-model",
                "--policy-file",
                policy,
                "--format",
                "json");

            Assert.Equal(
                ExitCodes.Success,
                result.Code);
            Assert.Equal(
                1,
                provider.CallCount);

            using var report =
                JsonDocument.Parse(
                    result.Output);

            Assert.Equal(
                "1.0",
                report.RootElement
                    .GetProperty("schemaVersion")
                    .GetString());
            Assert.Contains(
                "Review policy: advisory",
                result.Error,
                StringComparison.Ordinal);
            Assert.Contains(
                "[ops-notes]",
                result.Error,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "Review policy:",
                result.Output,
                StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static (int Code, string Output, string Error) RunWithPolicy(
        string policyContent)
    {
        var root = CreateTempDirectory();

        try
        {
            var target = WriteTarget(root);
            var policy = WritePolicy(
                root,
                policyContent);
            var provider = new StaticReviewProvider(
                CompleteObservedResult());

            var result = Run(
                provider,
                "review",
                target,
                "--provider",
                "openai",
                "--model",
                "test-model",
                "--policy-file",
                policy);

            Assert.Equal(
                0,
                provider.CallCount);

            return result;
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static (int Code, string Output, string Error) Run(
        IAdrReviewProvider provider,
        params string[] arguments)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var code = CliApplication.Run(
            arguments,
            output,
            error,
            TestContext.Current.CancellationToken,
            reviewProvider: provider);

        return (
            code,
            output.ToString(),
            error.ToString());
    }

    private static string WriteTarget(
        string root)
    {
        var target = Path.Combine(
            root,
            "0001-use-cache.md");

        File.WriteAllText(
            target,
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
            """);

        return target;
    }

    private static string WritePolicy(
        string root,
        string content)
    {
        var path = Path.Combine(
            root,
            "review-policy.json");
        File.WriteAllText(path, content);
        return path;
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"adr-guard-policy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
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

    private static AdrReviewResult CompleteRiskResult() =>
        new(
            AdrReviewContract.Dimensions
                .Select((dimension, index) =>
                    new AdrReviewFinding(
                        dimension,
                        index == 0
                            ? "potential-risk"
                            : "observed-evidence",
                        "0001-use-cache.md",
                        "Use a cache.",
                        index == 0
                            ? "critical high-risk wording from the model."
                            : "Evidence was observed.",
                        "Human reviewer should verify the evidence."))
                .ToArray());

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
