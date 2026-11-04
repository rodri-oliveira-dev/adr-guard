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
            lifecycleStatuses: null,
            conventionalSupersession: false,
            filenamePolicy: null,
            placeholderPolicy: null,
            validateMetadata: false,
            validationProfile: null,
            changed: false,
            baseReference: null,
            baselinePath: null,
            diagnosticLocations: false,
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
        CancellationToken cancellationToken) =>
        Run(directoryPath, format, adrFormat, null, false, null, null, false, null, changed, baseReference, baselinePath, false, output, error, cancellationToken);

    internal static int Run(
        string directoryPath,
        CheckOutputFormat format,
        AdrFormat adrFormat,
        string? lifecycleStatuses,
        bool conventionalSupersession,
        string? filenamePolicy,
        string? placeholderPolicy,
        bool validateMetadata,
        string? validationProfile,
        bool changed,
        string? baseReference,
        string? baselinePath,
        bool diagnosticLocations,
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
            var options = AdrValidationOptionsFactory.Create(
                adrFormat,
                AdrRepositoryRoot.Resolve(directoryPath),
                lifecycleStatuses,
                conventionalSupersession,
                filenamePolicy,
                placeholderPolicy,
                validateMetadata,
                validationProfile);
            var fullResult = AdrValidator.Validate(documents, null, options);
            var selection = SelectResult(directoryPath, baseReference, changed, fullResult, cancellationToken);
            var result = selection.Result;
            var baseline = CompareBaseline(
                directoryPath,
                baselinePath,
                fullResult,
                selection.ChangedPaths);
            WriteReport(format, documents, result, directoryPath, baseline, diagnosticLocations, output);

            var failingResult = baseline is null
                ? result
                : new ValidationResult(baseline.NewIssues);
            if (format == CheckOutputFormat.Text) WriteWarnings(failingResult, error);
            if (!failingResult.IsValid)
            {
                if (format == CheckOutputFormat.Text) WriteFailures(failingResult, baseline, error);

                return ExitCodes.ValidationFailed;
            }

            if (format == CheckOutputFormat.Text) WriteSuccess(documents.Count, failingResult, baseline, output);

            return ExitCodes.Success;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or InvalidOperationException
            or GitOperationException)
        {
            return WriteOperationalError(exception, error);
        }
        catch (OperationCanceledException)
        {
            error.WriteLine("ADR validation was canceled.");
            return ExitCodes.OperationalError;
        }
    }

    private static ResultSelection SelectResult(
        string directoryPath,
        string? baseReference,
        bool changed,
        ValidationResult fullResult,
        CancellationToken cancellationToken)
    {
        if (!changed) return new ResultSelection(fullResult, null);
        var changedPaths = GitChangeDetector.Detect(
            directoryPath,
            baseReference!,
            cancellationToken).CurrentPaths;
        return new ResultSelection(IncrementalValidation.Select(fullResult, changedPaths), changedPaths);
    }

    private static DiagnosticBaselineComparison? CompareBaseline(
        string directoryPath,
        string? baselinePath,
        ValidationResult fullResult,
        IReadOnlySet<string>? changedPaths)
    {
        if (baselinePath is null) return null;
        var comparison = DiagnosticBaselineService.Compare(
            fullResult,
            DiagnosticBaselineService.Load(baselinePath),
            directoryPath);
        return changedPaths is null
            ? comparison
            : comparison with
            {
                NewIssues = IncrementalValidation.Select(
                    new ValidationResult(comparison.NewIssues), changedPaths).Issues,
                ExistingIssues = IncrementalValidation.Select(
                    new ValidationResult(comparison.ExistingIssues), changedPaths).Issues,
            };
    }

    private static void WriteReport(
        CheckOutputFormat format,
        IReadOnlyList<AdrGuard.Model.AdrDocument> documents,
        ValidationResult result,
        string directoryPath,
        DiagnosticBaselineComparison? baseline,
        bool diagnosticLocations,
        TextWriter output)
    {
        if (format == CheckOutputFormat.Json)
            CheckReportRenderer.WriteJson(documents, result, directoryPath, baseline, diagnosticLocations, output);
        else if (format == CheckOutputFormat.Sarif)
            CheckReportRenderer.WriteSarif(result, directoryPath, baseline, diagnosticLocations, output);
    }

    private static void WriteWarnings(ValidationResult result, TextWriter error)
    {
        foreach (var warning in result.Issues.Where(issue => issue.Severity == ValidationSeverity.Warning))
            error.WriteLine($"{warning.FilePath}: {warning.Code} warning {warning.Message}");
    }

    private static void WriteFailures(
        ValidationResult result,
        DiagnosticBaselineComparison? baseline,
        TextWriter error)
    {
        ValidationOutput.WriteIssues(result, error);
        if (baseline is not null)
            error.WriteLine(
                $"Baseline: {baseline.NewIssues.Count} new, {baseline.ExistingIssues.Count} existing, {baseline.ResolvedEntries.Count} resolved issue(s).");
    }

    private static void WriteSuccess(
        int documentCount,
        ValidationResult result,
        DiagnosticBaselineComparison? baseline,
        TextWriter output)
    {
        var warningCount = result.Issues.Count(issue => issue.Severity == ValidationSeverity.Warning);
        var message = warningCount > 0
            ? $"Validated {documentCount} ADR(s): no errors; {warningCount} warning(s)."
            : $"Validated {documentCount} ADR(s): no issues found.";
        if (baseline is not null)
            message = $"Validated {documentCount} ADR(s): no new issues. Baseline: {baseline.ExistingIssues.Count} existing, {baseline.ResolvedEntries.Count} resolved issue(s).";
        output.WriteLine(message);
    }

    private sealed record ResultSelection(
        ValidationResult Result,
        IReadOnlySet<string>? ChangedPaths);

    private static int WriteOperationalError(
        Exception exception,
        TextWriter error)
    {
        error.WriteLine($"Unable to validate ADRs: {exception.Message}");
        return ExitCodes.OperationalError;
    }

}
