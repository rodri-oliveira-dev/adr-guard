using AdrGuard.Diagnostics;

namespace AdrGuard.Cli;

internal static class DoctorCommand
{
    internal const string HelpText = """
        Usage:
          adr-guard doctor [repository]

        Diagnose the selected local repository without changing it.
        The repository defaults to the invocation directory. Doctor checks the CLI identity,
        repository marker, strict .adrguard.yml configuration, effective ADR directory,
        current .NET runtime, and optional local Git executable. It does not access the network,
        install tools, inspect provider credentials, read ADR contents, or apply fixes.

        Exit codes:
          0  Diagnostics completed without errors; warnings may be present
          2  Invalid command-line usage
          3  One or more diagnostic errors, or an incomplete report
        """;

    internal static int Run(
        IReadOnlyList<string> args,
        string cliVersion,
        string invocationDirectory,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken,
        Func<string, CancellationToken, string>? gitVersionProbe = null)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (args.Count == 2 && args[1] is "-h" or "--help")
        {
            output.WriteLine(HelpText);
            return ExitCodes.Success;
        }

        if (!TryParse(args, invocationDirectory, out var repository))
        {
            error.WriteLine("Invalid arguments for 'doctor'.");
            error.WriteLine("Run 'adr-guard doctor --help' for usage.");
            return ExitCodes.UsageError;
        }

        try
        {
            var report = DoctorDiagnosticService.Run(
                repository,
                cliVersion,
                gitVersionProbe,
                cancellationToken);
            Render(report, output);
            return report.HasErrors ? ExitCodes.OperationalError : ExitCodes.Success;
        }
        catch (OperationCanceledException)
        {
            error.WriteLine("Unable to complete doctor diagnostics: operation was canceled.");
            return ExitCodes.OperationalError;
        }
        catch (Exception exception) when (
            exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or ArgumentException
            or System.Security.SecurityException)
        {
            error.WriteLine("Unable to complete doctor diagnostics safely.");
            return ExitCodes.OperationalError;
        }
    }

    private static bool TryParse(
        IReadOnlyList<string> args,
        string invocationDirectory,
        out string repository)
    {
        repository = invocationDirectory;
        if (args.Count == 1)
        {
            return true;
        }

        if (args.Count != 2
            || string.IsNullOrWhiteSpace(args[1])
            || args[1].StartsWith('-'))
        {
            return false;
        }

        repository = args[1];
        return true;
    }

    private static void Render(DoctorReport report, TextWriter output)
    {
        var builder = new System.Text.StringBuilder()
            .AppendLine("ADR Guard doctor");

        foreach (var diagnostic in report.Diagnostics)
        {
            builder.Append(diagnostic.Severity.ToString().ToUpperInvariant())
                .Append(' ')
                .Append(diagnostic.Check)
                .Append(": ")
                .AppendLine(diagnostic.Message);
        }

        var ok = report.Diagnostics.Count(diagnostic => diagnostic.Severity == DoctorDiagnosticSeverity.Ok);
        var warning = report.Diagnostics.Count(diagnostic => diagnostic.Severity == DoctorDiagnosticSeverity.Warning);
        var error = report.Diagnostics.Count(diagnostic => diagnostic.Severity == DoctorDiagnosticSeverity.Error);
        builder.Append("Summary: ")
            .Append(ok).Append(" OK, ")
            .Append(warning).Append(" WARNING, ")
            .Append(error).AppendLine(" ERROR");

        output.Write(builder.ToString());
    }
}
