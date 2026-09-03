using AdrGuard.Indexing;
using AdrGuard.Parsing;
using AdrGuard.Validation;

namespace AdrGuard.Cli;

internal static class IndexCommand
{
    internal static int Run(
        string directoryPath,
        string? outputPath,
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
            var validationResult = AdrValidator.Validate(documents);

            if (!validationResult.IsValid)
            {
                ValidationOutput.WriteIssues(validationResult, error);
                return ExitCodes.ValidationFailed;
            }

            var resolvedOutputPath = ResolveOutputPath(directoryPath, outputPath);
            var content = AdrIndexGenerator.Generate(documents);

            if (File.Exists(resolvedOutputPath)
                && string.Equals(
                    File.ReadAllText(resolvedOutputPath),
                    content,
                    StringComparison.Ordinal))
            {
                output.WriteLine($"Index already up to date: {resolvedOutputPath}");
                return ExitCodes.Success;
            }

            File.WriteAllText(resolvedOutputPath, content);
            output.WriteLine($"Index written: {resolvedOutputPath}");

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

    private static string ResolveOutputPath(
        string directoryPath,
        string? outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return Path.Combine(directoryPath, "README.md");
        }

        return Path.IsPathRooted(outputPath)
            ? outputPath
            : Path.Combine(directoryPath, outputPath);
    }

    private static int WriteOperationalError(
        Exception exception,
        TextWriter error)
    {
        error.WriteLine($"Unable to generate ADR index: {exception.Message}");
        return ExitCodes.OperationalError;
    }
}
