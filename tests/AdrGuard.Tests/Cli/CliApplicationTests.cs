using AdrGuard.Cli;
using Xunit;

namespace AdrGuard.Tests.Cli;

public sealed class CliApplicationTests
{
    [Fact]
    public void Run_WithNoArguments_WritesHelpAndReturnsSuccess()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = CliApplication.Run([], output, error);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("Usage:", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Theory]
    [InlineData("-h")]
    [InlineData("--help")]
    public void Run_WithHelpOption_WritesHelpAndReturnsSuccess(string option)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = CliApplication.Run([option], output, error);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("ADR Guard", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void Run_WithVersionOption_WritesVersionAndReturnsSuccess()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = CliApplication.Run(["--version"], output, error);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.False(string.IsNullOrWhiteSpace(output.ToString()));
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void Run_WithUnknownArgument_WritesErrorAndReturnsUsageError()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = CliApplication.Run(["check"], output, error);

        Assert.Equal(ExitCodes.UsageError, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("Unknown argument or command", error.ToString(), StringComparison.Ordinal);
    }
}
