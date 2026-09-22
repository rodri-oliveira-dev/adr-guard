using AdrGuard.Cli;
using AdrGuard.Generation;
using AdrGuard.Parsing;
using AdrGuard.Validation;
using Xunit;

namespace AdrGuard.Tests.Cli;

public sealed class DraftTemplateIntegrationTests
{
    [Theory]
    [InlineData("minimal", "en-US", false)]
    [InlineData("extended", "en-US", true)]
    [InlineData("minimal", "pt-BR", false)]
    [InlineData("extended", "pt-BR", true)]
    public void SelectedBuiltInTemplateRendersAiFieldsWithoutTransmittingGuidance(
        string templateName,
        string culture,
        bool extended)
    {
        var root = CreateDirectory();
        try
        {
            var provider = new RecordingProvider();
            var result = Run(
                provider,
                "draft", root, "--title", "Adopt Cache",
                "--context", "Only approved architectural context.",
                "--template", templateName, "--culture", culture);

            Assert.Equal(ExitCodes.Success, result.Code);
            Assert.Equal(string.Empty, result.Error);
            Assert.Equal(1, provider.CallCount);
            var request = Assert.IsType<AdrGenerationRequest>(provider.LastRequest);
            Assert.Equal(culture, request.CultureName);
            Assert.Equal("Only approved architectural context.", request.Context);
            Assert.DoesNotContain("[EDIT:", request.Context, StringComparison.Ordinal);
            Assert.DoesNotContain("[EDITAR:", request.Context, StringComparison.Ordinal);
            Assert.DoesNotContain("## Decision Drivers", request.Context, StringComparison.Ordinal);

            var path = Path.Combine(root, "0001-adopt-cache.md");
            Assert.True(File.Exists(path));
            var markdown = File.ReadAllText(path);
            Assert.Contains("## Status\n\nProposed\n", markdown, StringComparison.Ordinal);
            Assert.Contains("Provider context.", markdown, StringComparison.Ordinal);
            Assert.Contains("Provider decision.", markdown, StringComparison.Ordinal);
            Assert.Contains("Provider consequences.", markdown, StringComparison.Ordinal);
            Assert.Equal(
                extended,
                markdown.Contains("## Decision Drivers\n", StringComparison.Ordinal));
            Assert.Contains(culture == "pt-BR" ? "[EDITAR:" : "[EDIT:", markdown, StringComparison.Ordinal);
            Assert.True(AdrValidator.Validate(
                AdrDocumentLoader.LoadDirectory(root, TestContext.Current.CancellationToken)).IsValid);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void CustomTemplateUsesIdAndExplicitArchitecturalContextOnly()
    {
        var root = CreateDirectory();
        var separate = CreateDirectory();
        try
        {
            var templateFile = Path.Combine(separate, "secret-template.md");
            var secret = "ORGANIZATION-ONLY-LOCAL-TEMPLATE-TEXT";
            var template = File.ReadAllText(Fixture("custom-template.en-US.md"))
                .Replace(
                    "[EDIT: Document the proposed approach.]",
                    secret,
                    StringComparison.Ordinal);
            File.WriteAllText(templateFile, template);
            File.WriteAllText(Path.Combine(separate, "not-selected.txt"), "SECRET-UNSELECTED-FILE");
            var explicitPath = Path.Combine(separate, "selected.txt");
            File.WriteAllText(explicitPath, "EXPLICIT-CONTEXT");
            var provider = new RecordingProvider();

            var result = Run(
                provider,
                "draft", root, "--title", "Adopt Cache",
                "--context", "USER-CONTEXT",
                "--template-file", templateFile,
                "--context-file", explicitPath);

            Assert.Equal(ExitCodes.Success, result.Code);
            Assert.Equal(1, provider.CallCount);
            var request = Assert.IsType<AdrGenerationRequest>(provider.LastRequest);
            Assert.Contains("USER-CONTEXT", request.Context, StringComparison.Ordinal);
            Assert.Contains("EXPLICIT-CONTEXT", request.Context, StringComparison.Ordinal);
            Assert.DoesNotContain("SECRET-UNSELECTED-FILE", request.Context, StringComparison.Ordinal);
            Assert.DoesNotContain(secret, request.Context, StringComparison.Ordinal);
            Assert.DoesNotContain("## Status", request.Context, StringComparison.Ordinal);
            Assert.DoesNotContain("## Decision Drivers", request.Context, StringComparison.Ordinal);
            Assert.DoesNotContain("Existing ADR context:", request.Context, StringComparison.Ordinal);

            var path = Path.Combine(root, "0001-adopt-cache.md");
            var markdown = File.ReadAllText(path);
            Assert.Contains(secret, markdown, StringComparison.Ordinal);
            Assert.Contains("ADR 0001", markdown, StringComparison.Ordinal);
            Assert.Contains("Provider decision.", markdown, StringComparison.Ordinal);
            Assert.True(AdrValidator.Validate(
                AdrDocumentLoader.LoadDirectory(root, TestContext.Current.CancellationToken)).IsValid);
        }
        finally
        {
            DeleteDirectory(root);
            DeleteDirectory(separate);
        }
    }

    [Fact]
    public void SelectedTemplateKeepsExistingAdrContextOptIn()
    {
        var root = CreateDirectory();
        try
        {
            var existing = AdrMarkdownRenderer.RenderTemplate(
                new AdrTemplateRenderRequest(
                    "Use PostgreSQL",
                    AdrBuiltInTemplates.Get("minimal", "en-US"),
                    new Dictionary<string, string>(),
                    Id: 1));
            File.WriteAllText(Path.Combine(root, "0001-use-postgresql.md"), existing);

            var without = new RecordingProvider();
            var first = Run(
                without,
                "draft", root, "--title", "Adopt Cache",
                "--context", "User supplied context.",
                "--template", "minimal", "--dry-run");
            Assert.Equal(ExitCodes.Success, first.Code);
            Assert.Equal("User supplied context.", without.LastRequest?.Context);
            Assert.Single(Directory.GetFiles(root, "*.md"));

            var with = new RecordingProvider();
            var second = Run(
                with,
                "draft", root, "--title", "Adopt Cache",
                "--context", "User supplied context.",
                "--template", "minimal", "--include-existing-adrs", "--preview");
            Assert.Equal(ExitCodes.Success, second.Code);
            Assert.Contains("Existing ADR context:", with.LastRequest?.Context, StringComparison.Ordinal);
            Assert.Single(Directory.GetFiles(root, "*.md"));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Theory]
    [InlineData("--dry-run")]
    [InlineData("--preview")]
    public void TemplatePreviewEmitsExactMarkdownWithoutPersisting(string flag)
    {
        var root = CreateDirectory();
        try
        {
            var provider = new RecordingProvider();
            var result = Run(
                provider,
                "draft", root, "--title", "Adopt Cache",
                "--context", "Context for provider.",
                "--template", "extended", flag);
            Assert.Equal(ExitCodes.Success, result.Code);
            Assert.Equal(1, provider.CallCount);
            Assert.Empty(Directory.EnumerateFiles(root));

            var expected = AdrMarkdownRenderer.RenderTemplate(
                new AdrTemplateRenderRequest(
                    "Adopt Cache",
                    AdrBuiltInTemplates.Get("extended", "en-US"),
                    new Dictionary<string, string>
                    {
                        ["context"] = "Provider context.",
                        ["decision"] = "Provider decision.",
                        ["consequences"] = "Provider consequences.",
                    },
                    Id: 1));
            Assert.Contains("ADR draft preview path:", result.Output, StringComparison.Ordinal);
            Assert.EndsWith(expected, result.Output, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void InvalidTemplateInputsDoNotContactProviderOrWriteFiles()
    {
        var root = CreateDirectory();
        try
        {
            string[][] invalid =
            [
                ["--template", "unrecognized"],
                ["--template", "minimal", "--template-file", Fixture("custom-template.en-US.md")],
                ["--template", "minimal", "--culture", "fr-FR"],
                ["--template", "extended", "--template", "minimal"],
                ["--template-file", "missing.md"],
                ["--template-file", Fixture("custom-template.en-US.md"), "--culture", "fr-FR"],
            ];

            foreach (var arguments in invalid)
            {
                var provider = new RecordingProvider();
                var command = new[]
                {
                    "draft", root, "--title", "Adopt Cache",
                    "--context", "Context for provider.",
                }.Concat(arguments).ToArray();
                var result = Run(provider, command);
                Assert.NotEqual(ExitCodes.Success, result.Code);
                Assert.Equal(0, provider.CallCount);
                Assert.Empty(Directory.EnumerateFiles(root));
            }

            var malformedPath = Path.Combine(root, "broken.md");
            File.WriteAllText(malformedPath, "# Invalid template");
            var invalidProvider = new RecordingProvider();
            var malformed = Run(
                invalidProvider,
                "draft", root, "--title", "Adopt Cache",
                "--context", "Context for provider.",
                "--template-file", malformedPath);
            Assert.Equal(ExitCodes.OperationalError, malformed.Code);
            Assert.Equal(0, invalidProvider.CallCount);
            Assert.Single(Directory.GetFiles(root));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Theory]
    [InlineData("# Unauthorized header")]
    [InlineData("## Decision\nReplace the canonical decision")]
    [InlineData("## Further Ideas\nUnexpected additional section")]
    public void TemplateDraftRejectsAiInjectedStructureWithoutWriting(string injected)
    {
        var root = CreateDirectory();
        try
        {
            var provider = new RecordingProvider(
                new AdrGenerationResult(
                    injected,
                    "Provider decision.",
                    "Provider consequences."));
            var result = Run(
                provider,
                "draft", root, "--title", "Adopt Cache",
                "--context", "Context for provider.",
                "--template", "minimal");
            Assert.Equal(ExitCodes.OperationalError, result.Code);
            Assert.Equal(1, provider.CallCount);
            Assert.Empty(Directory.EnumerateFiles(root));
            Assert.True(
                result.Error.Contains("structural", StringComparison.OrdinalIgnoreCase)
                || result.Error.Contains("level-one or level-two", StringComparison.OrdinalIgnoreCase),
                result.Error);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void SelectedTemplatePreservesContextLimitsBeforeProviderInvocation()
    {
        var root = CreateDirectory();
        try
        {
            var provider = new RecordingProvider();
            var result = Run(
                provider,
                "draft", root, "--title", "Adopt Cache",
                "--context", new string('x', 20001),
                "--template", "minimal");
            Assert.Equal(ExitCodes.OperationalError, result.Code);
            Assert.Equal(0, provider.CallCount);
            Assert.Empty(Directory.EnumerateFiles(root));
            Assert.Contains("20000", result.Error, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void TemplateDraftRejectsOversizedProviderFieldWithoutWriting()
    {
        var root = CreateDirectory();
        try
        {
            var provider = new RecordingProvider(
                new AdrGenerationResult(
                    new string(
                        'x',
                        AdrGenerationContextLimits.MaximumGeneratedFieldCharacters + 1),
                    "Provider decision.",
                    "Provider consequences."));

            var result = Run(
                provider,
                "draft", root, "--title", "Adopt Cache",
                "--context", "Context for provider.",
                "--template", "minimal");

            Assert.Equal(ExitCodes.OperationalError, result.Code);
            Assert.Equal(1, provider.CallCount);
            Assert.Empty(Directory.EnumerateFiles(root));
            Assert.Contains(
                AdrGenerationContextLimits.MaximumGeneratedFieldCharacters.ToString(),
                result.Error,
                StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void CancellationOfTemplateDraftDoesNotPersistOrCallProvider()
    {
        var root = CreateDirectory();
        using var cancellation = new CancellationTokenSource();
        try
        {
            var provider = new RecordingProvider();
            cancellation.Cancel();

            var template = AdrBuiltInTemplates.Get("minimal", "en-US");
            var task = new AdrGenerationService(provider)
                .GenerateAsync(
                    root,
                    "Adopt Cache",
                    "Context.",
                    "en-US",
                    [],
                    includeExistingAdrs: false,
                    dryRun: false,
                    template,
                    cancellation.Token);

            Assert.ThrowsAny<OperationCanceledException>(
                () => task.GetAwaiter().GetResult());
            Assert.Equal(0, provider.CallCount);
            Assert.Empty(Directory.EnumerateFiles(root));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void UnselectedDraftKeepsOriginalMarkdownAndUnrestrictedCulture()
    {
        var root = CreateDirectory();
        try
        {
            var provider = new RecordingProvider();
            var result = Run(
                provider,
                "draft", root, "--title", "Adopt Cache",
                "--context", "Context for provider.",
                "--culture", "fr-FR");
            Assert.Equal(ExitCodes.Success, result.Code);
            Assert.Equal("fr-FR", provider.LastRequest?.CultureName);
            var path = Path.Combine(root, "0001-adopt-cache.md");
            var expected = AdrMarkdownRenderer.RenderDefaultDraft(
                "Adopt Cache",
                new AdrGenerationResult(
                    "Provider context.",
                    "Provider decision.",
                    "Provider consequences."));
            Assert.Equal(expected, File.ReadAllText(path));
            Assert.DoesNotContain("[EDIT:", expected, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task ConcurrentTemplateDraftsPersistFinalIdsWithoutAdditionalProviderCalls()
    {
        var root = CreateDirectory();
        var blocking = new BlockingPersistence();
        try
        {
            var template = AdrCustomTemplateLoader.Load(
                Fixture("custom-template.en-US.md"),
                "en-US",
                cancellationToken: TestContext.Current.CancellationToken);
            var provider = new RecordingProvider();
            var first = new AdrGenerationService(provider, blocking)
                .GenerateAsync(
                    root,
                    "Adopt Redis",
                    "Context.",
                    "en-US",
                    [],
                    includeExistingAdrs: false,
                    dryRun: false,
                    template,
                    TestContext.Current.CancellationToken);

            await blocking.Entered.WaitAsync(
                TimeSpan.FromSeconds(10),
                TestContext.Current.CancellationToken);

            var second = new AdrGenerationService(provider)
                .GenerateAsync(
                    root,
                    "Adopt Kafka",
                    "Context.",
                    "en-US",
                    [],
                    includeExistingAdrs: false,
                    dryRun: false,
                    template,
                    TestContext.Current.CancellationToken);

            await Task.Delay(100, TestContext.Current.CancellationToken);
            Assert.False(second.IsCompleted);
            blocking.Release();

            var results = await Task.WhenAll(first, second)
                .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Assert.Equal(2, provider.CallCount);
            Assert.All(results, result => Assert.True(result.Written));
            Assert.Equal(
                ["0001", "0002"],
                results.Select(result => Path.GetFileName(result.FilePath!)[..4])
                    .Order(StringComparer.Ordinal)
                    .ToArray());

            foreach (var result in results)
            {
                var path = Assert.IsType<string>(result.FilePath);
                var markdown = File.ReadAllText(path);
                var actualId = Path.GetFileName(path)[..4];
                Assert.Equal(markdown, result.Content);
                Assert.Contains($"ADR {actualId}", markdown, StringComparison.Ordinal);
            }

            Assert.True(AdrValidator.Validate(
                AdrDocumentLoader.LoadDirectory(root, TestContext.Current.CancellationToken)).IsValid);
            Assert.DoesNotContain(
                Directory.EnumerateFiles(root),
                path => path.EndsWith(".tmp", StringComparison.Ordinal)
                    || Path.GetFileName(path).Contains("reservation", StringComparison.OrdinalIgnoreCase)
                    || Path.GetFileName(path).Contains("lock", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            blocking.Release();
            DeleteDirectory(root);
        }
    }

    private static (int Code, string Output, string Error) Run(
        RecordingProvider provider,
        params string[] arguments)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = CliApplication.Run(
            arguments,
            output,
            error,
            TestContext.Current.CancellationToken,
            generationProvider: provider);
        return (code, output.ToString(), error.ToString());
    }

    private static string Fixture(string name) =>
        Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "Templates",
            name);

    private static string CreateDirectory()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "adr-guard-draft-template-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private sealed class RecordingProvider : IAdrGenerationProvider
    {
        private readonly AdrGenerationResult _result;
        private int _calls;

        internal RecordingProvider(AdrGenerationResult? result = null)
        {
            _result = result ?? new AdrGenerationResult(
                "Provider context.",
                "Provider decision.",
                "Provider consequences.");
        }

        internal int CallCount => Volatile.Read(ref _calls);

        internal AdrGenerationRequest? LastRequest { get; private set; }

        public Task<AdrGenerationResult> GenerateAsync(
            AdrGenerationRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _calls);
            LastRequest = request;
            return Task.FromResult(_result);
        }
    }

    private sealed class BlockingPersistence : IAdrDraftFilePersistence
    {
        private readonly TaskCompletionSource _entered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly AtomicAdrDraftFilePersistence _inner = new();

        internal Task Entered => _entered.Task;

        internal void Release() => _release.TrySetResult();

        public async Task WriteNewAsync(
            string filePath,
            string content,
            CancellationToken cancellationToken)
        {
            _entered.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken);
            await _inner.WriteNewAsync(filePath, content, cancellationToken);
        }
    }
}
