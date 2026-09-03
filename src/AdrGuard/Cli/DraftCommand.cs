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
        bool includeExistingAdrs,
        IAdrGenerationProvider? provider,
        TextWriter output,
        TextWriter error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(cultureName);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (title.Contains('\r')
            || title.Contains('\n'))
        {
            error.WriteLine("ADR title must be a single line.");
            return ExitCodes.UsageError;
        }

        if (!TryNormalizeCultureName(cultureName, out var normalizedCultureName))
        {
            error.WriteLine(
                $"Invalid culture '{cultureName}'. "
                + "Use a known .NET globalization culture name such as 'en-US' or 'pt-BR'.");
            return ExitCodes.UsageError;
        }

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
            if (includeExistingAdrs)
            {
                output.WriteLine(
                    $"Existing ADR context enabled: parsed ADR data from '{Path.GetFullPath(directoryPath)}' will be sent to the configured provider.");
            }

            var service = new AdrGenerationService(provider);
            var result = service
                .GenerateAsync(
                    directoryPath,
                    title,
                    context,
                    normalizedCultureName,
                    includeExistingAdrs,
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            if (!result.ValidationResult.IsValid)
            {
                ValidationOutput.WriteIssues(result.ValidationResult, error);
                return ExitCodes.ValidationFailed;
            }

            output.WriteLine($"ADR draft written: {result.FilePath}");
            return ExitCodes.Success;
        }
        catch (IOException exception)
        {
            return WriteOperationalError(exception, error);
        }
        catch (UnauthorizedAccessException exception)
        {
            return WriteOperationalError(exception, error);
        }
        catch (InvalidOperationException exception)
        {
            return WriteOperationalError(exception, error);
        }
        catch (OperationCanceledException exception)
        {
            return WriteOperationalError(exception, error);
        }
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
