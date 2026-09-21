using AdrGuard.Cli;
using AdrGuard.Generation;
using AdrGuard.Parsing;
using AdrGuard.Validation;
using System.Text;
using Xunit;

namespace AdrGuard.Tests.Generation;

public sealed class AdrCustomTemplateLoaderTests
{
    [Theory]
    [InlineData("en-US", "Adopt Cache", "Decision Drivers")]
    [InlineData("pt-BR", "Adotar Cache", "Critérios de decisão")]
    public void DocumentedCustomFixtureRendersAndPassesCheck(
        string culture,
        string title,
        string supplementalHeading)
    {
        var input = CreateDirectory();
        var output = CreateDirectory();
        try
        {
            var source = Path.Combine(
                input,
                "custom.md");
            File.Copy(Fixture(culture), source);

            var selected = Select(
                builtInName: null,
                templateFilePath: source,
                cultureName: culture);
            var rendered = AdrMarkdownRenderer.RenderTemplate(
                new AdrTemplateRenderRequest(
                    title,
                    selected,
                    new Dictionary<string, string>
                    {
                        ["context"] = "Explain the business requirement.",
                        ["decision"] = "Use a bounded cache.",
                        ["consequences"] = "Monitor cache misses.",
                    },
                    Id: 1));

            Assert.Contains("# " + title + "\n", rendered, StringComparison.Ordinal);
            Assert.Contains("## " + supplementalHeading + "\n", rendered, StringComparison.Ordinal);
            Assert.Contains("ADR 0001", rendered, StringComparison.Ordinal);
            Assert.Contains("## Status\n\nProposed\n", rendered, StringComparison.Ordinal);
            Assert.Equal(1, rendered.Split("## Status", StringSplitOptions.None).Length - 1);
            Assert.DoesNotContain("\r", rendered, StringComparison.Ordinal);

            var creation = AdrCreationService.Prepare(
                output,
                title,
                rendered,
                [],
                TestContext.Current.CancellationToken);
            Assert.True(creation.ValidationResult.IsValid);
            Assert.Equal(Path.GetFullPath(output), Path.GetDirectoryName(creation.FilePath));

            File.WriteAllText(creation.FilePath, rendered);
            using var checkOutput = new StringWriter();
            using var checkError = new StringWriter();
            var exitCode = CheckCommand.Run(output, checkOutput, checkError);

            Assert.Equal(ExitCodes.Success, exitCode);
            Assert.Equal(string.Empty, checkError.ToString());
            Assert.Single(Directory.GetFiles(output, "*.md"));
            Assert.True(File.Exists(source));
            Assert.Equal(
                rendered,
                AdrMarkdownRenderer.RenderTemplate(
                    new AdrTemplateRenderRequest(
                        title,
                        selected,
                        new Dictionary<string, string>
                        {
                            ["context"] = "Explain the business requirement.",
                            ["decision"] = "Use a bounded cache.",
                            ["consequences"] = "Monitor cache misses.",
                        },
                        Id: 1)));
        }
        finally
        {
            DeleteDirectory(input);
            DeleteDirectory(output);
        }
    }

