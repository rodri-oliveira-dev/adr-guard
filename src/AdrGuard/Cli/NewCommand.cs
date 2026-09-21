using AdrGuard.Generation;

namespace AdrGuard.Cli;

internal static class NewCommand
{
    internal const string HelpText = """
        Usage:
          adr-guard new [adr-directory] --title <title> [--template minimal|extended] [--template-file <path>] [--culture en-US|pt-BR] [--dry-run|--preview]

        Create a Proposed ADR offline. The directory defaults to the current directory.
        The default template is minimal and the default culture is en-US.
        A custom --template-file is resolved relative to the invocation working directory.
        --template and --template-file are mutually exclusive.
        Custom templates must be valid UTF-8 Markdown, at most 65536 bytes.
        Dry-run/preview validates and prints the prospective filename and exact content
        without writing files or updating the index. Templates contain author-editable
        guidance and do not represent architect-approved decisions.
        """;

    internal static int Run(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (args.Count == 2 && args[1] is "--help" or "-h")
        {
            output.WriteLine(HelpText);
            return ExitCodes.Success;
        }

        if (!TryParse(
                args,
                out var directoryPath,
                out var title,
                out var templateName,
                out var templateFilePath,
                out var cultureName,
                out var dryRun))
        {
            return UsageError(
                "Invalid arguments for 'new'. A non-empty --title is required.",
                error);
        }

        if (templateName is not null && templateFilePath is not null)
        {
            return UsageError(
                "--template and --template-file are mutually exclusive.",
                error);
        }

        if (!string.Equals(cultureName, "en-US", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(cultureName, "pt-BR", StringComparison.OrdinalIgnoreCase))
        {
            return UsageError(
                $"Unsupported template culture '{cultureName}'. Use en-US or pt-BR.",
                error);
        }

        cultureName = string.Equals(
            cultureName,
            "pt-BR",
            StringComparison.OrdinalIgnoreCase)
            ? "pt-BR"
            : "en-US";

        if (title.IndexOfAny(['\r', '\n']) >= 0
            || string.IsNullOrWhiteSpace(AdrSlug.Create(title)))
        {
            return UsageError(
                "ADR title must be one line and contain at least one ASCII letter or digit.",
                error);
        }

        if (templateName is not null
            && !AdrBuiltInTemplates.Names.Contains(
                templateName,
                StringComparer.Ordinal))
        {
            return UsageError(
                $"Unknown template '{templateName}'. Use minimal or extended.",
                error);
        }

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
                    cancellationToken)
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
        catch (IOException exception)
        {
            return OperationalError(exception, error);
        }
        catch (UnauthorizedAccessException exception)
        {
            return OperationalError(exception, error);
        }
        catch (InvalidOperationException exception)
        {
            return OperationalError(exception, error);
        }
        catch (OperationCanceledException exception)
        {
            return OperationalError(exception, error);
        }
    }

    private static bool TryParse(
        IReadOnlyList<string> args,
        out string directoryPath,
        out string title,
        out string? templateName,
        out string? templateFilePath,
        out string cultureName,
        out bool dryRun)
    {
        directoryPath = ".";
        title = string.Empty;
        templateName = null;
        templateFilePath = null;
        cultureName = "en-US";
        dryRun = false;

        var directoryAssigned = false;
        var titleAssigned = false;
        var templateAssigned = false;
        var templateFileAssigned = false;
        var cultureAssigned = false;
        var dryRunAssigned = false;

        for (var index = 1; index < args.Count; index++)
        {
            var argument = args[index];

            if (argument is "--dry-run" or "--preview")
            {
                if (dryRunAssigned)
                {
                    return false;
                }

                dryRun = true;
                dryRunAssigned = true;
                continue;
            }

            if (argument is "--title" or "--template" or "--template-file" or "--culture")
            {
                if (index + 1 >= args.Count)
                {
                    return false;
                }

                var value = args[++index];
                if (string.IsNullOrWhiteSpace(value)
                    || value.StartsWith('-'))
                {
                    return false;
                }

                switch (argument)
                {
                    case "--title":
                        if (titleAssigned)
                        {
                            return false;
                        }

                        title = value;
                        titleAssigned = true;
                        break;
                    case "--template":
                        if (templateAssigned)
                        {
                            return false;
                        }

                        templateName = value;
                        templateAssigned = true;
                        break;
                    case "--template-file":
                        if (templateFileAssigned)
                        {
                            return false;
                        }

                        templateFilePath = value;
                        templateFileAssigned = true;
                        break;
                    case "--culture":
                        if (cultureAssigned)
                        {
                            return false;
                        }

                        cultureName = value;
                        cultureAssigned = true;
                        break;
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

        return titleAssigned;
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
