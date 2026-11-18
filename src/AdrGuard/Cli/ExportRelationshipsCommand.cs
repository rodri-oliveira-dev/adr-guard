using AdrGuard.Configuration;
using AdrGuard.Exporting;
using AdrGuard.Parsing;
using AdrGuard.Validation;

namespace AdrGuard.Cli;

internal static class ExportRelationshipsCommand
{
    private const string DefaultOutputPath = ".adr-guard/relationships.json";

    internal static int Run(
        ExportRelationshipsCommandArguments arguments,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken,
        AdrRelationshipsExportFileWriter? fileWriter = null)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (!Directory.Exists(arguments.DirectoryPath))
        {
            error.WriteLine($"ADR directory does not exist: '{arguments.DirectoryPath}'.");
            return ExitCodes.OperationalError;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var repositoryRoot = AdrRepositoryRoot.Resolve(arguments.DirectoryPath);
            ValidateInputDirectory(repositoryRoot, arguments.DirectoryPath);
            var relativeOutputPath = arguments.OutputPath ?? DefaultOutputPath;
            ValidateOutputExtension(relativeOutputPath);
            var outputPath = RepositoryPath.ResolveContained(repositoryRoot, relativeOutputPath);
            var outputDirectory = Path.GetDirectoryName(outputPath)
                ?? throw new InvalidOperationException("Unable to resolve the relationships export directory.");

            Directory.CreateDirectory(outputDirectory);
            outputPath = RepositoryPath.ResolveContained(repositoryRoot, relativeOutputPath);

            var options = AdrValidationOptionsFactory.Create(
                arguments.AdrFormat,
                repositoryRoot,
                arguments.LifecycleStatuses,
                arguments.ConventionalSupersession,
                arguments.FilenamePolicy,
                placeholderPolicy: null,
                validateMetadata: false,
                arguments.ValidationProfile);
            var documents = AdrDocumentLoader.LoadDirectory(arguments.DirectoryPath, cancellationToken);
            var export = AdrRelationshipsExportBuilder.Create(documents, repositoryRoot, options);
            using var content = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
            AdrRelationshipsExportSerializer.Write(export, content);

            cancellationToken.ThrowIfCancellationRequested();
            (fileWriter ?? new AdrRelationshipsExportFileWriter()).Write(outputPath, content.ToString());
            output.WriteLine(
                $"Relationships exported: {PortableRelative(repositoryRoot, outputPath)} "
                + $"({export.Adrs.Count} ADR(s), {export.Relationships.Count} relationship(s), {export.Issues.Count} issue(s)).");
            return ExitCodes.Success;
        }
        catch (OperationCanceledException)
        {
            error.WriteLine("ADR relationships export was canceled.");
            return ExitCodes.OperationalError;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or InvalidOperationException
            or ArgumentException)
        {
            error.WriteLine($"Unable to export ADR relationships: {exception.Message}");
            return ExitCodes.OperationalError;
        }
    }

    private static void ValidateInputDirectory(string repositoryRoot, string directoryPath)
    {
        var relative = Path.GetRelativePath(repositoryRoot, Path.GetFullPath(directoryPath));
        RepositoryPath.ResolveContained(repositoryRoot, relative, allowMissingLeaf: false);
    }

    private static void ValidateOutputExtension(string path)
    {
        if (!string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Relationships export output must use a .json extension.", nameof(path));
        }
    }

    private static string PortableRelative(string repositoryRoot, string path) =>
        Path.GetRelativePath(repositoryRoot, path).Replace(Path.DirectorySeparatorChar, '/');
}
