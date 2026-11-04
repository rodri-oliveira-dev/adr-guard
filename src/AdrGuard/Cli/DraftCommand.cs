using AdrGuard.Generation;
using System.Globalization;

namespace AdrGuard.Cli;

internal static class DraftCommand
{
    internal static int Run(
        string directoryPath,
        string title,
        string context,
        string cultureName,
        IReadOnlyList<string> contextFilePaths,
        bool includeExistingAdrs,
        bool dryRun,
        IAdrGenerationProvider? provider,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default) =>
        Run(
            directoryPath,
            title,
            context,
            cultureName,
            contextFilePaths,
            includeExistingAdrs,
            dryRun,
            provider,
            output,
            error,
            template: null,
            cancellationToken);

    internal static int Run(
        string directoryPath,
        string title,
        string context,
        string cultureName,
        IReadOnlyList<string> contextFilePaths,
        bool includeExistingAdrs,
        bool dryRun,
        IAdrGenerationProvider? provider,
        TextWriter output,
        TextWriter error,
        AdrTemplateDefinition? template,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(cultureName);
        ArgumentNullException.ThrowIfNull(contextFilePaths);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        var validationError = ValidateInputs(title, cultureName, template, out var normalizedCultureName);
        if (validationError is not null) { error.WriteLine(validationError); return ExitCodes.UsageError; }

        if (!Directory.Exists(directoryPath))
        {
            error.WriteLine($"ADR directory does not exist: '{directoryPath}'.");
            return ExitCodes.OperationalError;
        }

        if (provider is null)
        {
            error.WriteLine(
                "No ADR generation provider is configured. "
                + "Install or configure a supported provider integration before using 'draft'.");
            return ExitCodes.OperationalError;
        }

        try
        {
            WriteContextSummary(output, directoryPath, contextFilePaths, includeExistingAdrs, dryRun);

            var service = new AdrGenerationService(provider);
            var result = service
                .GenerateAsync(
                    directoryPath,
                    title,
                    context,
                    normalizedCultureName,
                    contextFilePaths,
                    includeExistingAdrs,
                    dryRun,
                    template,
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
                output.WriteLine($"ADR draft preview path: {result.FilePath}");
                output.WriteLine();
                output.Write(result.Content);
                return ExitCodes.Success;
            }

            output.WriteLine($"ADR draft written: {result.FilePath}");
            return ExitCodes.Success;
        }
        catch (Exception exception) when (exception is IOException
            or ArgumentException
            or UnauthorizedAccessException
            or InvalidOperationException
            or OperationCanceledException)
        {
            return WriteOperationalError(exception, error);
        }
    }

    private static string? ValidateInputs(
        string title,
        string cultureName,
        AdrTemplateDefinition? template,
        out string normalizedCultureName)
    {
        normalizedCultureName = string.Empty;
        if (title.Contains('\r') || title.Contains('\n')) return "ADR title must be a single line.";
        if (!TryNormalizeCultureName(cultureName, out normalizedCultureName))
            return $"Invalid culture '{cultureName}'. Use a known .NET globalization culture name such as 'en-US' or 'pt-BR'.";
        if (template is not null
            && !string.Equals(template.CultureName, normalizedCultureName, StringComparison.Ordinal))
            return "Selected template culture must match --culture (en-US or pt-BR).";
        return null;
    }

    private static void WriteContextSummary(
        TextWriter output,
        string directoryPath,
        IReadOnlyList<string> contextFilePaths,
        bool includeExistingAdrs,
        bool dryRun)
    {
        if (dryRun)
            output.WriteLine("Dry-run enabled: the generated ADR will be validated and printed without creating a file.");
        if (contextFilePaths.Count > 0)
        {
            output.WriteLine("Explicit context files that will be sent to the configured provider:");
            foreach (var contextFilePath in contextFilePaths)
                output.WriteLine($"- {Path.GetFullPath(contextFilePath)}");
        }
        if (includeExistingAdrs)
            output.WriteLine(
                $"Existing ADR context enabled: parsed ADR data from '{Path.GetFullPath(directoryPath)}' will be sent to the configured provider.");
    }

    private static bool TryNormalizeCultureName(
        string cultureName,
        out string normalizedCultureName)
    {
        var culture = CultureInfo
            .GetCultures(CultureTypes.AllCultures)
            .FirstOrDefault(candidate =>
                !string.IsNullOrWhiteSpace(candidate.Name)
                && string.Equals(
                    candidate.Name,
                    cultureName,
                    StringComparison.OrdinalIgnoreCase));

        if (culture is null)
        {
            normalizedCultureName = string.Empty;
            return false;
        }

        normalizedCultureName = CultureInfo.GetCultureInfo(culture.Name).Name;
        return true;
    }

    private static int WriteOperationalError(
        Exception exception,
        TextWriter error)
    {
        error.WriteLine($"Unable to generate ADR draft: {exception.Message}");
        return ExitCodes.OperationalError;
    }
}
