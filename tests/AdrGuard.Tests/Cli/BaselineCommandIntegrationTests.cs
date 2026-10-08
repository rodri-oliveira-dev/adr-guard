using AdrGuard.Cli;
using System.Text.Json;
using Xunit;

namespace AdrGuard.Tests.Cli;

public sealed class BaselineCommandIntegrationTests
{
    [Fact]
    public void ExplicitGenerationAllowsLegacyIssueButRejectsNewIssue()
    {
        var root = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(root, "0001-legacy.md"), "# Legacy");
            var baselinePath = Path.Combine(root, "baseline.json");

            var generated = Run(["baseline", root, "--output", baselinePath]);
            Assert.Equal(ExitCodes.Success, generated.ExitCode);
            using (var document = JsonDocument.Parse(File.ReadAllText(baselinePath)))
            {
                Assert.Equal("1.0", document.RootElement.GetProperty("schemaVersion").GetString());
            }

            var accepted = Run(["check", root, "--baseline", baselinePath]);
            Assert.Equal(ExitCodes.Success, accepted.ExitCode);
            Assert.Contains("no new issues", accepted.Output, StringComparison.Ordinal);

            File.WriteAllText(Path.Combine(root, "0002-new.md"), "# New");
            var rejected = Run(["check", root, "--baseline", baselinePath, "--format", "json"]);
            Assert.Equal(ExitCodes.ValidationFailed, rejected.ExitCode);
            using var report = JsonDocument.Parse(rejected.Output);
            Assert.True(report.RootElement.GetProperty("baseline").GetProperty("new").GetInt32() > 0);

            Assert.Equal(
                ExitCodes.OperationalError,
                Run(["baseline", root, "--output", baselinePath]).ExitCode);
            Assert.Equal(
                ExitCodes.Success,
                Run(["baseline", root, "--output", baselinePath, "--update"]).ExitCode);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RepeatedBrokenReferenceCanBeCapturedInABaseline()
    {
        var root = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(root, "0001-broken.md"),
                ValidMarkdown + "\n## References\n[Missing](missing.md) and [Missing](missing.md)\n");
            var baselinePath = Path.Combine(root, "repeated.json");

            var generated = Run(["baseline", root, "--output", baselinePath]);

            Assert.Equal(ExitCodes.Success, generated.ExitCode);
            using var json = JsonDocument.Parse(File.ReadAllText(baselinePath));
            var entries = json.RootElement.GetProperty("diagnostics");
            Assert.Equal(1, entries.GetArrayLength());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void InvalidBaselineDoesNotTurnOperationalFailureIntoSuccess()
    {
        var root = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(root, "0001-valid.md"), ValidMarkdown);
            var baseline = Path.Combine(root, "invalid.json");
            File.WriteAllText(baseline, "{}");

            var result = Run(["check", root, "--baseline", baseline]);

            Assert.Equal(ExitCodes.OperationalError, result.ExitCode);
            Assert.Contains("baseline", result.Error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
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

    private static CommandResult Run(string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        return new CommandResult(CliApplication.Run(args, output, error), output.ToString(), error.ToString());
    }

    private static string CreateTempDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"adr-guard-baseline-cli-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private sealed record CommandResult(int ExitCode, string Output, string Error);
}
