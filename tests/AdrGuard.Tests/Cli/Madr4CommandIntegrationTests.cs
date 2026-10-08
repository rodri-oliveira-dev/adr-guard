using AdrGuard.Cli;
using Xunit;

namespace AdrGuard.Tests.Cli;

public sealed class Madr4CommandIntegrationTests
{
    [Fact]
    public void CheckAndIndexSupportExplicitMadr4WithoutChangingCanonicalDefault()
    {
        var root = Path.Combine(Path.GetTempPath(), $"adr-guard-madr-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            File.WriteAllText(
                Path.Combine(root, "0001-use-postgresql.md"),
                """
                # Use PostgreSQL
                ## Context and Problem Statement
                We need durable storage.
                ## Considered Options
                * PostgreSQL
                ## Decision Outcome
                Chosen option: "PostgreSQL", because it meets the requirements.
                """);

            Assert.Equal(ExitCodes.ValidationFailed, Run(["check", root]).ExitCode);
            Assert.Equal(ExitCodes.Success, Run(["check", root, "--adr-format", "madr-4"]).ExitCode);
            Assert.Equal(ExitCodes.Success, Run(["index", root, "--adr-format", "madr-4"]).ExitCode);
            Assert.True(File.Exists(Path.Combine(root, "README.md")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static CommandResult Run(string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        return new CommandResult(
            CliApplication.Run(args, output, error),
            output.ToString(),
            error.ToString());
    }

    private sealed record CommandResult(int ExitCode, string Output, string Error);
}
