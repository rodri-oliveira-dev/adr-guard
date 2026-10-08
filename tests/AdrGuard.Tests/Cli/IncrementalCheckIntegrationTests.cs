using AdrGuard.Cli;
using AdrGuard.Git;
using System.Text.Json;
using Xunit;

namespace AdrGuard.Tests.Cli;

public sealed class IncrementalCheckIntegrationTests
{
    [Fact]
    public void ChangedModeFiltersUnchangedStructuralDebtButKeepsGlobalIntegrity()
    {
        var root = CreateRepository();

        try
        {
            var adr = Path.Combine(root, "docs", "adr");
            File.WriteAllText(Path.Combine(adr, "0001-legacy.md"), "# Legacy debt");
            File.WriteAllText(Path.Combine(adr, "0002-valid.md"), Canonical("Valid", "Accepted"));
            RunGit(root, "add", ".");
            RunGit(root, "commit", "-m", "base");
            RunGit(root, "switch", "-c", "feature");

            File.AppendAllText(Path.Combine(adr, "0002-valid.md"), "\n<!-- changed -->\n");
            var incremental = Run(["check", adr, "--changed", "--base-ref", "main"]);
            var full = Run(["check", adr]);

            Assert.Equal(ExitCodes.Success, incremental.ExitCode);
            Assert.Equal(ExitCodes.ValidationFailed, full.ExitCode);

            File.WriteAllText(
                Path.Combine(adr, "0003-duplicate.md"),
                Canonical("Duplicate", "Accepted"));
            File.Move(
                Path.Combine(adr, "0003-duplicate.md"),
                Path.Combine(adr, "0002-duplicate.md"));
            var globalFailure = Run(["check", adr, "--changed", "--base-ref", "main"]);

            Assert.Equal(ExitCodes.ValidationFailed, globalFailure.ExitCode);
            Assert.Contains("ADR006", globalFailure.Error, StringComparison.Ordinal);
        }
        finally
        {
            DeleteRepository(root);
        }
    }

    [Fact]
    public void ChangedBaselineDoesNotResolveUnchangedFindings()
    {
        var root = CreateRepository();
        try
        {
            var adr = Path.Combine(root, "docs", "adr");
            var legacy = Path.Combine(adr, "0001-legacy.md");
            var changed = Path.Combine(adr, "0002-current.md");
            File.WriteAllText(legacy, "# Legacy structural debt");
            File.WriteAllText(changed, Canonical("Current", "Accepted"));
            RunGit(root, "add", ".");
            RunGit(root, "commit", "-m", "base");
            var baselinePath = Path.Combine(root, ".adrguard-baseline.json");
            Assert.Equal(ExitCodes.Success,
                Run(["baseline", adr, "--output", baselinePath]).ExitCode);

            RunGit(root, "switch", "-c", "feature");
            File.AppendAllText(changed, "\n<!-- changed -->\n");
            var check = Run([
                "check", adr, "--changed", "--base-ref", "main",
                "--baseline", baselinePath, "--format", "json"]);

            Assert.Equal(ExitCodes.Success, check.ExitCode);
            using (var report = JsonDocument.Parse(check.Output))
            {
                var counts = report.RootElement.GetProperty("baseline");
                Assert.Equal(0, counts.GetProperty("new").GetInt32());
                Assert.Equal(0, counts.GetProperty("existing").GetInt32());
                Assert.Equal(0, counts.GetProperty("resolved").GetInt32());
            }

            File.WriteAllText(legacy, Canonical("Legacy fixed", "Accepted"));
            var fixedResult = Run([
                "check", adr, "--changed", "--base-ref", "main",
                "--baseline", baselinePath, "--format", "json"]);
            Assert.Equal(ExitCodes.Success, fixedResult.ExitCode);
            using var fixedReport = JsonDocument.Parse(fixedResult.Output);
            Assert.True(fixedReport.RootElement
                .GetProperty("baseline").GetProperty("resolved").GetInt32() > 0);
        }
        finally
        {
            DeleteRepository(root);
        }
    }

    [Theory]
    [InlineData("main injected")]
    [InlineData("--help")]
    [InlineData("bad\nref")]
    public void UnsafeBaseReferenceReturnsUsageError(string reference)
    {
        var root = Path.Combine(Path.GetTempPath(), $"adr-guard-ref-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var result = Run(["check", root, "--changed", "--base-ref", reference]);

            Assert.Equal(ExitCodes.UsageError, result.ExitCode);
            Assert.Contains("check", result.Error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ChangedModeFailsExplicitlyOutsideGitOrForMissingBase()
    {
        var root = Path.Combine(Path.GetTempPath(), $"adr-guard-no-git-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var noGit = Run(["check", root, "--changed", "--base-ref", "main"]);
            Assert.Equal(ExitCodes.OperationalError, noGit.ExitCode);
            Assert.Contains("Git", noGit.Error, StringComparison.OrdinalIgnoreCase);

            var usage = Run(["check", root, "--changed"]);
            Assert.Equal(ExitCodes.UsageError, usage.ExitCode);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateRepository()
    {
        var root = Path.Combine(Path.GetTempPath(), $"adr-guard-incremental-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "docs", "adr"));
        RunGit(root, "init", "--initial-branch=main");
        RunGit(root, "config", "user.email", "tests@example.invalid");
        RunGit(root, "config", "user.name", "ADR Guard Tests");
        return root;
    }

    private static void RunGit(string root, params string[] arguments) =>
        GitCommandRunner.Run(root, arguments, default);

    private static void DeleteRepository(string root)
    {
        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(path, FileAttributes.Normal);
        }

        Directory.Delete(root, recursive: true);
    }

    private static CommandResult Run(string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        return new CommandResult(CliApplication.Run(args, output, error), output.ToString(), error.ToString());
    }

    private static string Canonical(string title, string status) =>
        $"""
        # {title}
        ## Status
        {status}
        ## Context
        Context.
        ## Decision
        Decision.
        ## Consequences
        Consequences.
        """;

    private sealed record CommandResult(int ExitCode, string Output, string Error);
}
