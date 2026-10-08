using AdrGuard.Parsing;
using AdrGuard.Validation;
using AdrGuard.Git;
using AdrGuard.Baselines;

namespace AdrGuard.Cli;

internal static class CheckCommand
{
    internal static int Run(
        string directoryPath,
        TextWriter output,
        TextWriter error) =>
        Run(
            directoryPath,
            CheckOutputFormat.Text,
            AdrFormat.Canonical,
            changed: false,
            baseReference: null,
            baselinePath: null,
            output,
            error,
            default);

    internal static int Run(
        string directoryPath,
        CheckOutputFormat format,
        AdrFormat adrFormat,
        bool changed,
        string? baseReference,
        string? baselinePath,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
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
            cancellationToken.ThrowIfCancellationRequested();
            var documents = AdrDocumentLoader.LoadDirectory(directoryPath, cancellationToken);
            var fullResult = AdrValidator.Validate(documents, adrFormat);
            var result = fullResult;
            IReadOnlySet<string>? changedPaths = null;
            if (changed)
            {
                var changeSet = GitChangeDetector.Detect(
                    directoryPath,
                    baseReference!,
                    cancellationToken);
                changedPaths = changeSet.CurrentPaths;
                result = IncrementalValidation.Select(fullResult, changedPaths);
            }

            DiagnosticBaselineComparison? baseline = null;
            if (baselinePath is not null)
            {
                // Compare the complete current ADR set to the baseline before
                // filtering unchanged local debt. Otherwise unchanged findings
                // would be incorrectly classified as resolved.
                var comparison = DiagnosticBaselineService.Compare(
                    fullResult,
                    DiagnosticBaselineService.Load(baselinePath),
                    directoryPath);
                baseline = changedPaths is null
                    ? comparison
                    : comparison with
                    {
                        NewIssues = IncrementalValidation.Select(
                            new ValidationResult(comparison.NewIssues),
                            changedPaths).Issues,
                        ExistingIssues = IncrementalValidation.Select(
                            new ValidationResult(comparison.ExistingIssues),
                            changedPaths).Issues,
                    };
            }

            switch (format)
            {
                case CheckOutputFormat.Json:
                    CheckReportRenderer.WriteJson(
                        documents,
                        result,
                        directoryPath,
                        baseline,
                        output);
                    break;
                case CheckOutputFormat.Sarif:
                    CheckReportRenderer.WriteSarif(
                        result,
                        directoryPath,
                        baseline,
                        output);
                    break;
            }

            var failingResult = baseline is null
                ? result
                : new ValidationResult(baseline.NewIssues);
            if (!failingResult.IsValid)
            {
                if (format == CheckOutputFormat.Text)
                {
                    ValidationOutput.WriteIssues(failingResult, error);
                    if (baseline is not null)
                    {
                        error.WriteLine(
                            $"Baseline: {baseline.NewIssues.Count} new, {baseline.ExistingIssues.Count} existing, {baseline.ResolvedEntries.Count} resolved issue(s).");
                    }
                }

                return ExitCodes.ValidationFailed;
            }

            if (format == CheckOutputFormat.Text)
            {
                output.WriteLine(
                    baseline is null
                        ? $"Validated {documents.Count} ADR(s): no issues found."
                        : $"Validated {documents.Count} ADR(s): no new issues. Baseline: {baseline.ExistingIssues.Count} existing, {baseline.ResolvedEntries.Count} resolved issue(s).");
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
        catch (InvalidDataException exception)
        {
            return WriteOperationalError(exception, error);
        }
        catch (InvalidOperationException exception)
        {
            return WriteOperationalError(exception, error);
        }
        catch (GitOperationException exception)
        {
            return WriteOperationalError(exception, error);
        }
        catch (OperationCanceledException)
        {
            error.WriteLine("ADR validation was canceled.");
            return ExitCodes.OperationalError;
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
