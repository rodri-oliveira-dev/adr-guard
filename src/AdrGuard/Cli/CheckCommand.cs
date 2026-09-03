using AdrGuard.Parsing;
using AdrGuard.Validation;

namespace AdrGuard.Cli;

internal static class CheckCommand
{
    internal static int Run(
        string directoryPath,
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
            var result = AdrValidator.Validate(documents);

            if (!result.IsValid)
            {
                ValidationOutput.WriteIssues(result, error);
                return ExitCodes.ValidationFailed;
            }

            output.WriteLine($"Validated {documents.Count} ADR(s): no issues found.");
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
