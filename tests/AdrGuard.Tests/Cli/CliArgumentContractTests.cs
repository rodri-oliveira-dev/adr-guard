using AdrGuard.Cli;
using Xunit;

namespace AdrGuard.Tests.Cli;

public sealed class CliArgumentContractTests
{
    public static TheoryData<string, string[]> InvalidCommandArguments => new()
    {
        { "check", ["check", "--format"] },
        { "check", ["check", "--format", "json", "--format", "sarif"] },
        { "check", ["check", "first", "second"] },
        { "check", ["check", "--changed"] },
        { "check", ["check", "--base-ref", "main"] },
        { "relationships", ["relationships", "--format", "text"] },
        { "relationships", ["relationships", "first", "second"] },
        {
            "check",
            [
                "check",
                "--diagnostic-locations",
                "--validate-metadata",
                "--conventional-supersession",
                "--format",
                "text",
                "--adr-format",
                "canonical",
                "--lifecycle-statuses",
                "Under Review=proposed",
                "--filename-policy",
                "canonical",
                "--placeholder-policy",
                "off",
                "--validation-profile",
                "strict",
                "--unknown",
            ]
        },
        { "baseline", ["baseline"] },
        { "baseline", ["baseline", "--output"] },
        { "baseline", ["baseline", "--output", ""] },
        { "baseline", ["baseline", "--output", "   "] },
        { "baseline", ["baseline", "--output", "baseline.json", "--update", "--update"] },
        {
            "baseline",
            [
                "baseline",
                ".",
                "--output",
                "baseline.json",
                "--conventional-supersession",
                "--adr-format",
                "canonical",
                "--lifecycle-statuses",
                "Under Review=proposed",
                "--filename-policy",
                "canonical",
                "--validation-profile",
                "strict",
                "--unknown",
            ]
        },
        { "index", ["index", "--catalog", "basic"] },
        { "index", ["index", "--output", "README.md", "--output", "index.md"] },
        {
            "index",
            [
                "index",
                ".",
                "--output",
                "README.md",
                "--catalog",
                "enriched",
                "--conventional-supersession",
                "--adr-format",
                "canonical",
                "--lifecycle-statuses",
                "Under Review=proposed",
                "--filename-policy",
                "canonical",
                "--validation-profile",
                "strict",
                "--unknown",
            ]
        },
        { "impact", ["impact", "--base-ref", "main"] },
        { "impact", ["impact", "--map", ".adrguard-impact.json"] },
        { "impact", ["impact", "first", "second", "--base-ref", "main", "--map", "map.json"] },
        { "drift", ["drift", "--rules", "rules.json", "--update-baseline"] },
        { "drift", ["drift", "--rules", "rules.json", "--format", "sarif"] },
        { "drift", ["drift", "--rules", "rules.json", "--policy", "enforce", "--policy", "advisory"] },
        { "review", ["review", "first.md", "second.md", "--provider", "openai", "--model", "test"] },
        { "review", ["review", "first.md", "--provider", "openai", "--provider", "gemini", "--model", "test"] },
        { "review", ["review", "first.md", "--provider", "openai", "--model", "test", "--overwrite"] },
        { "draft", ["draft", "--title", "First", "--context", "Context", "--title", "Second"] },
        { "draft", ["draft", "first", "second", "--title", "Title", "--context", "Context"] },
        { "draft", ["draft", "--title", "Title", "--context", "Context", "--dry-run", "--preview"] },
    };

    [Theory]
    [MemberData(nameof(InvalidCommandArguments))]
    public void InvalidCommandShapesPreserveUsageExitAndExactDiagnostic(
        string command,
        string[] arguments)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = RunInIsolatedDirectory(arguments, output, error);

        Assert.Equal(ExitCodes.UsageError, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Equal(
            $"Invalid arguments for '{command}'.{Environment.NewLine}"
            + $"Run 'adr-guard {command} --help' for usage.{Environment.NewLine}",
            error.ToString());
    }

    [Theory]
    [InlineData("baseline")]
    [InlineData("relationships")]
    [InlineData("impact")]
    [InlineData("drift")]
    public void CommandSpecificHelpBypassesRequiredArguments(string command)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = RunInIsolatedDirectory([command, "--help"], output, error);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.StartsWith($"Usage:\n  adr-guard {command} ", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void DraftTemplateValidationPrecedesProviderRequirements()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = RunInIsolatedDirectory(
            ["draft", "--title", "Title", "--context", "Context", "--template", "unknown"],
            output,
            error);

        Assert.Equal(ExitCodes.UsageError, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Equal(
            $"Unknown template 'unknown'. Use minimal or extended.{Environment.NewLine}"
            + $"Run 'adr-guard draft --help' for usage.{Environment.NewLine}",
            error.ToString());
    }

    [Fact]
    public void ReviewEnforcementRequirementsPrecedeTargetAccess()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = RunInIsolatedDirectory(
            [
                "review",
                "missing.md",
                "--provider",
                "openai",
                "--model",
                "test",
                "--policy",
                "enforce",
            ],
            output,
            error);

        Assert.Equal(ExitCodes.UsageError, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Equal(
            "'review --policy enforce' requires --policy-file with explicit deterministic rules."
            + Environment.NewLine
            + $"Run 'adr-guard review --help' for usage.{Environment.NewLine}",
            error.ToString());
    }

    private static int RunInIsolatedDirectory(
        IReadOnlyList<string> arguments,
        TextWriter output,
        TextWriter error)
    {
        var directory = Directory.CreateTempSubdirectory("adr-guard-cli-contract-");

        try
        {
            return CliApplication.Run(
                arguments,
                output,
                error,
                invocationDirectory: directory.FullName);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
