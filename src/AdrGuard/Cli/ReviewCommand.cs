using AdrGuard.Parsing;
using AdrGuard.Review;
using AdrGuard.Validation;

namespace AdrGuard.Cli;

internal static class ReviewCommand
{
    internal static int Run(
        string targetPath,
        IReadOnlyList<string> contextFilePaths,
        bool includeExistingAdrs,
        IAdrReviewProvider provider,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        ArgumentNullException.ThrowIfNull(contextFilePaths);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        var fullPath = Path.GetFullPath(targetPath);

        if (!string.Equals(
                Path.GetExtension(fullPath),
                ".md",
                StringComparison.OrdinalIgnoreCase))
        {
            error.WriteLine("Review target must be a Markdown ADR file with a .md extension.");
            return ExitCodes.UsageError;
        }

        if (!File.Exists(fullPath))
        {
            error.WriteLine($"ADR file does not exist: '{fullPath}'.");
            return ExitCodes.OperationalError;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var markdown = File.ReadAllText(fullPath);
            var directory = Path.GetDirectoryName(fullPath)
                ?? Directory.GetCurrentDirectory();
            var documents = AdrDocumentLoader.LoadDirectory(
                directory,
                cancellationToken);
            var validation = AdrValidator.Validate(documents);
            var targetValidation = new ValidationResult(
                validation.Issues
                    .Where(issue =>
                        string.Equals(
                            Path.GetFullPath(issue.FilePath),
                            fullPath,
                            StringComparison.OrdinalIgnoreCase))
                    .ToArray());

            if (!targetValidation.IsValid)
            {
                error.WriteLine(
                    "Selected ADR is structurally invalid and was not sent to the review provider.");
                ValidationOutput.WriteIssues(targetValidation, error);
                return ExitCodes.ValidationFailed;
            }

            AdrReviewContext reviewContext;
            try
            {
                reviewContext = AdrReviewContextBuilder
                    .BuildAsync(
                        fullPath,
                        markdown,
                        contextFilePaths,
                        includeExistingAdrs,
                        cancellationToken)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (InvalidOperationException exception)
            {
                error.WriteLine($"Unable to build review context: {exception.Message}");
                return ExitCodes.OperationalError;
            }

            output.WriteLine("Review material sent to the configured external provider:");
            output.WriteLine($"- target ADR: {Path.GetFileName(fullPath)}");

            foreach (var contextFile in reviewContext.ExplicitFiles)
            {
                output.WriteLine($"- explicit context: {Path.GetFileName(contextFile.FilePath)}");
            }

            if (includeExistingAdrs)
            {
                output.WriteLine(
                    $"- existing ADR context: {reviewContext.ExistingAdrs?.IncludedCount ?? 0} parsed ADR(s)");
                output.WriteLine(
                    "Warning: --include-existing-adrs transmits bounded parsed ADR content to the configured external provider.");
            }

            var providerContext = AdrReviewContextBuilder.ComposeProviderContext(reviewContext);

            var result = provider
                .ReviewAsync(
                    new AdrReviewRequest(fullPath, markdown, providerContext),
                    cancellationToken)
                .GetAwaiter()
                .GetResult();

            output.WriteLine("AI-assisted ADR review (advisory only)");
            output.WriteLine("Human review remains authoritative; no ADR content or status was changed.");
            output.WriteLine();
            output.WriteLine(result.ToHumanReadable());
            return ExitCodes.Success;
        }
        catch (OperationCanceledException)
        {
            error.WriteLine("ADR review was canceled.");
            return ExitCodes.OperationalError;
        }
        catch (IOException exception)
        {
            error.WriteLine($"Unable to review ADR: {exception.Message}");
            return ExitCodes.OperationalError;
        }
        catch (UnauthorizedAccessException exception)
        {
            error.WriteLine($"Unable to review ADR: {exception.Message}");
            return ExitCodes.OperationalError;
        }
        catch (InvalidOperationException exception)
        {
            error.WriteLine($"ADR review provider failed: {exception.Message}");
            return ExitCodes.OperationalError;
        }
        catch (ArgumentException exception)
        {
            error.WriteLine($"ADR review failed: {exception.Message}");
            return ExitCodes.OperationalError;
        }
    }
}
