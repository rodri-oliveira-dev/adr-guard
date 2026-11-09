using AdrGuard.Configuration;
using AdrGuard.Model;
using AdrGuard.Parsing;
using AdrGuard.Validation;
using System.Text;

namespace AdrGuard.Cli;

internal static class RelationshipsCommand
{
    internal const int MaximumAdrFiles = 1000;
    internal const int MaximumAdrBytes = 524_288;
    internal const int MaximumTotalAdrBytes = 33_554_432;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static int Run(
        RelationshipsCommandArguments arguments,
        string repositoryRoot,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var root = Path.GetFullPath(repositoryRoot);
            var adrDirectory = ResolveAdrDirectory(root, arguments.DirectoryPath);
            var options = AdrValidationOptionsFactory.Create(
                arguments.AdrFormat,
                root,
                arguments.LifecycleStatuses,
                arguments.ConventionalSupersession,
                arguments.FilenamePolicy,
                placeholderPolicy: null,
                validateMetadata: false,
                arguments.ValidationProfile);
            var documents = LoadDocuments(adrDirectory, root, cancellationToken)
                .Select(options.EffectiveFilenamePolicy.NormalizeIdentity)
                .ToArray();
            documents = AdrStatusResolver.ForFormat(documents, arguments.AdrFormat).ToArray();
            var graph = AdrRelationshipGraphBuilder.Build(documents, options);
            var diagnostics = AdrValidator.Validate(documents, additionalKnownPaths: null, options)
                .Issues
                .Where(issue => AdrRelationshipGraphBuilder.IsRelationshipDiagnostic(issue.Code))
                .ToArray();

            RelationshipGraphReportRenderer.Write(
                graph,
                diagnostics,
                root,
                adrDirectory,
                arguments.AdrFormat,
                output);
            return diagnostics.Any(issue => issue.Severity == ValidationSeverity.Error)
                ? ExitCodes.ValidationFailed
                : ExitCodes.Success;
        }
        catch (OperationCanceledException)
        {
            error.WriteLine("ADR relationship graph generation was canceled.");
            return ExitCodes.OperationalError;
        }
        catch (Exception exception) when (exception is ArgumentException
            or IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or DecoderFallbackException)
        {
            error.WriteLine($"Unable to generate ADR relationship graph: {exception.Message}");
            return ExitCodes.OperationalError;
        }
    }

    private static string ResolveAdrDirectory(string repositoryRoot, string directoryPath)
    {
        var candidate = Path.GetFullPath(directoryPath, repositoryRoot);
        var relative = Path.GetRelativePath(repositoryRoot, candidate);
        var resolved = RepositoryPath.ResolveContained(repositoryRoot, relative, allowMissingLeaf: false);
        if (!Directory.Exists(resolved))
        {
            throw new DirectoryNotFoundException($"ADR directory does not exist: '{directoryPath}'.");
        }
        return resolved;
    }

    private static List<AdrDocument> LoadDocuments(
        string adrDirectory,
        string repositoryRoot,
        CancellationToken cancellationToken)
    {
        var paths = new List<string>(MaximumAdrFiles + 1);
        var enumerationOptions = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = false,
            AttributesToSkip = FileAttributes.ReparsePoint,
            ReturnSpecialDirectories = false,
        };
        foreach (var path in Directory.EnumerateFiles(adrDirectory, "*", enumerationOptions))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.Equals(Path.GetExtension(path), ".md", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Path.GetFileName(path), "README.md", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var relative = Path.GetRelativePath(repositoryRoot, path);
            paths.Add(RepositoryPath.ResolveContained(repositoryRoot, relative, allowMissingLeaf: false));
            if (paths.Count > MaximumAdrFiles)
            {
                throw new InvalidDataException($"ADR directory exceeds the {MaximumAdrFiles}-file limit.");
            }
        }

        paths.Sort(StringComparer.Ordinal);
        var documents = new List<AdrDocument>(paths.Count);
        long totalBytes = 0;
        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bytes = ReadBounded(path);
            totalBytes += bytes.Length;
            if (totalBytes > MaximumTotalAdrBytes)
            {
                throw new InvalidDataException(
                    $"ADR content exceeds the {MaximumTotalAdrBytes}-byte aggregate limit.");
            }

            documents.Add(AdrMarkdownParser.Parse(path, StrictUtf8.GetString(bytes)));
        }
        return documents;
    }

    private static byte[] ReadBounded(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.SequentialScan);
        if (stream.Length > MaximumAdrBytes)
        {
            throw new InvalidDataException(
                $"ADR '{Path.GetFileName(path)}' exceeds the {MaximumAdrBytes}-byte limit.");
        }

        var bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        return bytes;
    }
}
