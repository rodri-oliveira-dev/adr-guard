using AdrGuard.Configuration;
using Xunit;

namespace AdrGuard.Tests.Configuration;

public sealed class AdrGuardConfigurationLoaderTests
{
    [Fact]
    public void LoadReadsStrictScalarConfigurationAndResolvesRelativePaths()
    {
        var root = CreateTempDirectory();

        try
        {
            Directory.CreateDirectory(Path.Combine(root, "templates"));
            File.WriteAllText(Path.Combine(root, "templates", "team.md"), "# Template");
            File.WriteAllText(
                Path.Combine(root, AdrGuardConfigurationLoader.FileName),
                """
                # ADR Guard configuration
                schema-version: 1
                adr-directory: "docs/decisions"
                template-file: 'templates/team.md'
                adr-format: canonical
                """);

            var configuration = AdrGuardConfigurationLoader.Load(root);

            Assert.NotNull(configuration);
            Assert.Equal("docs/decisions", configuration.AdrDirectory);
            Assert.Equal(Path.Combine(root, "docs", "decisions"), configuration.AdrDirectoryPath);
            Assert.Equal(Path.Combine(root, "templates", "team.md"), configuration.TemplateFilePath);
            Assert.Equal("canonical", configuration.AdrFormat);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Theory]
    [InlineData("schema-version: 1\nunknown: value\n", "unknown property")]
    [InlineData("schema-version: 1\ntemplate: minimal\ntemplate: extended\n", "duplicate property")]
    [InlineData("schema-version: 2\n", "unsupported schema-version")]
    [InlineData("schema-version: 1\ntemplate: other\n", "must be 'minimal' or 'extended'")]
    [InlineData("schema-version: 1\nadr-directory: ../outside\n", "unsafe path")]
    [InlineData("schema-version: 1\nitems: [one, two]\n", "unknown property")]
    public void LoadRejectsInvalidConfiguration(string text, string expected)
    {
        var root = CreateTempDirectory();

        try
        {
            File.WriteAllText(Path.Combine(root, AdrGuardConfigurationLoader.FileName), text);

            var exception = Assert.Throws<AdrGuardConfigurationException>(
                () => AdrGuardConfigurationLoader.Load(root));

            Assert.Contains(expected, exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void ApplyUsesCliThenConfigurationThenLegacyDefaults()
    {
        var configuration = new AdrGuardConfiguration(
            Path.GetFullPath("repo"),
            "docs/decisions",
            "extended",
            null,
            null);

        var configured = ConfigurationArguments.Apply(
            ["new", "--title", "Configured"],
            configuration);
        Assert.Equal(configuration.AdrDirectoryPath, configured[1]);
        Assert.Contains("--template", configured);
        Assert.Contains("extended", configured);

        var explicitArgs = new[]
        {
            "new", "custom/adrs", "--title", "Explicit", "--template", "minimal",
        };
        Assert.Equal(explicitArgs, ConfigurationArguments.Apply(explicitArgs, configuration));

        var legacy = new[] { "check" };
        Assert.Same(legacy, ConfigurationArguments.Apply(legacy, null));
    }

    [Theory]
    [InlineData("check", "--help")]
    [InlineData("check", "-h")]
    [InlineData("index", "--help")]
    [InlineData("index", "-h")]
    [InlineData("new", "--help")]
    [InlineData("new", "-h")]
    [InlineData("draft", "--help")]
    [InlineData("draft", "-h")]
    public void ApplyPreservesCommandHelpWithConfiguration(string command, string helpOption)
    {
        var configuration = new AdrGuardConfiguration(
            Path.GetFullPath("repo"),
            "docs/decisions",
            "extended",
            null,
            null);
        var args = new[] { command, helpOption };

        Assert.Same(args, ConfigurationArguments.Apply(args, configuration));
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"adr-guard-config-{Guid.NewGuid():N}");
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
}
