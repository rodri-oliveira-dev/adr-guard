using AdrGuard.Cli;
using AdrGuard.Generation.Providers.Gemini;
using System.Net;
using System.Text.Json;
using Xunit;

namespace AdrGuard.Tests.Cli;

public sealed class DraftProviderSelectionIntegrationTests
{
    [Fact]
    public void DraftRequiresProviderAndModelAtRuntime()
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

            Assert.Equal(
                ExitCodes.UsageError,
                exitCode);
            Assert.Contains(
                "--provider",
                error.ToString(),
                StringComparison.Ordinal);
            Assert.Contains(
                "--model",
                error.ToString(),
                StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void DraftRejectsUnsupportedProvider()
    {
        var root = CreateTempDirectory();

        try
        {
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = CliApplication.Run(
                BaseArguments(
                    root,
                    "unknown",
                    "model"),
                output,
                error);

            Assert.Equal(
                ExitCodes.UsageError,
                exitCode);
            Assert.Contains(
                "Unsupported AI provider",
                error.ToString(),
                StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void DraftRejectsEndpointForOfficialProvider()
    {
        var root = CreateTempDirectory();

        try
        {
            using var output = new StringWriter();
            using var error = new StringWriter();

            var args = BaseArguments(
                    root,
                    "openai",
                    "model")
                .Concat(
                    [
                        "--endpoint",
                        "https://example.test/v1",
                    ])
                .ToArray();

            var exitCode = CliApplication.Run(
                args,
                output,
                error);

            Assert.Equal(
                ExitCodes.UsageError,
                exitCode);
            Assert.Contains(
                "--endpoint is only valid",
                error.ToString(),
                StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void DraftRequiresEndpointForOpenAiCompatible()
    {
        var root = CreateTempDirectory();

        try
        {
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = CliApplication.Run(
                BaseArguments(
                    root,
                    "openai-compatible",
                    "model"),
                output,
                error);

            Assert.Equal(
                ExitCodes.UsageError,
                exitCode);
            Assert.Contains(
                "--endpoint is required",
                error.ToString(),
                StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void DraftMissingCredentialFailsBeforeHttpRequest()
    {
        var root = CreateTempDirectory();
        var callCount = 0;

        try
        {
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = CliApplication.Run(
                BaseArguments(
                    root,
                    "gemini",
                    "gemini-test"),
                output,
                error,
                generationProvider: null,
                httpClientFactory: () =>
                    new HttpClient(
                        new StubHttpMessageHandler(
                            (_, _) =>
                            {
                                callCount++;
                                return Task.FromResult(
                                    new HttpResponseMessage(
                                        HttpStatusCode.OK));
                            })),
                environmentVariableReader: _ => null);

            Assert.Equal(
                ExitCodes.OperationalError,
                exitCode);
            Assert.Equal(0, callCount);
            Assert.Contains(
                GeminiProviderOptions.ApiKeyEnvironmentVariableName,
                error.ToString(),
                StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void DraftUsesGeminiProviderEndToEnd()
    {
        var root = CreateTempDirectory();
        Uri? capturedUri = null;
        string? capturedModel = null;
        string? capturedApiKey = null;

        try
        {
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = CliApplication.Run(
                BaseArguments(
                    root,
                    "GEMINI",
                    "gemini-test"),
                output,
                error,
                generationProvider: null,
                httpClientFactory: () =>
                    new HttpClient(
                        new StubHttpMessageHandler(
                            async (
                                request,
                                cancellationToken) =>
                            {
                                capturedUri =
                                    request.RequestUri;
                                capturedApiKey =
                                    request.Headers
                                        .GetValues(
                                            "x-goog-api-key")
                                        .Single();

                                var requestJson =
                                    await request.Content!
                                        .ReadAsStringAsync(
                                            cancellationToken);
                                using var document =
                                    JsonDocument.Parse(
                                        requestJson);
                                capturedModel =
                                    document.RootElement
                                        .GetProperty("model")
                                        .GetString();

                                return GeminiSuccessResponse();
                            })),
                environmentVariableReader:
                    variableName =>
                        variableName
                            == GeminiProviderOptions
                                .ApiKeyEnvironmentVariableName
                            ? "gemini-secret"
                            : null);

            Assert.Equal(
                ExitCodes.Success,
                exitCode);
            Assert.Equal(
                string.Empty,
                error.ToString());
            Assert.Equal(
                GeminiProviderOptions.Endpoint,
                capturedUri);
            Assert.Equal(
                "gemini-test",
                capturedModel);
            Assert.Equal(
                "gemini-secret",
                capturedApiKey);
            Assert.True(
                File.Exists(
                    Path.Combine(
                        root,
                        "0001-use-redis.md")));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static string[] BaseArguments(
        string root,
        string provider,
        string model) =>
        [
            "draft",
            root,
            "--title",
            "Use Redis",
            "--context",
            "We need distributed caching.",
            "--provider",
            provider,
            "--model",
            model,
        ];

    private static HttpResponseMessage GeminiSuccessResponse()
    {
        var generated =
            JsonSerializer.Serialize(
                new
                {
                    context =
                        "The service needs distributed caching.",
                    decision =
                        "Use Redis for distributed caching.",
                    consequences =
                        "The team must operate Redis.",
                });
        var escaped =
            JsonSerializer.Serialize(generated);

        return new HttpResponseMessage(
            HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""
                {
                  "status":"completed",
                  "steps":[
                    {
                      "type":"model_output",
                      "content":[
                        {
                          "type":"text",
                          "text":{{escaped}}
                        }
                      ]
                    }
                  ]
                }
                """),
        };
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"adr-guard-provider-{Guid.NewGuid():N}");
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

    private sealed class StubHttpMessageHandler(
        Func<
            HttpRequestMessage,
            CancellationToken,
            Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            handler(
                request,
                cancellationToken);
    }
}
