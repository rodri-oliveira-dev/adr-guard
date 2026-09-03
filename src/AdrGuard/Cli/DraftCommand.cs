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
                + "Use a valid .NET globalization culture name such as 'en-US' or 'pt-BR'.");
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
            var service = new AdrGenerationService(provider);
            var result = service
                .GenerateAsync(
                    directoryPath,
                    title,
                    context,
                    normalizedCultureName,
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
        try
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);

            if (string.IsNullOrWhiteSpace(culture.Name))
            {
                normalizedCultureName = string.Empty;
                return false;
            }

            normalizedCultureName = culture.Name;
            return true;
        }
        catch (CultureNotFoundException)
        {
            normalizedCultureName = string.Empty;
            return false;
        }
    }

    private static int WriteOperationalError(
        Exception exception,
        TextWriter error)
    {
        error.WriteLine($"Unable to generate ADR draft: {exception.Message}");
        return ExitCodes.OperationalError;
    }
}
