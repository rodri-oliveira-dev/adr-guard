using System.Diagnostics;
using System.Text.Json;
using AdrGuard.Cli;
using Xunit;

namespace AdrGuard.Tests.Cli;

public sealed class ImpactCommandIntegrationTests
{
    [Fact]
    public async Task JsonSuccessIsStableAdvisoryAndKeepsStderrClean()
    {
        var root = CreateRepository(mappedChange: true);
        try
        {
            var first = await RunProcessAsync(
                root,
                "impact", ".", "--base-ref", "HEAD", "--map", ".adrguard-impact.json", "--format", "json");
            var second = await RunProcessAsync(
                root,
                "impact", ".", "--base-ref", "HEAD", "--map", ".adrguard-impact.json", "--format", "json");

            Assert.Equal(ExitCodes.Success, first.ExitCode);
            Assert.Equal(string.Empty, first.Error);
            Assert.Equal(first.Output, second.Output);
            using var document = JsonDocument.Parse(first.Output);
            var report = document.RootElement;
            Assert.Equal("1.0", report.GetProperty("schemaVersion").GetString());
            Assert.True(report.GetProperty("advisory").GetBoolean());
            Assert.Equal(1, report.GetProperty("coverage").GetProperty("affectedChanges").GetInt32());
            Assert.Equal("affected", report.GetProperty("decisions")[0].GetProperty("status").GetString());
            Assert.Equal("src/service.cs", report.GetProperty("decisions")[0].GetProperty("evidence")[0].GetProperty("path").GetString());
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task NoMatchesRemainSuccessfulAndAreReportedAsCoverage()
    {
        var root = CreateRepository(mappedChange: false);
        try
        {
            var result = await RunProcessAsync(
                root,
                "impact", ".", "--base-ref", "HEAD", "--map", ".adrguard-impact.json");

            Assert.Equal(ExitCodes.Success, result.ExitCode);
            Assert.Equal(string.Empty, result.Error);
            Assert.Contains("advisory", result.Output, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("1 changed file(s) have no explicit ADR mapping", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Theory]
    [InlineData("yaml")]
    [InlineData("")]
    public async Task MalformedReportRequestReturnsUsageError(string format)
    {
        var root = CreateTempDirectory();
        try
        {
            var arguments = format.Length == 0
                ? new[] { "impact", ".", "--base-ref", "HEAD", "--map", "map.json", "--format" }
                : new[] { "impact", ".", "--base-ref", "HEAD", "--map", "map.json", "--format", format };

            var result = await RunProcessAsync(root, arguments);

            Assert.Equal(ExitCodes.UsageError, result.ExitCode);
            Assert.Equal(string.Empty, result.Output);
            Assert.Contains("Invalid arguments for 'impact'", result.Error, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task MissingBaseReturnsOperationalErrorOnlyOnStderr()
    {
        var root = CreateRepository(mappedChange: true);
        try
        {
            var result = await RunProcessAsync(
                root,
                "impact", ".", "--base-ref", "refs/heads/missing", "--map", ".adrguard-impact.json", "--format", "json");

            Assert.Equal(ExitCodes.OperationalError, result.ExitCode);
            Assert.Equal(string.Empty, result.Output);
            Assert.Contains("Unable to analyze architecture impact", result.Error, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task DisconnectedBaseReturnsOperationalError()
    {
        var root = CreateRepository(mappedChange: false, createChange: false);
        try
        {
            RunGit(root, "switch", "--orphan", "unrelated");
            File.WriteAllText(Path.Combine(root, "unrelated.txt"), "unrelated\n");
            RunGit(root, "add", ".");
            RunGit(root, "commit", "-m", "unrelated history");
            var unrelated = RunGit(root, "rev-parse", "HEAD").Trim();
            RunGit(root, "switch", "main");

            var result = await RunProcessAsync(
                root,
                "impact", ".", "--base-ref", unrelated, "--map", ".adrguard-impact.json", "--format", "json");

            Assert.Equal(ExitCodes.OperationalError, result.ExitCode);
            Assert.Equal(string.Empty, result.Output);
            Assert.Contains("Git exited", result.Error, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void VersionedSchemaAndExampleExposeRequiredContractSections()
    {
        var schemaPath = Path.Combine(AppContext.BaseDirectory, "Schemas", "adr-impact-report-v1.schema.json");
        using var schema = JsonDocument.Parse(File.ReadAllText(schemaPath));
        Assert.Equal("object", schema.RootElement.GetProperty("type").GetString());
        Assert.Contains(
            schema.RootElement.GetProperty("required").EnumerateArray(),
            item => item.GetString() == "uncertainty");

        var repositoryRoot = FindRepositoryRoot();
        var examplePath = Path.Combine(repositoryRoot, "docs", "examples", "adr-impact-report-v1.json");
        using var example = JsonDocument.Parse(File.ReadAllText(examplePath));
        Assert.Equal("1.0", example.RootElement.GetProperty("schemaVersion").GetString());
        Assert.True(example.RootElement.GetProperty("advisory").GetBoolean());
    }

    private static string CreateRepository(bool mappedChange, bool createChange = true)
    {
        var root = CreateTempDirectory();
        RunGit(root, "init", "-b", "main");
        RunGit(root, "config", "user.email", "tests@example.com");
        RunGit(root, "config", "user.name", "ADR Guard Tests");
        Directory.CreateDirectory(Path.Combine(root, "docs", "adr"));
        Directory.CreateDirectory(Path.Combine(root, "src"));
        File.WriteAllText(Path.Combine(root, "docs", "adr", "0001-service.md"), ValidAdr);
        File.WriteAllText(Path.Combine(root, "src", "service.cs"), "initial\n");
        File.WriteAllText(Path.Combine(root, "README.md"), "initial\n");
        File.WriteAllText(Path.Combine(root, ".adrguard-impact.json"), Manifest);
        RunGit(root, "add", ".");
        RunGit(root, "commit", "-m", "initial");

        if (createChange)
        {
            var changedPath = mappedChange
                ? Path.Combine(root, "src", "service.cs")
                : Path.Combine(root, "README.md");
            File.AppendAllText(changedPath, "changed\n");
        }

        return root;
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
            CreateNoWindow = true,
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
        return new ProcessResult(process.ExitCode, await outputTask, await errorTask);
    }

    private static string RunGit(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"git {string.Join(' ', arguments)} failed: {error}");
        return output;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AdrGuard.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Unable to locate repository root.");
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"adr-guard-impact-cli-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(path, recursive: true);
        }
    }

    private const string Manifest = """
        {
          "schemaVersion": "1.0",
          "mappings": [
            {
              "decision": { "stableId": "ADR-1", "path": "docs/adr/0001-service.md" },
              "patterns": ["src/**"],
              "relationship": "governs",
              "reason": "The decision governs service source changes."
            }
          ]
        }
        """;

    private const string ValidAdr = """
        # Service boundary

        ## Status

        Accepted

        ## Context

        The service needs an explicit boundary.

        ## Decision

        Keep the service behind the boundary.

        ## Consequences

        Source changes require architectural review.
        """;

    private sealed record ProcessResult(int ExitCode, string Output, string Error);
}