    [Fact]
    public void ExplicitRelativePathIsResolvedFromInvocationDirectoryAndNoOtherFilesAreRead()
    {
        var root = CreateDirectory();
        try
        {
            var config = Path.Combine(root, "config");
            Directory.CreateDirectory(config);
            var target = Path.Combine(config, "adr-template.md");
            File.Copy(Fixture("en-US"), target);
            File.WriteAllText(
                Path.Combine(config, "unrelated.md"),
                "# Invalid extra file\n## Status\nAccepted");

            var loaded = Load(
                Path.Combine("config", "adr-template.md"),
                "en-US",
                root);
            var selected = Select(
                builtInName: null,
                templateFilePath: Path.Combine("config", "adr-template.md"),
                cultureName: "en-US",
                invocationDirectory: root);

            Assert.Equal(
                AdrMarkdownRenderer.RenderTemplate(
                    new AdrTemplateRenderRequest(
                        "Adopt Cache",
                        loaded,
                        new Dictionary<string, string>(),
                        Id: 42)),
                AdrMarkdownRenderer.RenderTemplate(
                    new AdrTemplateRenderRequest(
                        "Adopt Cache",
                        selected,
                        new Dictionary<string, string>(),
                        Id: 42)));
            Assert.Contains(
                "## Decision Drivers",
                AdrMarkdownRenderer.RenderTemplate(
                    new AdrTemplateRenderRequest(
                        "Adopt Cache",
                        selected,
                        new Dictionary<string, string>(),
                        Id: 42)),
                StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(root, "0001-adopt-cache.md")));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void SelectionDefaultsToMinimalAndRejectsConflictsAndUnknownNames()
    {
        var root = CreateDirectory();
        try
        {
            var path = Path.Combine(root, "custom.md");
            File.Copy(Fixture("en-US"), path);

            Assert.Equal(
                AdrMarkdownRenderer.RenderTemplate(
                    new AdrTemplateRenderRequest(
                        "Adopt Cache",
                        AdrBuiltInTemplates.Get("minimal", "en-US"),
                        new Dictionary<string, string>())),
                AdrMarkdownRenderer.RenderTemplate(
                    new AdrTemplateRenderRequest(
                        "Adopt Cache",
                        Select(null, null, "en-US"),
                        new Dictionary<string, string>())));

            var conflict = Assert.Throws<ArgumentException>(
                () => Select("extended", path, "en-US"));
            Assert.Contains("--template and --template-file", conflict.Message, StringComparison.Ordinal);

            var explicitDefaultConflict = Assert.Throws<ArgumentException>(
                () => Select("minimal", path, "en-US"));
            Assert.Contains("mutually exclusive", explicitDefaultConflict.Message, StringComparison.Ordinal);

            var unknown = Assert.Throws<ArgumentException>(
                () => Select("unexpected", null, "en-US"));
            Assert.Contains("minimal", unknown.Message, StringComparison.Ordinal);

            Assert.Throws<ArgumentException>(
                () => Select(null, " ", "en-US"));
            Assert.Throws<ArgumentException>(
                () => Select(null, "plain.txt", "en-US", root));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void MissingAndInaccessibleTemplatePathsProduceActionableErrors()
    {
        var root = CreateDirectory();
        try
        {
            var missing = Assert.Throws<IOException>(
                () => Load(
                    "missing.md",
                    "en-US",
                    root));
            Assert.Contains("--template-file", missing.Message, StringComparison.Ordinal);

            var invalidDirectory = Assert.Throws<IOException>(
                () => Load(
                    Path.Combine("not-created", "missing.md"),
                    "en-US",
                    root));
            Assert.Contains("directory", invalidDirectory.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void OversizedTemplateIsRejectedBeforeRenderingOrWritingAnAdr()
    {
        var root = CreateDirectory();
        try
        {
            var path = Path.Combine(root, "oversized.md");
            File.WriteAllText(
                path,
                new string('a', AdrCustomTemplateLoader.MaximumTemplateBytes + 1));

            var error = Assert.Throws<InvalidDataException>(
                () => Load(path, "en-US"));

            Assert.Contains("65536-byte", error.Message, StringComparison.Ordinal);
            Assert.Single(Directory.GetFiles(root));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void InvalidUtf8AndNulBytesAreRejectedBeforeRendering()
    {
        var root = CreateDirectory();
        try
        {
            var path = Path.Combine(root, "invalid.md");
            File.WriteAllBytes(path, [0x23, 0x20, 0xC3, 0x28]);

            var encoding = Assert.Throws<InvalidDataException>(
                () => Load(path, "en-US"));
            Assert.Contains("UTF-8", encoding.Message, StringComparison.Ordinal);

            File.WriteAllText(
                path,
                "# {{title}}\n\0\n## Status\n\n{{status}}");
            var nul = Assert.Throws<InvalidDataException>(
                () => Load(path, "en-US"));
            Assert.Contains("NUL", nul.Message, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void ValidUtf8BomAndLfNormalizationAreSupported()
    {
        var root = CreateDirectory();
        try
        {
            var source = File.ReadAllText(Fixture("en-US"))
                .Replace("\n", "\r\n", StringComparison.Ordinal);
            var path = Path.Combine(root, "bom.md");
            File.WriteAllText(
                path,
                source,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            var template = Load(path, "en-US");
            var output = AdrMarkdownRenderer.RenderTemplate(
                new AdrTemplateRenderRequest(
                    "Adopt Cache",
                    template,
                    new Dictionary<string, string>(),
                    Id: 17));

            Assert.Contains("ADR 0017", output, StringComparison.Ordinal);
            Assert.DoesNotContain("\r", output, StringComparison.Ordinal);
            Assert.True(AdrValidator.Validate(
                [AdrMarkdownParser.Parse("0017-adopt-cache.md", output)]).IsValid);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Theory]
    [InlineData("## Status\n\nAccepted", "Status")]
    [InlineData("## Status\n\n{{status}}\nAccepted", "Status")]
    [InlineData("## Status\n\n{{status}}\n\n## Context\n\nText\n\n## Context\n\nDuplicate", "Duplicate")]
    [InlineData("## Status\n\n{{status}}\n\n## Context\n\nText\n\n## Decision\n\nText", "Consequences")]
    [InlineData("## Status\n\n{{status}}\n\n## Context\n\nText\n\n## Decision\n\nText\n\n## Consequences\n\n{{not-supported}}", "placeholder")]
    public void MalformedOrIncompatibleTemplatesFailBeforePersistence(
        string sections,
        string expected)
    {
        var root = CreateDirectory();
        try
        {
            var templatePath = Path.Combine(root, "invalid.md");
            File.WriteAllText(templatePath, "# {{title}}\n\n" + sections);

            var error = Assert.Throws<InvalidDataException>(
                () => Load(templatePath, "en-US"));

            Assert.Contains(expected, error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Single(Directory.GetFiles(root));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Theory]
    [InlineData("# Wrong title\n")]
    [InlineData("# {{title}}\n## Status\nProposed\n# Extra title\n")]
    [InlineData("# {{title}}\n## Status\n{{status}}\n## Context\nExtra\n## {{dynamic}}\nBroken")]
    public void CustomTemplateMustControlH1AndHeadings(string source)
    {
        var root = CreateDirectory();
        try
        {
            var path = Path.Combine(root, "invalid.md");
            File.WriteAllText(path, source);

            Assert.Throws<InvalidDataException>(
                () => Load(path, "en-US"));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Theory]
    [InlineData("{{missing}}")]
    [InlineData("{{ context }}")]
    [InlineData("{{{status}}}")]
    [InlineData("{title}")]
    [InlineData("${title}")]
    [InlineData("{% execute %}")]
    public void UnsupportedPlaceholderSyntaxIsRejected(string input)
    {
        var root = CreateDirectory();
        try
        {
            var path = Path.Combine(root, "invalid.md");
            var source = File.ReadAllText(Fixture("en-US"))
                .Replace(
                    "[EDIT: Document the proposed approach.]",
                    input,
                    StringComparison.Ordinal);
            File.WriteAllText(path, source);

            var error = Assert.Throws<InvalidDataException>(
                () => Load(path, "en-US"));

            Assert.Contains("placeholder", error.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void AuthorContentCannotInjectHeadingsOrReplaceReservedIdStatus()
    {
        var template = Load(
            Fixture("en-US"),
            "en-US");
        var attack = new AdrTemplateRenderRequest(
            "Adopt Cache",
            template,
            new Dictionary<string, string>
            {
                ["context"] = "Context\n## Status\nAccepted",
            },
            Id: 7);

        var error = Assert.Throws<InvalidOperationException>(
            () => AdrMarkdownRenderer.RenderTemplate(attack));
        Assert.Contains("level-one or level-two", error.Message, StringComparison.Ordinal);

        Assert.Throws<ArgumentException>(
            () => AdrMarkdownRenderer.RenderTemplate(
                attack with
                {
                    Substitutions = new Dictionary<string, string>
                    {
                        ["id"] = "9999",
                    },
                }));
        Assert.Throws<ArgumentException>(
            () => AdrMarkdownRenderer.RenderTemplate(
                attack with
                {
                    Substitutions = new Dictionary<string, string>
                    {
                        ["status"] = "Accepted",
                    },
                }));
        Assert.Throws<InvalidOperationException>(
            () => AdrMarkdownRenderer.RenderTemplate(
                attack with { Id = null, Substitutions = new Dictionary<string, string>() }));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => AdrMarkdownRenderer.RenderTemplate(
                attack with { Id = 10000, Substitutions = new Dictionary<string, string>() }));
    }

    [Fact]
    public void ShellLookingTextIsNeverExecutedOrUsedAsAFileTarget()
    {
        var root = CreateDirectory();
        try
        {
            var source = File.ReadAllText(Fixture("en-US"))
                .Replace(
                    "[EDIT: Document the proposed approach.]",
                    "$(touch sentinel-should-not-exist)",
                    StringComparison.Ordinal);
            var templatePath = Path.Combine(root, "template.md");
            File.WriteAllText(templatePath, source);

            var loaded = Load(templatePath, "en-US");
            var rendered = AdrMarkdownRenderer.RenderTemplate(
                new AdrTemplateRenderRequest(
                    "Adopt Cache",
                    loaded,
                    new Dictionary<string, string>(),
                    Id: 1));

            Assert.Contains("$(touch sentinel-should-not-exist)", rendered, StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(root, "sentinel-should-not-exist")));
            Assert.False(File.Exists(Path.Combine(root, "0001-adopt-cache.md")));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static AdrTemplateDefinition Load(
        string templateFilePath,
        string cultureName,
        string? invocationDirectory = null) =>
        AdrCustomTemplateLoader.Load(
            templateFilePath,
            cultureName,
            invocationDirectory,
            TestContext.Current.CancellationToken);

    private static AdrTemplateDefinition Select(
        string? builtInName,
        string? templateFilePath,
        string cultureName,
        string? invocationDirectory = null) =>
        AdrTemplateSelection.Resolve(
            builtInName,
            templateFilePath,
            cultureName,
            invocationDirectory,
            TestContext.Current.CancellationToken);

    private static string Fixture(string cultureName) =>
        Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "Templates",
            $"custom-template.{cultureName}.md");

    private static string CreateDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "adr-guard-custom-template-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
