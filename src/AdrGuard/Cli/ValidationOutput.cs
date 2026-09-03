using AdrGuard.Validation;

namespace AdrGuard.Cli;

internal static class ValidationOutput
{
    internal static void WriteIssues(
        ValidationResult result,
        TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(error);

        foreach (var issue in result.Issues)
        {
            error.WriteLine($"{issue.FilePath}: {issue.Code} {issue.Message}");
        }

        error.WriteLine($"Validation failed with {result.Issues.Count} issue(s).");
    }
}
