using AdrGuard.Cli;
using System.Text.Json;
using Xunit;

namespace AdrGuard.Tests.Cli;

public sealed class GovernanceReportIntegrationTests
{
    [Theory]
    [InlineData("json")]
    [InlineData("sarif")]
    public void NewGovernanceDiagnosticsAppearInMachineReports(string format)
    {
        var root = Path.Combine(Path.GetTempPath(), $"adr-guard-governance-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            File.WriteAllText(
                Path.Combine(root, "0001-first.md"),
                Canonical("First", "Superseded", "0002-second.md"));
            File.WriteAllText(
                Path.Combine(root, "0002-second.md"),
                Canonical("Second", "Superseded", "0001-first.md"));

            using var output = new StringWriter();
            using var error = new StringWriter();
            var exitCode = CliApplication.Run(
                ["check", root, "--format", format],
                output,
                error);

            Assert.Equal(ExitCodes.ValidationFailed, exitCode);
            Assert.Equal(string.Empty, error.ToString());
            using var report = JsonDocument.Parse(output.ToString());
            var serialized = report.RootElement.ToString();
            Assert.Contains("ADR010", serialized, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string Canonical(string title, string status, string target) =>
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
        ## Superseded by
        [Next]({target})
        """;
}
