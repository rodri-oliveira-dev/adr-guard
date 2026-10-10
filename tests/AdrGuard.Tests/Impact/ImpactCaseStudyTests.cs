using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using AdrGuard.Cli;
using Xunit;

namespace AdrGuard.Tests.Impact;

public sealed class ImpactCaseStudyTests
{
    [Fact]
    public void ReproducibleRepositoryMatchesVersionedJsonAndTextEvidence()
    {
        var fixtureRoot = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "Impact",
            "case-study");
        var repository = CreateTempDirectory();

        try
        {
            CopyDirectory(fixtureRoot, repository);
            RunGit(repository, "init", "-b", "main");
            RunGit(repository, "config", "user.email", "tests@example.com");
            RunGit(repository, "config", "user.name", "ADR Guard Tests");
            RunGit(repository, "add", ".");
            RunGit(repository, "commit", "-m", "case study baseline");

            File.AppendAllText(
                Path.Combine(repository, "src", "orders-api", "endpoint.cs.fixture"),
                "// changed endpoint\n");
            File.AppendAllText(
                Path.Combine(repository, "docs", "architecture-notes.md"),
                "\nThis non-ADR note changed without becoming a decision mapping.\n");
            Directory.CreateDirectory(Path.Combine(repository, "src", "unmapped"));
            File.WriteAllText(
                Path.Combine(repository, "src", "unmapped", "worker.cs.fixture"),
                "namespace CaseStudy.Unmapped;\n");
            RunGit(
                repository,
                "mv",
                "src/orders-storage/repository.cs.fixture",
                "src/orders-storage/renamed-repository.cs.fixture");
            RunGit(repository, "rm", "src/orders-v1/legacy-client.cs.fixture");

            var json = RunImpact(repository, "json");
            var text = RunImpact(repository, "text");
            var normalizedJson = NormalizeJson(json.Output);
            var normalizedText = NormalizeText(text.Output, json.MergeBase);

            Assert.Equal(ExitCodes.Success, json.ExitCode);
            Assert.Equal(ExitCodes.Success, text.ExitCode);
            Assert.Equal(string.Empty, json.Error);
            Assert.Equal(string.Empty, text.Error);

            var expectedRoot = Path.Combine(repository, "expected");
            Assert.Equal(
                File.ReadAllText(Path.Combine(expectedRoot, "impact-report-v1.json")),
                normalizedJson);
            Assert.Equal(
                File.ReadAllText(Path.Combine(expectedRoot, "impact-report.txt")),
                normalizedText);

            using var report = JsonDocument.Parse(normalizedJson);
            var decisions = report.RootElement.GetProperty("decisions").EnumerateArray().ToArray();
            Assert.Equal(
                decisions.Length,
                decisions.Select(decision => decision.GetProperty("stableId").GetString()).Distinct().Count());
            Assert.Equal(
                ["active", "proposed", "inactive"],
                decisions.Select(decision => decision.GetProperty("resolution").GetString()).ToArray());
            var ordinaryMarkdown = Assert.Single(
                report.RootElement.GetProperty("changedFiles").EnumerateArray(),
                change => change.GetProperty("newPath").GetString() == "docs/architecture-notes.md");
            Assert.Equal("not-matched", ordinaryMarkdown.GetProperty("status").GetString());
            Assert.Empty(ordinaryMarkdown.GetProperty("decisionStableIds").EnumerateArray());
        }
        finally
        {
            DeleteDirectory(repository);
        }
    }

    private static RunResult RunImpact(string repository, string format)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = CliApplication.Run(
            [
                "impact",
                repository,
                "--base-ref",
                "HEAD",
                "--map",
                ".adrguard-impact.json",
                "--format",
                format,
            ],
            output,
            error);
        var rendered = output.ToString();
        var mergeBase = format == "json"
            ? JsonNode.Parse(rendered)!["repository"]!["mergeBase"]!.GetValue<string>()
            : string.Empty;
        return new RunResult(exitCode, rendered, error.ToString(), mergeBase);
    }

    private static string NormalizeJson(string json)
    {
        var report = JsonNode.Parse(json)!;
        report["repository"]!["mergeBase"] = "<merge-base>";
        return report.ToJsonString(
                   new JsonSerializerOptions(JsonSerializerDefaults.Web)
                   {
                       WriteIndented = true,
                       NewLine = "\n",
                   })
               + "\n";
    }

    private static string NormalizeText(string text, string mergeBase) =>
        text.Replace(mergeBase, "<merge-base>", StringComparison.Ordinal)
            .Replace("\r\n", "\n", StringComparison.Ordinal);

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(
                destination,
                Path.GetRelativePath(source, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, Path.Combine(destination, Path.GetRelativePath(source, file)));
        }
    }

    private static void RunGit(string workingDirectory, params string[] arguments)
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
        Assert.True(
            process.ExitCode == 0,
            $"git {string.Join(' ', arguments)} failed: {output}{error}");
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"adr-guard-impact-case-study-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

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

    private sealed record RunResult(
        int ExitCode,
        string Output,
        string Error,
        string MergeBase);
}
