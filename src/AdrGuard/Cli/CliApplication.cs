using AdrGuard.Generation;
using AdrGuard.Generation.Providers;
using System.Reflection;

namespace AdrGuard.Cli;

internal static class CliApplication
{
    private const string DefaultDraftCultureName = "en-US";

    private const string HelpText = """
        ADR Guard

        Validate and maintain Architecture Decision Records from the command line.

        Usage:
          adr-guard check [directory]
          adr-guard index [directory] [--output <file>]
          adr-guard draft [directory] --title <title> --context <context> --provider <provider> --model <model> [--culture <name>] [--endpoint <uri>] [--context-file <path>]... [--include-existing-adrs] [--dry-run|--preview]
          adr-guard [options]

        Commands:
          check    Validate ADR files. Defaults to the current directory.
          index    Validate ADR files and generate an index. Defaults to README.md.
          draft    Generate a Proposed ADR draft through a configured AI provider.

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
        Relative --output paths are resolved from the current working directory.
        """;

    private const string DraftHelpText = """
        Usage:
          adr-guard draft [directory] --title <title> --context <context> --provider <provider> --model <model> [--culture <name>] [--endpoint <uri>] [--context-file <path>]... [--include-existing-adrs] [--dry-run|--preview]

        Generate a Proposed ADR draft through a configured AI provider.
        The directory defaults to the current directory.

        Required provider options:
          --provider <provider>   openai | anthropic | gemini | openai-compatible
          --model <model>         Provider model identifier. ADR Guard does not choose a default model.

        Optional options:
          --culture <name>        .NET globalization culture name such as en-US or pt-BR.
                                  Defaults to en-US.
          --endpoint <uri>        Required only for openai-compatible; rejected for official providers.
          --context-file <path>    Add an explicit .md or .txt context file. May be repeated.
                                  Relative paths are resolved from the current working directory.
          --include-existing-adrs   Opt in to sending parsed existing ADR context to the provider.
                                  ADRs are ordered deterministically and bounded to 12000 characters.
          --dry-run, --preview      Generate and validate the ADR without writing a file.

        Context limits:
          --context               20000 characters maximum.
          each --context-file     50000 characters maximum.
          all --context-file      100000 characters maximum in aggregate.
          composed context        120000 characters maximum.
          existing ADR context    12000 characters maximum.

        Authentication is read only from environment variables:
          openai                  OPENAI_API_KEY
          anthropic               ANTHROPIC_API_KEY
          gemini                  GEMINI_API_KEY
          openai-compatible       ADR_GUARD_OPENAI_COMPATIBLE_API_KEY (optional)

        Oversized context is rejected before provider invocation and is never silently truncated.
        ADR structural headings and the Proposed status remain canonical.
        Generated content is validated before a new ADR file is written.
        Ctrl+C cancels the draft workflow through context loading, provider calls, validation, and persistence.
        Persisted drafts are written to a temporary file and atomically promoted without overwrite.
        Dry-run/preview uses the same deterministic ID and filename calculation,
        validates the generated ADR, prints it, and does not write any file.
        """;

    internal static int Run(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        IAdrGenerationProvider? generationProvider = null,
        Func<HttpClient>? httpClientFactory = null,
        Func<string, string?>? environmentVariableReader = null) =>
        RunCore(
            args,
            output,
            error,
            generationProvider,
            httpClientFactory,
            environmentVariableReader,
            default);

    internal static int Run(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken,
        IAdrGenerationProvider? generationProvider = null,
        Func<HttpClient>? httpClientFactory = null,
        Func<string, string?>? environmentVariableReader = null) =>
        RunCore(
            args,
            output,
            error,
            generationProvider,
            httpClientFactory,
            environmentVariableReader,
            cancellationToken);

    private static int RunCore(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        IAdrGenerationProvider? generationProvider,
        Func<HttpClient>? httpClientFactory,
        Func<string, string?>? environmentVariableReader,
        CancellationToken cancellationToken)
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
            "draft" => RunDraft(
                args,
                output,
                error,
                generationProvider,
                httpClientFactory,
                environmentVariableReader,
                cancellationToken),
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

        if (directoryPath.StartsWith('-'))
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

        if (!TryParseIndexArguments(
                args,
                out var directoryPath,
                out var outputPath))
        {
            return WriteCommandUsageError("index", error);
        }

