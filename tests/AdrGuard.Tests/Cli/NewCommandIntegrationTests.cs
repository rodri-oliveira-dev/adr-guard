using AdrGuard.Cli;
using AdrGuard.Generation;
using AdrGuard.Parsing;
using AdrGuard.Validation;
using Xunit;

namespace AdrGuard.Tests.Cli;

public sealed class NewCommandIntegrationTests
{
    [Fact]
    public void HelpIncludesCommandOptionsWithoutChangingDraftHelp()
    {
        var (code, output, error) = Run("new", "--help");
        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("--template-file", output, StringComparison.Ordinal);
        Assert.Contains("--culture", output, StringComparison.Ordinal);
        Assert.Contains("--preview", output, StringComparison.Ordinal);
        Assert.Equal(string.Empty, error);

        var general = Run("--help");
        Assert.Equal(ExitCodes.Success, general.Code);
        Assert.Contains("adr-guard new", general.Output, StringComparison.Ordinal);
        Assert.Contains("adr-guard draft", general.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("minimal", "en-US")]
    [InlineData("minimal", "pt-BR")]
    [InlineData("extended", "en-US")]
    [InlineData("extended", "pt-BR")]
    public void BuiltInTemplatesCreateValidatedProposedAdrs(
        string templateName,
        string culture)
    {
        var directory = CreateDirectory();
        try
        {
            var result = Run(
                "new", directory, "--title", "Adopt Cache",
                "--template", templateName, "--culture", culture);

            Assert.Equal(ExitCodes.Success, result.Code);
            Assert.Equal(string.Empty, result.Error);
            var path = Path.Combine(directory, "0001-adopt-cache.md");
            Assert.True(File.Exists(path));
            var content = File.ReadAllText(path);
            Assert.Contains("## Status\n\nProposed\n", content, StringComparison.Ordinal);
            Assert.Contains(
                culture == "pt-BR" ? "[EDITAR:" : "[EDIT:",
                content,
                StringComparison.Ordinal);
            Assert.Equal(
                templateName == "extended",
                content.Contains("## Decision Drivers\n", StringComparison.Ordinal));
            Assert.False(File.Exists(Path.Combine(directory, "README.md")));
            Assert.True(AdrValidator.Validate(
                AdrDocumentLoader.LoadDirectory(
                    directory,
                    TestContext.Current.CancellationToken)).IsValid);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void DefaultMinimalUsesNextIdWithoutModifyingExistingAdrOrIndex()
    {
        var directory = CreateDirectory();
        try
        {
            var original = File.ReadAllText(Fixture("minimal.en-US.md"))
                .Replace("# Adopt Redis", "# Existing", StringComparison.Ordinal);
            var currentPath = Path.Combine(directory, "0002-existing.md");
            File.WriteAllText(currentPath, original);
            var index = Path.Combine(directory, "README.md");
            File.WriteAllText(index, "Keep this index unchanged.");
            var result = Run("new", directory, "--title", "Use Kafka");

            Assert.Equal(ExitCodes.Success, result.Code);
            Assert.True(File.Exists(Path.Combine(directory, "0003-use-kafka.md")));
            Assert.Equal(original, File.ReadAllText(currentPath));
            Assert.Equal("Keep this index unchanged.", File.ReadAllText(index));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Theory]
    [InlineData("--dry-run")]
    [InlineData("--preview")]
    public void PreviewPrintsExactCandidateWithoutFilesystemChanges(string flag)
    {
        var directory = CreateDirectory();
        try
        {
            var before = Directory.EnumerateFiles(directory).ToArray();
            var result = Run(
                "new", directory, "--title", "Adopt Redis", "--template", "extended",
                flag);

            Assert.Equal(ExitCodes.Success, result.Code);
            Assert.Equal(string.Empty, result.Error);
            var path = Path.Combine(directory, "0001-adopt-redis.md");
            var prefix = $"ADR preview path: {path}{Environment.NewLine}{Environment.NewLine}";
            Assert.StartsWith(prefix, result.Output, StringComparison.Ordinal);
            var markdown = result.Output[prefix.Length..];
            Assert.Equal(
                AdrMarkdownRenderer.RenderTemplate(
                    new AdrTemplateRenderRequest(
                        "Adopt Redis",
                        AdrBuiltInTemplates.Get("extended", "en-US"),
                        new Dictionary<string, string>(),
                        Id: 1)),
                markdown);
            Assert.Equal(before, Directory.EnumerateFiles(directory).ToArray());
            Assert.False(File.Exists(path));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void CustomTemplateCreatesExactIdAndSeparatesInputFromOutput()
    {
        var directory = CreateDirectory();
        var input = CreateDirectory();
        try
        {
            var template = Path.Combine(input, "custom.md");
            File.Copy(Fixture("custom-template.pt-BR.md"), template);

            var result = Run(
                "new", directory, "--title", "Adotar Cache",
                "--culture", "pt-br", "--template-file", template);

            Assert.Equal(ExitCodes.Success, result.Code);
            var path = Path.Combine(directory, "0001-adotar-cache.md");
            Assert.True(File.Exists(path));
            Assert.Equal(
                "ADR 0001",
                "ADR " + File.ReadAllText(path).Split("ADR ")[1][..4]);
            Assert.True(File.Exists(template));
            Assert.Single(Directory.GetFiles(directory, "*.md"));
            Assert.True(AdrValidator.Validate(
                AdrDocumentLoader.LoadDirectory(
                    directory,
                    TestContext.Current.CancellationToken)).IsValid);
        }
        finally
        {
            DeleteDirectory(directory);
            DeleteDirectory(input);
        }
    }

    [Fact]
    public void InvalidCliArgumentsReturnUsageErrorWithoutCreatingAnything()
    {
        string[][] invalidCases =
        [
            ["new"],
            ["new", "--title", ""],
            ["new", "--title", " "],
            ["new", "--title", "💥"],
            ["new", "--title", "Hello\n## Decision"],
            ["new", "--title", "Use Redis", "--template", "unknown"],
            ["new", "--title", "Use Redis", "--culture", "fr-FR"],
            ["new", "--title", "Use Redis", "--template", "extended", "--template-file", "custom.md"],
            ["new", "--title", "Use Redis", "--title", "Again"],
            ["new", "--title", "Use Redis", "--dry-run", "--preview"],
            ["new", "--title", "Use Redis", "--invalid"],
        ];

        foreach (var arguments in invalidCases)
        {
            var result = Run(arguments);
            Assert.True(
                result.Code == ExitCodes.UsageError,
                $"Arguments: {string.Join(" / ", arguments)}. Exit: {result.Code}. Error: {result.Error}");
            Assert.NotEqual(string.Empty, result.Error);
        }
    }

    [Fact]
    public void MissingDirectoryAndMalformedCustomTemplateAreOperationalErrors()
    {
        var root = CreateDirectory();
        try
        {
            var missing = Run(
                "new", Path.Combine(root, "missing"), "--title", "Adopt Cache");
            Assert.Equal(ExitCodes.OperationalError, missing.Code);
            Assert.False(Directory.Exists(Path.Combine(root, "missing")));

            var invalidPath = Path.Combine(root, "invalid.md");
            File.WriteAllText(invalidPath, "# Invalid template");

            var malformed = Run(
                "new", root, "--title", "Adopt Cache",
                "--template-file", invalidPath);
            Assert.Equal(ExitCodes.OperationalError, malformed.Code);
            Assert.Contains("template", malformed.Error, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(Path.Combine(root, "0001-adopt-cache.md")));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void ExistingAdrValidationIssuesReturnCodeOneAndDoNotPersist()
    {
        var root = CreateDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(root, "0001-broken.md"),
                "# Broken\n\n## Status\n\nProposed\n");
            var result = Run("new", root, "--title", "Adopt Cache");

            Assert.Equal(ExitCodes.ValidationFailed, result.Code);
            Assert.Contains("Validation failed", result.Error, StringComparison.Ordinal);
            Assert.Single(Directory.GetFiles(root, "*.md"));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void ExhaustedIdFailsBeforeFileCreation()
    {
        var root = CreateDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(root, "9999-last-adr.md"),
                File.ReadAllText(Fixture("minimal.en-US.md"))
                    .Replace("# Adopt Redis", "# Last ADR", StringComparison.Ordinal));
            var result = Run("new", root, "--title", "Next ADR");

            Assert.Equal(ExitCodes.OperationalError, result.Code);
            Assert.Contains("9999", result.Error, StringComparison.Ordinal);
            Assert.Single(Directory.GetFiles(root, "*.md"));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task ConcurrentCreatorsReRenderCustomIdUnderSharedCreationMutex()
    {
        var root = CreateDirectory();
        var blocking = new BlockingPersistence();
        try
        {
            var template = AdrCustomTemplateLoader.Load(
                Fixture("custom-template.en-US.md"),
                "en-US",
                cancellationToken: TestContext.Current.CancellationToken);
            var first = new AdrNewService(blocking)
                .CreateAsync(
                    root,
                    "Adopt Redis",
                    template,
                    dryRun: false,
                    TestContext.Current.CancellationToken);

            await blocking.Entered.WaitAsync(
                TimeSpan.FromSeconds(10),
                TestContext.Current.CancellationToken);

            var second = new AdrNewService()
                .CreateAsync(
                    root,
                    "Adopt Kafka",
                    template,
                    dryRun: false,
                    TestContext.Current.CancellationToken);

            await Task.Delay(100, TestContext.Current.CancellationToken);
            Assert.False(second.IsCompleted);
            blocking.Release();

            var results = await Task.WhenAll(first, second)
                .WaitAsync(
                    TimeSpan.FromSeconds(10),
                    TestContext.Current.CancellationToken);
            Assert.All(results, result => Assert.True(result.Written));

            foreach (var result in results)
            {
                var path = Assert.IsType<string>(result.FilePath);
                var content = File.ReadAllText(path);
                var finalId = Path.GetFileName(path)[..4];

                Assert.Contains($"ADR {finalId}", content, StringComparison.Ordinal);
                Assert.Equal(content, result.Content);
            }

            Assert.Equal(
                ["0001", "0002"],
                results.Select(result => Path.GetFileName(result.FilePath!)[..4])
                    .Order(StringComparer.Ordinal)
                    .ToArray());
            Assert.True(AdrValidator.Validate(
                AdrDocumentLoader.LoadDirectory(
                    root,
                    TestContext.Current.CancellationToken)).IsValid);
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

    private static (int Code, string Output, string Error) Run(params string[] arguments)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = CliApplication.Run(
            arguments,
            output,
            error,
            TestContext.Current.CancellationToken);
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
        var path = Path.Combine(
            Path.GetTempPath(),
            "adr-guard-new-tests",
            Guid.NewGuid().ToString("N"));
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
            await _inner.WriteNewAsync(
                filePath,
                content,
                cancellationToken);
        }
    }
}
