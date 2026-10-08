using AdrGuard.Parsing;
using AdrGuard.Validation;

namespace AdrGuard.Cli;

internal static class CheckCommand
{
    internal static int Run(
        string directoryPath,
        TextWriter output,
        TextWriter error) =>
        Run(directoryPath, CheckOutputFormat.Text, AdrFormat.Canonical, output, error);

    internal static int Run(
        string directoryPath,
        CheckOutputFormat format,
        AdrFormat adrFormat,
        TextWriter output,
        TextWriter error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (!Directory.Exists(directoryPath))
        {
            error.WriteLine($"ADR directory does not exist: '{directoryPath}'.");
            return ExitCodes.OperationalError;
        }

        try
        {
            var documents = AdrDocumentLoader.LoadDirectory(directoryPath);
            var result = AdrValidator.Validate(documents, adrFormat);

            switch (format)
            {
                case CheckOutputFormat.Json:
                    CheckReportRenderer.WriteJson(documents, result, directoryPath, output);
                    break;
                case CheckOutputFormat.Sarif:
                    CheckReportRenderer.WriteSarif(result, directoryPath, output);
                    break;
            }

            if (!result.IsValid)
            {
                if (format == CheckOutputFormat.Text)
                {
                    ValidationOutput.WriteIssues(result, error);
                }

                return ExitCodes.ValidationFailed;
            }

            if (format == CheckOutputFormat.Text)
            {
                output.WriteLine($"Validated {documents.Count} ADR(s): no issues found.");
            }

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
    }

    private static int WriteOperationalError(
        Exception exception,
        TextWriter error)
    {
        error.WriteLine($"Unable to validate ADRs: {exception.Message}");
        return ExitCodes.OperationalError;
    }
}
