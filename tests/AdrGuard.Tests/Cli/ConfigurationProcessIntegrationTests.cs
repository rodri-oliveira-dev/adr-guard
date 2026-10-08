using AdrGuard.Cli;
using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace AdrGuard.Tests.Cli;

public sealed class ConfigurationProcessIntegrationTests
{
    [Fact]
    public async Task ProcessUsesConfiguredDirectoryAndMachineFormat()
    {
        var root = CreateTempDirectory();

        try
        {
            var adrDirectory = Path.Combine(root, "records");
            Directory.CreateDirectory(adrDirectory);
            File.WriteAllText(Path.Combine(root, ".adrguard.yml"), "schema-version: 1\nadr-directory: records\n");
            File.WriteAllText(Path.Combine(adrDirectory, "0001-valid.md"), ValidMarkdown);

            var result = await RunProcessAsync(root, "check", "--format", "json");

            Assert.Equal(ExitCodes.Success, result.ExitCode);
            Assert.Equal(string.Empty, result.Error);
            using var report = JsonDocument.Parse(result.Output);
            Assert.True(report.RootElement.GetProperty("valid").GetBoolean());
            Assert.Equal(1, report.RootElement.GetProperty("summary").GetProperty("files").GetInt32());
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task ExplicitDirectoryOverridesConfigurationAndBadConfigurationFailsBeforeValidation()
    {
        var root = CreateTempDirectory();

        try
        {
            var configured = Path.Combine(root, "configured");
            var explicitDirectory = Path.Combine(root, "explicit");
            Directory.CreateDirectory(configured);
            Directory.CreateDirectory(explicitDirectory);
            File.WriteAllText(Path.Combine(root, ".adrguard.yml"), "schema-version: 1\nadr-directory: configured\n");
            File.WriteAllText(Path.Combine(configured, "0001-invalid.md"), "# Invalid");
            File.WriteAllText(Path.Combine(explicitDirectory, "0001-valid.md"), ValidMarkdown);

            var explicitResult = await RunProcessAsync(root, "check", "explicit");
            Assert.Equal(ExitCodes.Success, explicitResult.ExitCode);

            File.WriteAllText(Path.Combine(root, ".adrguard.yml"), "schema-version: 1\nunknown: value\n");
            var invalid = await RunProcessAsync(root, "check", "explicit");
            Assert.Equal(ExitCodes.UsageError, invalid.ExitCode);
            Assert.Contains("unknown property", invalid.Error, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Theory]
    [InlineData("check")]
    [InlineData("index")]
    [InlineData("new")]
    [InlineData("draft")]
    public async Task ConfiguredCommandHelpReturnsUsage(string command)
    {
        var root = CreateTempDirectory();

        try
        {
            File.WriteAllText(
                Path.Combine(root, ".adrguard.yml"),
                "schema-version: 1\nadr-directory: docs/adr\ntemplate: extended\n");

            var result = await RunProcessAsync(root, command, "--help");

            Assert.Equal(ExitCodes.Success, result.ExitCode);
            Assert.Equal(string.Empty, result.Error);
            Assert.Contains($"adr-guard {command}", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task JsonUsesInvocationRootForCheckedSubdirectory()
    {
        var root = CreateTempDirectory();

        try
        {
            var adrDirectory = Path.Combine(root, "docs", "adr");
            Directory.CreateDirectory(adrDirectory);
            File.WriteAllText(Path.Combine(adrDirectory, "0001-parent.md"), "# Invalid");

            var result = await RunProcessAsync(root, "check", "docs/adr", "--format", "json");

            Assert.Equal(ExitCodes.ValidationFailed, result.ExitCode);
            using var report = JsonDocument.Parse(result.Output);
            Assert.Equal(
                "docs/adr/0001-parent.md",
                report.RootElement.GetProperty("files")[0].GetString());
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Theory]
    [InlineData("json")]
    [InlineData("sarif")]
    public async Task ReportUsesOneRootAndSarifEscapesSpecialCharacters(string format)
    {
        var root = CreateTempDirectory();

        try
        {
            var checkedDirectory = Path.Combine(root, "docs", "adr");
            var nestedDirectory = Path.Combine(checkedDirectory, "team");
            Directory.CreateDirectory(nestedDirectory);
            File.WriteAllText(Path.Combine(checkedDirectory, "0001-parent.md"), "# Invalid");
            File.WriteAllText(Path.Combine(nestedDirectory, "0002-child.md"), "# Invalid");
            File.WriteAllText(Path.Combine(nestedDirectory, "0003-é #%.md"), "# Invalid");

            // Run from a child directory while checking its ancestor.
            var result = await RunProcessAsync(
                nestedDirectory, "check", "..", "--format", format);

            Assert.Equal(ExitCodes.ValidationFailed, result.ExitCode);
            Assert.Equal(string.Empty, result.Error);
            using var report = JsonDocument.Parse(result.Output);

            if (format == "json")
            {
                var files = report.RootElement.GetProperty("files")
                    .EnumerateArray().Select(item => item.GetString()).ToArray();
                Assert.Contains("0001-parent.md", files);
                Assert.Contains("team/0002-child.md", files);
                Assert.Contains("team/0003-é #%.md", files);
            }
            else
            {
                var uris = report.RootElement.GetProperty("runs")[0]
                    .GetProperty("results").EnumerateArray()
                    .Select(item => item.GetProperty("locations")[0]
                        .GetProperty("physicalLocation")
                        .GetProperty("artifactLocation").GetProperty("uri").GetString())
                    .ToArray();

                Assert.Contains("0001-parent.md", uris);
                Assert.Contains("team/0002-child.md", uris);
                Assert.Contains("team/0003-%C3%A9%20%23%25.md", uris);
                Assert.All(uris, uri => Assert.True(
                    Uri.TryCreate(uri, UriKind.Relative, out _)));
            }
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static async Task<ProcessResult> RunProcessAsync(
        string workingDirectory,
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(typeof(CliApplication).Assembly.Location);
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)!;
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new ProcessResult(
            process.ExitCode,
            await outputTask,
            await errorTask);
    }

    private const string ValidMarkdown = """
        # Valid

        ## Status
        Accepted

        ## Context
        Context.

        ## Decision
        Decision.

        ## Consequences
        Consequences.
        """;

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"adr-guard-process-{Guid.NewGuid():N}");
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

    private sealed record ProcessResult(int ExitCode, string Output, string Error);
}
