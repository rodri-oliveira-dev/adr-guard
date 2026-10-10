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
        TextWriter error) =>
        Run(directoryPath, outputPath, AdrFormat.Canonical, null, false, null, null, false, output, error);

    internal static int Run(
        string directoryPath,
        string? outputPath,
        AdrFormat adrFormat,
        TextWriter output,
        TextWriter error) =>
        Run(directoryPath, outputPath, adrFormat, null, false, null, null, false, output, error);

    internal static int Run(
        string directoryPath,
        string? outputPath,
        AdrFormat adrFormat,
        string? lifecycleStatuses,
        bool conventionalSupersession,
        string? filenamePolicy,
        string? validationProfile,
        bool enrichedCatalog,
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
            var resolvedOutputPath = ResolveOutputPath(directoryPath, outputPath);

            if (!IsAllowedOutputPath(directoryPath, resolvedOutputPath))
            {
                error.WriteLine(
                    "A custom index inside the ADR directory must be named 'README.md' "
                    + "because other Markdown files are validated as ADRs.");
                return ExitCodes.UsageError;
            }

            var policy = AdrFilenamePolicy.Parse(filenamePolicy);
            var documents = AdrDocumentLoader.LoadDirectory(directoryPath)
                .Select(policy.NormalizeIdentity).ToArray();
            var validationResult = AdrValidator.Validate(
                documents,
                null,
                AdrValidationOptionsFactory.Create(
                    adrFormat,
                    AdrRepositoryRoot.Resolve(directoryPath),
                    lifecycleStatuses,
                    conventionalSupersession,
                    filenamePolicy,
                    null,
                    false,
                    validationProfile));

            if (!validationResult.IsValid)
            {
                ValidationOutput.WriteIssues(validationResult, error);
                return ExitCodes.ValidationFailed;
            }

            var resolvedDocuments = AdrStatusResolver.ForFormat(documents, adrFormat);
            var content = enrichedCatalog
                ? AdrIndexGenerator.GenerateEnriched(resolvedDocuments)
                : AdrIndexGenerator.Generate(resolvedDocuments);

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
            return Path.GetFullPath(Path.Combine(directoryPath, "README.md"));
        }

        return Path.GetFullPath(outputPath);
    }

    private static bool IsAllowedOutputPath(
        string directoryPath,
        string outputPath)
    {
        var directory = Path.GetFullPath(directoryPath);
        var relativePath = Path.GetRelativePath(directory, outputPath);
        var isInsideDirectory = !Path.IsPathRooted(relativePath)
            && relativePath != ".."
            && !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            && !relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);

        return !isInsideDirectory
            || string.Equals(
                Path.GetFileName(outputPath),
                "README.md",
                StringComparison.OrdinalIgnoreCase);
    }

    private static int WriteOperationalError(
        Exception exception,
        TextWriter error)
    {
        error.WriteLine($"Unable to generate ADR index: {exception.Message}");
        return ExitCodes.OperationalError;
    }
}
