using System.Reflection;

namespace AdrGuard.Cli;

internal static class CliApplication
{
    private const string HelpText = """
        ADR Guard

        Validate and maintain Architecture Decision Records from the command line.

        Usage:
          adr-guard check [directory]
          adr-guard index [directory] [--output <file>]
          adr-guard [options]

        Commands:
          check    Validate ADR files. Defaults to the current directory.
          index    Validate ADR files and generate an index. Defaults to README.md.

        Options:
          -h, --help    Show command-line help.
          --version     Show the application version.

        Exit codes:
          0  Success
          1  ADR validation failed
          2  Invalid command-line usage
          3  Operational error
        """;

    private const string CheckHelpText = """
        Usage:
          adr-guard check [directory]

        Validate ADR files recursively. The directory defaults to the current directory.
        """;

    private const string IndexHelpText = """
        Usage:
          adr-guard index [directory] [--output <file>]

        Validate ADR files and generate a Markdown index.
        The directory defaults to the current directory.
        The output defaults to README.md inside the ADR directory.
        Relative --output paths are resolved from the ADR directory.
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

        return args[0] switch
        {
            "check" => RunCheck(args, output, error),
            "index" => RunIndex(args, output, error),
            _ => WriteUsageError(args, error),
        };
    }

    private static int RunCheck(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error)
    {
        if (args.Count == 2 && IsHelpOption(args[1]))
        {
            output.WriteLine(CheckHelpText);
            return ExitCodes.Success;
        }

        if (args.Count > 2)
        {
            return WriteCommandUsageError("check", error);
        }

        var directoryPath = args.Count == 2 ? args[1] : ".";

        if (directoryPath.StartsWith('-', StringComparison.Ordinal))
        {
            return WriteCommandUsageError("check", error);
        }

        return CheckCommand.Run(directoryPath, output, error);
    }

    private static int RunIndex(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error)
    {
        if (args.Count == 2 && IsHelpOption(args[1]))
        {
            output.WriteLine(IndexHelpText);
            return ExitCodes.Success;
        }

        if (!TryParseIndexArguments(args, out var directoryPath, out var outputPath))
        {
            return WriteCommandUsageError("index", error);
        }

        return IndexCommand.Run(directoryPath, outputPath, output, error);
    }

    private static bool TryParseIndexArguments(
        IReadOnlyList<string> args,
        out string directoryPath,
        out string? outputPath)
    {
        directoryPath = ".";
        outputPath = null;
        var directoryAssigned = false;

        for (var index = 1; index < args.Count; index++)
        {
            var argument = args[index];

            if (argument == "--output")
            {
                if (outputPath is not null || index + 1 >= args.Count)
                {
                    return false;
                }

                outputPath = args[++index];
                if (string.IsNullOrWhiteSpace(outputPath)
                    || outputPath.StartsWith('-', StringComparison.Ordinal))
                {
                    return false;
                }

                continue;
            }

            if (argument.StartsWith('-', StringComparison.Ordinal)
                || directoryAssigned)
            {
                return false;
            }

            directoryPath = argument;
            directoryAssigned = true;
        }

        return true;
    }

    private static int WriteUsageError(
        IReadOnlyList<string> args,
        TextWriter error)
    {
        error.WriteLine($"Unknown argument or command: '{string.Join(' ', args)}'.");
        error.WriteLine("Run 'adr-guard --help' for usage.");

        return ExitCodes.UsageError;
    }

    private static int WriteCommandUsageError(
        string command,
        TextWriter error)
    {
        error.WriteLine($"Invalid arguments for '{command}'.");
        error.WriteLine($"Run 'adr-guard {command} --help' for usage.");

        return ExitCodes.UsageError;
    }

    private static bool IsHelpRequest(IReadOnlyList<string> args) =>
        args.Count == 1 && IsHelpOption(args[0]);

    private static bool IsHelpOption(string value) =>
        value is "-h" or "--help";

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
