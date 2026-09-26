using AdrGuard.Cli;
using AdrGuard.Generation.Http;
using AdrGuard.Review;
using AdrGuard.Review.Providers;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace AdrGuard.Tests.Review;

public sealed class AdrReviewProviderIntegrationTests
{
    [Theory]
    [InlineData("openai")]
    [InlineData("anthropic")]
    [InlineData("gemini")]
    [InlineData("openai-compatible")]
    public async Task ReviewProvidersUseReviewContractAndParseEightDimensions(
        string providerName)
    {
        string? capturedBody = null;
        var reviewJson = CreateReviewJson(sparse: false);

        using var client = new HttpClient(
            new StubHttpMessageHandler(
                async (request, cancellationToken) =>
                {
                    capturedBody = await request.Content!
                        .ReadAsStringAsync(cancellationToken);

                    return JsonResponse(
                        CreateProviderResponse(
                            providerName,
                            reviewJson));
                }));

        var provider = AdrReviewProviderFactory.Create(
            providerName,
            "test-model",
            providerName == "openai-compatible"
                ? "https://compatible.example.test/v1/"
                : null,
            client,
            EnvironmentReader);

        var result = await provider.ReviewAsync(
            new AdrReviewRequest(
                "/tmp/0001-use-cache.md",
                "# Use cache",
                "Target ADR source: 0001-use-cache.md"),
            TestContext.Current.CancellationToken);

        Assert.Equal(8, result.Findings.Count);
        Assert.All(
            AdrReviewContract.Dimensions,
            dimension => Assert.Contains(
                result.Findings,
                finding => string.Equals(
                    finding.Dimension,
                    dimension,
                    StringComparison.Ordinal)));

        Assert.NotNull(capturedBody);
        Assert.Contains(
            "ADR technical review contract v1.0",
            capturedBody,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Draft the prose fields",
            capturedBody,
            StringComparison.Ordinal);

        if (string.Equals(
                providerName,
                "anthropic",
                StringComparison.Ordinal))
        {
            Assert.Contains(
                $"\"max_tokens\":{AdrReviewProviderFactory.AnthropicReviewMaxTokens}",
                capturedBody,
                StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("unknown-dimension")]
    [InlineData("invalid-classification")]
    [InlineData("missing-dimension")]
    [InlineData("missing-context-without-uncertainty")]
    public void ReviewJsonContractRejectsInvalidStructuredFindings(
        string invalidCase)
    {
        var json = CreateInvalidReviewJson(
            invalidCase);

        var exception = Assert.Throws<AiProviderException>(
            () => AdrReviewJsonContract.Parse(
                json,
                "Test provider"));

        Assert.Equal(
            AiProviderErrorKind.InvalidResponse,
            exception.ErrorKind);
    }

    [Fact]
    public void OpenAiCompatibleJsonFenceRemainsAcceptedBoundary()
    {
        var json = CreateReviewJson(
            sparse: false);
        var fenced =
            $"```json{Environment.NewLine}{json}{Environment.NewLine}```";

        var normalized =
            AdrReviewJsonContract.RemoveSingleJsonFence(
                fenced);
        var result = AdrReviewJsonContract.Parse(
            normalized,
            "Compatible provider");

        Assert.Equal(
            8,
            result.Findings.Count);
    }

    [Fact]
    public void ReviewJsonContractRejectsExcessiveFindingCount()
    {
        var finding = new
        {
            dimension = "clarity-and-rationale",
            classification = "observed-evidence",
            source = "0001-use-cache.md",
            excerpt = "Use a cache.",
            explanation = "Evidence observed.",
            guidance = "Verify the evidence.",
        };

        var json = JsonSerializer.Serialize(
            new
            {
                findings = Enumerable
                    .Repeat(
                        finding,
                        AdrReviewJsonContract.MaximumFindings + 1)
                    .ToArray(),
            });

        var exception = Assert.Throws<AiProviderException>(
            () => AdrReviewJsonContract.Parse(
                json,
                "Test provider"));

        Assert.Equal(
            AiProviderErrorKind.InvalidResponse,
            exception.ErrorKind);
        Assert.Contains(
            "finding review limit",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SparseAdrProducesExplicitUnknownsThroughProductionReviewAdapter()
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

                ## Decision
                Use a cache.

                ## Consequences
                Operational details are not yet defined.
                """);

            var reviewJson = CreateReviewJson(sparse: true);
            using var client = new HttpClient(
                new StubHttpMessageHandler(
                    (_, _) => Task.FromResult(
                        JsonResponse(
                            CreateProviderResponse(
                                "openai",
                                reviewJson)))));

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
                ],
                output,
                error,
                TestContext.Current.CancellationToken,
                httpClientFactory: () => client,
                environmentVariableReader: EnvironmentReader);

            Assert.Equal(ExitCodes.Success, exitCode);
            Assert.Equal(string.Empty, error.ToString());
            Assert.Contains(
                "not enough information",
                output.ToString(),
                StringComparison.OrdinalIgnoreCase);
            Assert.Contains(
                "What measurable target should be used?",
                output.ToString(),
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "50 ms",
                output.ToString(),
                StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(
                "approved",
                output.ToString(),
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void InvalidProviderReviewJsonIsOperationalFailureNotFinding()
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

            using var client = new HttpClient(
                new StubHttpMessageHandler(
                    (_, _) => Task.FromResult(
                        JsonResponse(
                            CreateProviderResponse(
                                "openai",
                                """{"findings":[]}""")))));

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
                ],
                output,
                error,
                TestContext.Current.CancellationToken,
                httpClientFactory: () => client,
                environmentVariableReader: EnvironmentReader);

            Assert.Equal(
                ExitCodes.OperationalError,
                exitCode);
            Assert.Contains(
                "provider failed",
                error.ToString(),
                StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(
                "[missing-context]",
                output.ToString(),
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "## nonfunctional-requirements",
                output.ToString(),
                StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static string CreateInvalidReviewJson(
        string invalidCase)
    {
        var findings = AdrReviewContract.Dimensions
            .Select(dimension =>
                new Dictionary<string, string>(
                    StringComparer.Ordinal)
                {
                    ["dimension"] = dimension,
                    ["classification"] = "observed-evidence",
                    ["source"] = "0001-use-cache.md",
                    ["excerpt"] = "Use a cache.",
                    ["explanation"] = "Evidence observed.",
                    ["guidance"] = "Verify the evidence.",
                })
            .ToList();

        switch (invalidCase)
        {
            case "unknown-dimension":
                findings[0]["dimension"] =
                    "unknown-dimension";
                break;

            case "invalid-classification":
                findings[0]["classification"] =
                    "critical";
                break;

            case "missing-dimension":
                findings.RemoveAt(
                    findings.Count - 1);
                break;

            case "missing-context-without-uncertainty":
                findings[0]["classification"] =
                    "missing-context";
                findings[0]["explanation"] =
                    "The ADR does not say.";
                findings[0]["source"] =
                    string.Empty;
                findings[0]["excerpt"] =
                    string.Empty;
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(invalidCase));
        }

        return JsonSerializer.Serialize(
            new { findings });
    }

    private static string CreateReviewJson(bool sparse)
    {
        var findings = AdrReviewContract.Dimensions
            .Select(dimension =>
            {
                var missing = sparse
                    && dimension is
                        "considered-alternatives"
                        or "nonfunctional-requirements"
                        or "security-and-compliance"
                        or "implementation-and-operational-feasibility"
                        or "measurable-verification-criteria";

                return new
                {
                    dimension,
                    classification =
                        missing
                            ? "missing-context"
                            : "observed-evidence",
                    source =
                        missing
                            ? string.Empty
                            : "0001-use-cache.md",
                    excerpt =
                        missing
                            ? string.Empty
                            : "Use a cache.",
                    explanation =
                        missing
                            ? "not enough information to determine this dimension from the selected evidence."
                            : "The selected ADR contains evidence for this dimension.",
                    guidance =
                        dimension == "measurable-verification-criteria"
                            ? "What measurable target should be used?"
                            : "Human reviewer should verify this dimension.",
                };
            })
            .ToArray();

        return JsonSerializer.Serialize(
            new { findings });
    }

    private static string CreateProviderResponse(
        string providerName,
        string reviewJson)
    {
        return providerName switch
        {
            "openai" => JsonSerializer.Serialize(
                new
                {
                    status = "completed",
                    output = new[]
                    {
                        new
                        {
                            type = "message",
                            content = new[]
                            {
                                new
                                {
                                    type = "output_text",
                                    text = reviewJson,
                                },
                            },
                        },
                    },
                }),
            "anthropic" => JsonSerializer.Serialize(
                new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = reviewJson,
                        },
                    },
                    stop_reason = "end_turn",
                }),
            "gemini" => JsonSerializer.Serialize(
                new
                {
                    status = "completed",
                    steps = new[]
                    {
                        new
                        {
                            type = "model_output",
                            content = new[]
                            {
                                new
                                {
                                    type = "text",
                                    text = reviewJson,
                                },
                            },
                        },
                    },
                }),
            "openai-compatible" => JsonSerializer.Serialize(
                new
                {
                    choices = new[]
                    {
                        new
                        {
                            message = new
                            {
                                content = reviewJson,
                            },
                        },
                    },
                }),
            _ => throw new ArgumentOutOfRangeException(
                nameof(providerName)),
        };
    }

    private static string? EnvironmentReader(
        string name) =>
        name switch
        {
            "OPENAI_API_KEY" => "openai-test-key",
            "ANTHROPIC_API_KEY" => "anthropic-test-key",
            "GEMINI_API_KEY" => "gemini-test-key",
            "ADR_GUARD_OPENAI_COMPATIBLE_API_KEY" => null,
            _ => null,
        };

    private static HttpResponseMessage JsonResponse(
        string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                json,
                Encoding.UTF8,
                "application/json"),
        };

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"adr-guard-review-provider-{Guid.NewGuid():N}");
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

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>
            handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            handler(request, cancellationToken);
    }
}
