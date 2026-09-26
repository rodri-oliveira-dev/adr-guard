using AdrGuard.Parsing;
using AdrGuard.Review;
using AdrGuard.Validation;

namespace AdrGuard.Cli;

internal static class ReviewCommand
{
    internal static int Run(
        string targetPath,
        IAdrReviewProvider provider,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
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
            var document = AdrMarkdownParser.Parse(fullPath, markdown);
            var validation = AdrValidator.Validate([document]);

            if (!validation.IsValid)
            {
                error.WriteLine(
                    "Selected ADR is structurally invalid and was not sent to the review provider.");
                ValidationOutput.WriteIssues(validation, error);
                return ExitCodes.ValidationFailed;
            }

            var result = provider
                .ReviewAsync(
                    new AdrReviewRequest(fullPath, markdown),
                    cancellationToken)
                .GetAwaiter()
                .GetResult();

            output.WriteLine("AI-assisted ADR review (advisory only)");
            output.WriteLine("Human review remains authoritative; no ADR content or status was changed.");
            output.WriteLine();
            output.WriteLine(result.Summary);
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
