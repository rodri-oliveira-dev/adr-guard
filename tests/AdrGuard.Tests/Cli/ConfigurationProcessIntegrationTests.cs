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