        return IndexCommand.Run(
            directoryPath,
            outputPath,
            output,
            error);
    }

    private static int RunDraft(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        IAdrGenerationProvider? injectedProvider,
        Func<HttpClient>? httpClientFactory,
        Func<string, string?>? environmentVariableReader,
        CancellationToken cancellationToken)
    {
        if (args.Count == 2 && IsHelpOption(args[1]))
        {
            output.WriteLine(DraftHelpText);
            return ExitCodes.Success;
        }

        if (!TryParseDraftArguments(
                args,
                out var draftArguments))
        {
            return WriteCommandUsageError(
                "draft",
                error);
        }

        if (injectedProvider is not null)
        {
            WriteProviderSelection(draftArguments, output);

            return RunDraftCommand(
                draftArguments,
                injectedProvider,
                output,
                error,
                cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(
                draftArguments.ProviderName)
            || string.IsNullOrWhiteSpace(
                draftArguments.Model))
        {
            error.WriteLine(
                "'draft' requires both --provider and --model.");
            error.WriteLine(
                "Run 'adr-guard draft --help' for usage.");
            return ExitCodes.UsageError;
        }

        WriteProviderSelection(draftArguments, output);

        try
        {
            using var httpClient =
                httpClientFactory?.Invoke()
                ?? new HttpClient();

            var provider =
                AdrGenerationProviderFactory.Create(
                    draftArguments.ProviderName,
                    draftArguments.Model,
                    draftArguments.Endpoint,
                    httpClient,
                    environmentVariableReader);

            return RunDraftCommand(
                draftArguments,
                provider,
                output,
                error,
                cancellationToken);
        }
        catch (ArgumentException exception)
        {
            error.WriteLine(exception.Message);
            error.WriteLine(
                "Run 'adr-guard draft --help' for usage.");
            return ExitCodes.UsageError;
        }
        catch (InvalidOperationException exception)
        {
            error.WriteLine(exception.Message);
            return ExitCodes.OperationalError;
        }
    }

    private static int RunDraftCommand(
        DraftArguments arguments,
        IAdrGenerationProvider provider,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken) =>
        DraftCommand.Run(
            arguments.DirectoryPath,
            arguments.Title,
            arguments.Context,
            arguments.CultureName,
            arguments.ContextFilePaths,
            arguments.IncludeExistingAdrs,
            arguments.DryRun,
            provider,
            output,
            error,
            cancellationToken);

    private static void WriteProviderSelection(
        DraftArguments arguments,
        TextWriter output)
    {
        if (!string.IsNullOrWhiteSpace(arguments.ProviderName))
        {
            output.WriteLine($"AI provider: {arguments.ProviderName}");
        }

        if (!string.IsNullOrWhiteSpace(arguments.Model))
        {
            output.WriteLine($"AI model: {arguments.Model}");
        }
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
                if (outputPath is not null
                    || index + 1 >= args.Count)
                {
                    return false;
                }

                outputPath = args[++index];
                if (string.IsNullOrWhiteSpace(outputPath)
                    || outputPath.StartsWith('-'))
                {
                    return false;
                }

                continue;
            }

            if (argument.StartsWith('-')
                || directoryAssigned)
            {
                return false;
            }

            directoryPath = argument;
            directoryAssigned = true;
        }

        return true;
    }

    private static bool TryParseDraftArguments(
        IReadOnlyList<string> args,
        out DraftArguments draftArguments)
    {
        var directoryPath = ".";
        var title = string.Empty;
        var context = string.Empty;
        var cultureName = DefaultDraftCultureName;
        string? providerName = null;
        string? model = null;
        string? endpoint = null;
        var contextFilePaths = new List<string>();
        var includeExistingAdrs = false;
        var dryRun = false;

        var directoryAssigned = false;
        var titleAssigned = false;
        var contextAssigned = false;
        var cultureAssigned = false;
        var providerAssigned = false;
        var modelAssigned = false;
        var endpointAssigned = false;
        var includeExistingAdrsAssigned = false;
        var dryRunAssigned = false;

        for (var index = 1; index < args.Count; index++)
        {
            var argument = args[index];

            if (argument == "--include-existing-adrs")
            {
                if (includeExistingAdrsAssigned)
                {
                    draftArguments = DraftArguments.Empty;
                    return false;
                }

                includeExistingAdrs = true;
                includeExistingAdrsAssigned = true;
                continue;
            }

            if (argument is "--dry-run" or "--preview")
            {
                if (dryRunAssigned)
                {
                    draftArguments = DraftArguments.Empty;
                    return false;
                }

                dryRun = true;
                dryRunAssigned = true;
                continue;
            }

            if (argument is "--title"
                or "--context"
                or "--culture"
                or "--provider"
                or "--model"
                or "--endpoint"
                or "--context-file")
            {
                if (index + 1 >= args.Count)
                {
                    draftArguments = DraftArguments.Empty;
                    return false;
                }

                var value = args[++index];
                if (string.IsNullOrWhiteSpace(value)
                    || value.StartsWith('-'))
                {
                    draftArguments = DraftArguments.Empty;
                    return false;
                }

                switch (argument)
                {
                    case "--title":
                        if (titleAssigned)
                        {
                            draftArguments = DraftArguments.Empty;
                            return false;
                        }

                        title = value;
                        titleAssigned = true;
                        break;

                    case "--context":
                        if (contextAssigned)
                        {
                            draftArguments = DraftArguments.Empty;
                            return false;
                        }

                        context = value;
                        contextAssigned = true;
                        break;

                    case "--culture":
                        if (cultureAssigned)
                        {
                            draftArguments = DraftArguments.Empty;
                            return false;
                        }

                        cultureName = value;
                        cultureAssigned = true;
                        break;

                    case "--provider":
                        if (providerAssigned)
                        {
                            draftArguments = DraftArguments.Empty;
                            return false;
                        }

                        providerName = value;
                        providerAssigned = true;
                        break;

                    case "--model":
                        if (modelAssigned)
                        {
                            draftArguments = DraftArguments.Empty;
                            return false;
                        }

                        model = value;
                        modelAssigned = true;
                        break;

                    case "--endpoint":
                        if (endpointAssigned)
                        {
                            draftArguments = DraftArguments.Empty;
                            return false;
                        }

                        endpoint = value;
                        endpointAssigned = true;
                        break;

                    case "--context-file":
                        contextFilePaths.Add(value);
                        break;
                }

                continue;
            }

            if (argument.StartsWith('-')
                || directoryAssigned)
            {
                draftArguments = DraftArguments.Empty;
                return false;
            }

            directoryPath = argument;
            directoryAssigned = true;
        }

        draftArguments = new DraftArguments(
            directoryPath,
            title,
            context,
            cultureName,
            providerName,
            model,
            endpoint,
            contextFilePaths,
            includeExistingAdrs,
            dryRun);

        return titleAssigned && contextAssigned;
    }

    private static int WriteUsageError(
        IReadOnlyList<string> args,
        TextWriter error)
    {
        error.WriteLine(
            $"Unknown argument or command: '{string.Join(' ', args)}'.");
        error.WriteLine(
            "Run 'adr-guard --help' for usage.");

        return ExitCodes.UsageError;
    }

    private static int WriteCommandUsageError(
        string command,
        TextWriter error)
    {
        error.WriteLine(
            $"Invalid arguments for '{command}'.");
        error.WriteLine(
            $"Run 'adr-guard {command} --help' for usage.");

        return ExitCodes.UsageError;
    }

    private static bool IsHelpRequest(
        IReadOnlyList<string> args) =>
        args.Count == 1
        && IsHelpOption(args[0]);

    private static bool IsHelpOption(
        string value) =>
        value is "-h" or "--help";

    private static bool IsVersionRequest(
        IReadOnlyList<string> args) =>
        args.Count == 1
        && args[0] == "--version";

    private static string GetVersion()
    {
        var assembly = typeof(CliApplication).Assembly;

        return assembly
                   .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                   ?.InformationalVersion
               ?? assembly.GetName().Version?.ToString()
               ?? "unknown";
    }

    private sealed record DraftArguments(
        string DirectoryPath,
        string Title,
        string Context,
        string CultureName,
        string? ProviderName,
        string? Model,
        string? Endpoint,
        IReadOnlyList<string> ContextFilePaths,
        bool IncludeExistingAdrs,
        bool DryRun)
    {
        internal static DraftArguments Empty { get; } =
            new(
                ".",
                string.Empty,
                string.Empty,
                DefaultDraftCultureName,
                null,
                null,
                null,
                [],
                false,
                false);
    }
}
