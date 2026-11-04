using AdrGuard.Generation;

namespace AdrGuard.Cli;

internal static class NewCommand
{
    internal const string HelpText = """
        Usage:
          adr-guard new [adr-directory] --title <title> [--template minimal|extended] [--template-file <path>] [--culture en-US|pt-BR] [--filename-policy <policy>] [--dry-run|--preview]
          adr-guard new [adr-directory] --form [--template minimal|extended] [--template-file <path>] [--culture en-US|pt-BR] [--filename-policy <policy>]

        Create a Proposed ADR offline. The directory defaults to the current directory.
        The default template is minimal and the default culture is en-US.
        Filename policies are canonical, unnumbered, adr-prefix, or numeric:1..9.
        A custom --template-file is resolved relative to the invocation working directory.
        --template and --template-file are mutually exclusive.
        Custom templates must be valid UTF-8 Markdown, at most 65536 bytes.
        --form reads a bounded schema 1.0 JSON request from stdin and writes a
        schema 1.0 JSON response. Supported operations are describe, preview,
        and create. Custom templates are described as incompatible so callers
        can offer the traditional creation flow without substituting templates.
        Dry-run/preview validates and prints the prospective filename and exact content
        without writing files or updating the index. Templates contain author-editable
        guidance and do not represent architect-approved decisions.
        """;

    internal static int Run(
        IReadOnlyList<string> args,
        TextReader input,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (args.Count == 2 && args[1] is "--help" or "-h")
        {
            output.WriteLine(HelpText);
            return ExitCodes.Success;
        }

        if (args.Contains("--form", StringComparer.Ordinal))
        {
            return NewFormCommand.Run(args, input, output, error, cancellationToken);
        }

        if (!TryParse(args, out var arguments))
        {
            return UsageError(
                "Invalid arguments for 'new'. A non-empty --title is required.",
                error);
        }

        var directoryPath = arguments.DirectoryPath;
        var title = arguments.Title;
        var templateName = arguments.TemplateName;
        var templateFilePath = arguments.TemplateFilePath;
        var cultureName = arguments.CultureName;
        var dryRun = arguments.DryRun;
        var filenamePolicyName = arguments.FilenamePolicy;

        var validationError = ValidateArguments(
            title,
            templateName,
            templateFilePath,
            cultureName,
            out cultureName);
        if (validationError is not null) return UsageError(validationError, error);

        if (!Directory.Exists(directoryPath))
        {
            error.WriteLine($"ADR directory does not exist: '{directoryPath}'.");
            return ExitCodes.OperationalError;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var template = AdrTemplateSelection.Resolve(
                templateName,
                templateFilePath,
                cultureName,
                cancellationToken: cancellationToken);

            var result = new AdrNewService()
                .CreateAsync(
                    directoryPath,
                    title,
                    template,
                    dryRun,
                    cancellationToken,
                    AdrGuard.Validation.AdrFilenamePolicy.Parse(filenamePolicyName))
                .GetAwaiter()
                .GetResult();

            if (!result.ValidationResult.IsValid)
            {
                ValidationOutput.WriteIssues(result.ValidationResult, error);
                return ExitCodes.ValidationFailed;
            }

            if (dryRun)
            {
                output.WriteLine($"ADR preview path: {result.FilePath}");
                output.WriteLine();
                output.Write(result.Content);
                return ExitCodes.Success;
            }

            output.WriteLine($"ADR written: {result.FilePath}");
            return ExitCodes.Success;
        }
        catch (ArgumentException exception)
        {
            return UsageError(exception.Message, error);
        }
        catch (Exception exception) when (exception is InvalidDataException
            or IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or OperationCanceledException)
        {
            return OperationalError(exception, error);
        }
    }

    private static string? ValidateArguments(
        string title,
        string? templateName,
        string? templateFilePath,
        string cultureName,
        out string normalizedCulture)
    {
        normalizedCulture = string.Equals(cultureName, "pt-BR", StringComparison.OrdinalIgnoreCase)
            ? "pt-BR"
            : "en-US";
        if (templateName is not null && templateFilePath is not null)
            return "--template and --template-file are mutually exclusive.";
        if (!string.Equals(cultureName, "en-US", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(cultureName, "pt-BR", StringComparison.OrdinalIgnoreCase))
            return $"Unsupported template culture '{cultureName}'. Use en-US or pt-BR.";
        if (title.IndexOfAny(['\r', '\n']) >= 0 || string.IsNullOrWhiteSpace(AdrSlug.Create(title)))
            return "ADR title must be one line and contain at least one ASCII letter or digit.";
        if (templateName is not null
            && !AdrBuiltInTemplates.Names.Contains(templateName, StringComparer.Ordinal))
            return $"Unknown template '{templateName}'. Use minimal or extended.";
        return null;
    }

    private static bool TryParse(
        IReadOnlyList<string> args,
        out NewArgumentState arguments)
    {
        var state = new NewArgumentState();
        arguments = state;
        return PopulateState(args, state) && state.TitleAssigned;
    }

    private static bool PopulateState(IReadOnlyList<string> args, NewArgumentState state)
    {
        var index = 1;
        while (index < args.Count)
        {
            var argument = args[index++];
            if (!TryApplyArgument(argument, args, ref index, state)) return false;
        }
        return true;
    }

    private static bool TryApplyArgument(
        string argument,
        IReadOnlyList<string> args,
        ref int index,
        NewArgumentState state)
    {
        if (argument is "--dry-run" or "--preview") return state.TrySetDryRun();
        if (argument is not ("--title" or "--template" or "--template-file"
            or "--culture" or "--filename-policy")) return state.TrySetDirectory(argument);
        if (index >= args.Count) return false;
        return state.TrySetValue(argument, args[index++]);
    }

    private sealed class NewArgumentState
    {
        private readonly HashSet<string> assigned = new(StringComparer.Ordinal);
        internal string DirectoryPath { get; private set; } = ".";
        internal string Title { get; private set; } = string.Empty;
        internal string? TemplateName { get; private set; }
        internal string? TemplateFilePath { get; private set; }
        internal string CultureName { get; private set; } = "en-US";
        internal bool DryRun { get; private set; }
        internal string? FilenamePolicy { get; private set; }
        internal bool TitleAssigned => assigned.Contains("--title");

        internal bool TrySetDryRun()
        {
            if (!assigned.Add("dry-run")) return false;
            DryRun = true;
            return true;
        }

        internal bool TrySetValue(string argument, string value)
        {
            if (!assigned.Add(argument) || string.IsNullOrWhiteSpace(value) || value.StartsWith('-'))
                return false;
            switch (argument)
            {
                case "--title": Title = value; break;
                case "--template": TemplateName = value; break;
                case "--template-file": TemplateFilePath = value; break;
                case "--culture": CultureName = value; break;
                case "--filename-policy":
                    try { AdrGuard.Validation.AdrFilenamePolicy.Parse(value); }
                    catch (ArgumentException) { return false; }
                    FilenamePolicy = value;
                    break;
            }
            return true;
        }

        internal bool TrySetDirectory(string argument)
        {
            if (argument.StartsWith('-') || !assigned.Add("directory")) return false;
            DirectoryPath = argument;
            return true;
        }
    }

    private static int UsageError(string message, TextWriter error)
    {
        error.WriteLine(message);
        error.WriteLine("Run 'adr-guard new --help' for usage.");
        return ExitCodes.UsageError;
    }

    private static int OperationalError(Exception exception, TextWriter error)
    {
        error.WriteLine($"Unable to create ADR: {exception.Message}");
        return ExitCodes.OperationalError;
    }
}
