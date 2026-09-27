using System.Text;

namespace AdrGuard.Review.Reporting;

internal static class AdrReviewReportFileWriter
{
    private static readonly UTF8Encoding Utf8NoBom =
        new(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);

    internal static string ValidateDestination(
        string outputPath,
        AdrReviewOutputFormat format,
        string targetAdrPath,
        bool overwrite)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            outputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            targetAdrPath);

        var fullOutputPath = Path.GetFullPath(
            outputPath);
        var fullTargetPath = Path.GetFullPath(
            targetAdrPath);
        var targetDirectory =
            Path.GetDirectoryName(fullTargetPath)
            ?? Directory.GetCurrentDirectory();
        var targetIndexPath = Path.Combine(
            targetDirectory,
            "README.md");

        if (PathsEqual(
                fullOutputPath,
                fullTargetPath)
            || PathsEqual(
                fullOutputPath,
                targetIndexPath))
        {
            throw new InvalidOperationException(
                "Review report output cannot overwrite the selected ADR or its README.md index.");
        }

        ValidateExtension(
            fullOutputPath,
            format);

        var outputDirectory =
            Path.GetDirectoryName(fullOutputPath);

        if (string.IsNullOrWhiteSpace(
                outputDirectory)
            || !Directory.Exists(
                outputDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Review report output directory does not exist: '{outputDirectory}'.");
        }

        if (File.Exists(fullOutputPath))
        {
            var attributes = File.GetAttributes(
                fullOutputPath);

            if ((attributes
                    & FileAttributes.ReparsePoint)
                != 0)
            {
                throw new InvalidOperationException(
                    "Review report output cannot overwrite a symbolic link or reparse point.");
            }

            if (!overwrite)
            {
                throw new InvalidOperationException(
                    $"Review report output already exists: '{fullOutputPath}'. Use --overwrite to replace it explicitly.");
            }
        }

        return fullOutputPath;
    }

    internal static string Write(
        string outputPath,
        string content,
        AdrReviewOutputFormat format,
        string targetAdrPath,
        bool overwrite)
    {
        ArgumentNullException.ThrowIfNull(content);

        var fullOutputPath = ValidateDestination(
            outputPath,
            format,
            targetAdrPath,
            overwrite);
        var outputDirectory =
            Path.GetDirectoryName(fullOutputPath)!;

        var temporaryPath = Path.Combine(
            outputDirectory,
            $".{Path.GetFileName(fullOutputPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None))
            using (var writer = new StreamWriter(
                       stream,
                       Utf8NoBom))
            {
                writer.Write(content);
            }

            File.Move(
                temporaryPath,
                fullOutputPath,
                overwrite);

            return fullOutputPath;
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static void ValidateExtension(
        string outputPath,
        AdrReviewOutputFormat format)
    {
        var extension = Path.GetExtension(
            outputPath);

        var valid = format switch
        {
            AdrReviewOutputFormat.Json =>
                string.Equals(
                    extension,
                    ".json",
                    StringComparison.OrdinalIgnoreCase),
            AdrReviewOutputFormat.Text =>
                string.Equals(
                    extension,
                    ".md",
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    extension,
                    ".txt",
                    StringComparison.OrdinalIgnoreCase),
            _ => false,
        };

        if (!valid)
        {
            throw new InvalidOperationException(
                format == AdrReviewOutputFormat.Json
                    ? "JSON review report output must use a .json extension."
                    : "Text review report output must use a .md or .txt extension.");
        }
    }

    private static bool PathsEqual(
        string first,
        string second)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var fullFirst = Path.GetFullPath(first);
        var fullSecond = Path.GetFullPath(second);

        if (string.Equals(
                fullFirst,
                fullSecond,
                comparison))
        {
            return true;
        }

        if (!File.Exists(fullFirst)
            || !File.Exists(fullSecond))
        {
            return false;
        }

        return string.Equals(
            ResolveFinalTargetPath(fullFirst),
            ResolveFinalTargetPath(fullSecond),
            comparison);
    }

    private static string ResolveFinalTargetPath(
        string path)
    {
        var file = new FileInfo(path);
        var resolved = file.ResolveLinkTarget(
            returnFinalTarget: true);

        return Path.GetFullPath(
            resolved?.FullName
            ?? file.FullName);
    }
}
