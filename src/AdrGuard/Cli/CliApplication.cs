using System.Reflection;

namespace AdrGuard.Cli;

internal static class CliApplication
{
    private const string HelpText = """
        ADR Guard

        Validate and maintain Architecture Decision Records from the command line.

        Usage:
          adr-guard [options]

        Options:
          -h, --help    Show command-line help.
          --version     Show the application version.
        """;

    internal static int Run(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (args.Count == 0 || IsHelpRequest(args))
        {
            output.WriteLine(HelpText);
            return ExitCodes.Success;
        }

        if (IsVersionRequest(args))
        {
            output.WriteLine(GetVersion());
            return ExitCodes.Success;
        }

        error.WriteLine($"Unknown argument or command: '{string.Join(' ', args)}'.");
        error.WriteLine("Run 'adr-guard --help' for usage.");

        return ExitCodes.UsageError;
    }

    private static bool IsHelpRequest(IReadOnlyList<string> args) =>
        args.Count == 1 && args[0] is "-h" or "--help";

    private static bool IsVersionRequest(IReadOnlyList<string> args) =>
        args.Count == 1 && args[0] == "--version";

    private static string GetVersion()
    {
        var assembly = typeof(CliApplication).Assembly;

        return assembly
                   .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                   ?.InformationalVersion
               ?? assembly.GetName().Version?.ToString()
               ?? "unknown";
    }
}
