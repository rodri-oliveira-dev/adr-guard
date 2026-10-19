using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AdrGuard.Cli;
using Xunit;

namespace AdrGuard.Tests.Cli;

public sealed class CoreCliCrossPlatformSmokeTests
{
    [Fact]
    public async Task PublicCliCompletesPortableRepositoryJourney()
    {
        var smokeRoot = Path.Combine(
            Path.GetTempPath(),
            $"ADR Guard smoke 路径 {Guid.NewGuid():N}");
        var repository = Path.Combine(smokeRoot, "sample repository");

        try
        {
            Directory.CreateDirectory(repository);

            var init = await RunCliAsync(repository, "init", repository);
            AssertExitCode("init", ExitCodes.Success, init);
            var initAgain = await RunCliAsync(repository, "init", repository);
            AssertExitCode("init idempotent rerun", ExitCodes.Success, initAgain);
            Assert.Contains("Unchanged file", initAgain.Output, StringComparison.Ordinal);

            var adrDirectory = Path.Combine(repository, "docs", "adr");
            var create = await RunCliAsync(
                repository,
                "new",
                adrDirectory,
                "--title",
                "Adopt portable cache");
            AssertExitCode("new", ExitCodes.Success, create);

            var check = await RunCliAsync(repository, "check", adrDirectory, "--format", "json");
            AssertExitCode("check valid ADR", ExitCodes.Success, check);
            using (var report = JsonDocument.Parse(check.Output))
            {
                Assert.True(report.RootElement.GetProperty("valid").GetBoolean());
                Assert.Equal("1.0", report.RootElement.GetProperty("schemaVersion").GetString());
            }

            var index = await RunCliAsync(repository, "index", adrDirectory);
            AssertExitCode("index", ExitCodes.Success, index);
            var indexPath = Path.Combine(adrDirectory, "README.md");
            var firstIndex = await File.ReadAllTextAsync(
                indexPath,
                TestContext.Current.CancellationToken);
            var indexAgain = await RunCliAsync(repository, "index", adrDirectory);
            AssertExitCode("index idempotent rerun", ExitCodes.Success, indexAgain);
            Assert.Contains("already up to date", indexAgain.Output, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(
                firstIndex,
                await File.ReadAllTextAsync(indexPath, TestContext.Current.CancellationToken));

            var adrPath = Assert.Single(Directory.GetFiles(adrDirectory, "0001-*.md"));
            var validContent = await File.ReadAllTextAsync(
                adrPath,
                TestContext.Current.CancellationToken);
            var unchangedInvalidPath = Path.Combine(adrDirectory, "0002-unchanged-invalid.md");
            await File.WriteAllTextAsync(
                unchangedInvalidPath,
                validContent
                    .Replace("# Adopt portable cache", "# Unchanged invalid decision", StringComparison.Ordinal)
                    .Replace("\nProposed\n", "\nApproved\n", StringComparison.Ordinal),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                TestContext.Current.CancellationToken);

            await RunGitAsync(repository, "init", "--initial-branch=main");
            await RunGitAsync(repository, "config", "user.email", "adr-guard-smoke@example.invalid");
            await RunGitAsync(repository, "config", "user.name", "ADR Guard Smoke");
            await RunGitAsync(repository, "add", ".");
            await RunGitAsync(repository, "commit", "-m", "smoke baseline");

            var invalidContent = validContent.Replace(
                "\nProposed\n",
                "\nApproved\n",
                StringComparison.Ordinal);
            Assert.NotEqual(validContent, invalidContent);
            await File.WriteAllTextAsync(
                adrPath,
                invalidContent,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                TestContext.Current.CancellationToken);

            string[] changedArguments =
            [
                "check",
                adrDirectory,
                "--changed",
                "--base-ref",
                "main",
                "--format",
                "json",
                "--diagnostic-locations",
            ];
            var invalid = await RunCliAsync(repository, changedArguments);
            AssertExitCode("check changed invalid ADR", ExitCodes.ValidationFailed, invalid);
            var invalidAgain = await RunCliAsync(repository, changedArguments);
            AssertExitCode("check changed deterministic rerun", ExitCodes.ValidationFailed, invalidAgain);
            Assert.Equal(invalid.Output, invalidAgain.Output);
            Assert.Equal(invalid.Error, invalidAgain.Error);

            using var invalidReport = JsonDocument.Parse(invalid.Output);
            Assert.False(invalidReport.RootElement.GetProperty("valid").GetBoolean());
            var diagnostic = Assert.Single(
                invalidReport.RootElement.GetProperty("diagnostics").EnumerateArray());
            Assert.Equal("ADR004", diagnostic.GetProperty("code").GetString());
            Assert.EndsWith(
                Path.GetFileName(adrPath),
                diagnostic.GetProperty("file").GetString(),
                StringComparison.Ordinal);
            Assert.True(diagnostic.GetProperty("line").GetInt32() >= 1);
            Assert.True(diagnostic.GetProperty("column").GetInt32() >= 1);
        }
        finally
        {
            DeleteDirectory(smokeRoot);
        }
    }

    private static Task<ProcessResult> RunCliAsync(
        string workingDirectory,
        params string[] arguments) =>
        RunProcessAsync(
            "dotnet",
            [typeof(CliApplication).Assembly.Location, .. arguments],
            workingDirectory);

    private static async Task RunGitAsync(
        string workingDirectory,
        params string[] arguments)
    {
        var result = await RunProcessAsync("git", arguments, workingDirectory);
        AssertExitCode($"git {arguments[0]}", expected: 0, result);
    }

    private static async Task<ProcessResult> RunProcessAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string workingDirectory)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Unable to start '{executable}'.");
        var output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        return new ProcessResult(process.ExitCode, await output, await error);
    }

    private static void AssertExitCode(string testCase, int expected, ProcessResult result) =>
        Assert.True(
            result.ExitCode == expected,
            $"[{testCase}] Expected exit {expected} but received {result.ExitCode}."
                + $"{Environment.NewLine}stdout:{Environment.NewLine}{result.Output}"
                + $"{Environment.NewLine}stderr:{Environment.NewLine}{result.Error}");

    private static void DeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(path, recursive: true);
    }

    private sealed record ProcessResult(int ExitCode, string Output, string Error);
}
