using AdrGuard.Model;
using AdrGuard.Parsing;
using AdrGuard.Review;
using AdrGuard.Review.Policy;
using AdrGuard.Review.Reporting;
using AdrGuard.Review.Security;
using AdrGuard.Validation;
using AdrGuard.Git;

namespace AdrGuard.Cli;

internal static class ReviewCommand
{
    internal static int Run(
        string targetPath,
        IReadOnlyList<string> contextFilePaths,
        bool includeExistingAdrs,
        string providerName,
        string model,
        AdrReviewOutputFormat format,
        string? outputPath,
        bool overwriteOutput,
        AdrReviewPolicyMode policyMode,
        string? policyFilePath,
        AdrReviewSecurityBoundary securityBoundary,
        Func<IAdrReviewProvider> providerFactory,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default) =>
        Run(
            targetPath,
            contextFilePaths,
            includeExistingAdrs,
            providerName,
            model,
            format,
            outputPath,
            overwriteOutput,
            policyMode,
            policyFilePath,
            securityBoundary,
            providerFactory,
            output,
            error,
            compareReference: null,
            cancellationToken);

    internal static int Run(
        string targetPath,
        IReadOnlyList<string> contextFilePaths,
        bool includeExistingAdrs,
        string providerName,
        string model,
        AdrReviewOutputFormat format,
        string? outputPath,
        bool overwriteOutput,
        AdrReviewPolicyMode policyMode,
        string? policyFilePath,
        AdrReviewSecurityBoundary securityBoundary,
        Func<IAdrReviewProvider> providerFactory,
        TextWriter output,
        TextWriter error,
        string? compareReference,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        ArgumentNullException.ThrowIfNull(contextFilePaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentNullException.ThrowIfNull(securityBoundary);
        ArgumentNullException.ThrowIfNull(providerFactory);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (compareReference is not null)
        {
            try
            {
                GitChangeDetector.ValidateReference(compareReference);
            }
            catch (ArgumentException exception)
            {
                error.WriteLine(
                    securityBoundary.SanitizeDiagnostic(
                        $"Invalid comparison reference: {exception.Message}"));
                return ExitCodes.UsageError;
            }
        }

        var fullPath = Path.GetFullPath(targetPath);

        if (!string.Equals(
                Path.GetExtension(fullPath),
                ".md",
                StringComparison.OrdinalIgnoreCase))
        {
            error.WriteLine(
                "Review target must be a Markdown ADR file with a .md extension.");
            return ExitCodes.UsageError;
        }

        if (!File.Exists(fullPath))
        {
            error.WriteLine(
                securityBoundary.SanitizeDiagnostic(
                    $"ADR file does not exist: '{fullPath}'."));
            return ExitCodes.OperationalError;
        }

        if (outputPath is not null)
        {
            try
            {
                AdrReviewReportFileWriter.ValidateDestination(
                    outputPath,
                    format,
                    fullPath,
                    overwriteOutput);
            }
            catch (Exception exception) when (
                exception is InvalidOperationException
                    or IOException
                    or UnauthorizedAccessException
                    or ArgumentException)
            {
                error.WriteLine(
                    securityBoundary.SanitizeDiagnostic(
                        $"Invalid review report output: {exception.Message}"));
                return ExitCodes.OperationalError;
            }
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var documentStatus = PrepareDocument(
                fullPath, compareReference, error, securityBoundary, out var prepared, cancellationToken);
            if (documentStatus is not null) return documentStatus.Value;
            var markdown = prepared!.Markdown;
            var document = prepared.Document;
            var policyStatus = ApplyPolicy(
                document, contextFilePaths, policyMode, policyFilePath, format,
                output, error, securityBoundary);
            if (policyStatus is not null) return policyStatus.Value;

            var contextStatus = PrepareContext(
                fullPath, markdown, contextFilePaths, includeExistingAdrs, prepared.Comparison,
                document, securityBoundary, error, out var securedMaterial, cancellationToken);
            if (contextStatus is not null) return contextStatus.Value;
            var securedReviewContext = securedMaterial!.Context;
            var securedDocument = securedMaterial.Document;

            var diagnosticWriter =
                format == AdrReviewOutputFormat.Json
                    ? error
                    : output;

            WriteSourceDisclosure(
                securedReviewContext,
                includeExistingAdrs,
                diagnosticWriter);

            var providerContext =
                AdrReviewContextBuilder.ComposeProviderContext(
                    securedReviewContext);
            var providerStatus = InvokeProvider(
                providerFactory, securedReviewContext, providerContext,
                error, securityBoundary, out var result, cancellationToken);
            if (providerStatus is not null) return providerStatus.Value;

            return WriteReport(
                securedDocument, securedReviewContext, result!, providerName, model, format,
                outputPath, fullPath, overwriteOutput, output, diagnosticWriter, error,
                securityBoundary);
        }
        catch (OperationCanceledException)
        {
            error.WriteLine(
                "ADR review was canceled.");
            return ExitCodes.OperationalError;
        }
        catch (IOException exception)
        {
            error.WriteLine(
                securityBoundary.SanitizeDiagnostic(
                    $"Unable to review ADR: {exception.Message}"));
            return ExitCodes.OperationalError;
        }
        catch (UnauthorizedAccessException exception)
        {
            error.WriteLine(
                securityBoundary.SanitizeDiagnostic(
                    $"Unable to review ADR: {exception.Message}"));
            return ExitCodes.OperationalError;
        }
        catch (GitOperationException exception)
        {
            error.WriteLine(
                securityBoundary.SanitizeDiagnostic(
                    $"Unable to compare ADR versions: {exception.Message}"));
            return ExitCodes.OperationalError;
        }
        catch (ArgumentException exception)
        {
            error.WriteLine(
                securityBoundary.SanitizeDiagnostic(
                    $"ADR review failed: {exception.Message}"));
            return ExitCodes.OperationalError;
        }
    }

    private static int? PrepareDocument(
        string fullPath,
        string? compareReference,
        TextWriter error,
        AdrReviewSecurityBoundary securityBoundary,
        out PreparedReview? prepared,
        CancellationToken cancellationToken)
    {
        var markdown = File.ReadAllText(fullPath);
        var comparison = compareReference is null
            ? null
            : GitHistoricalFileReader.Read(fullPath, compareReference, cancellationToken);
        var directory = Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory();
        var document = AdrMarkdownParser.Parse(fullPath, markdown);
        var referencedSiblingPaths = AdrReference.FindAll(document)
            .Select(reference => reference.ResolvedPath)
            .Where(path => IsWithinDirectory(directory, path) && File.Exists(path))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var validation = AdrValidator.Validate([document], referencedSiblingPaths);

        if (!validation.IsValid)
        {
            error.WriteLine(
                "Selected ADR is structurally invalid and was not sent to the review provider.");
            WriteValidationIssues(validation, error, securityBoundary);
            prepared = null;
            return ExitCodes.ValidationFailed;
        }

        prepared = new PreparedReview(markdown, document, comparison);
        return null;
    }

    private static int? ApplyPolicy(
        AdrDocument document,
        IReadOnlyList<string> contextFilePaths,
        AdrReviewPolicyMode policyMode,
        string? policyFilePath,
        AdrReviewOutputFormat format,
        TextWriter output,
        TextWriter error,
        AdrReviewSecurityBoundary securityBoundary)
    {
        AdrReviewPolicyDefinition policyDefinition;
        try
        {
            policyDefinition = AdrReviewPolicyLoader.Load(policyFilePath);
        }
        catch (Exception exception) when (
            exception is FileNotFoundException or InvalidDataException or ArgumentException)
        {
            error.WriteLine(securityBoundary.SanitizeDiagnostic(
                $"Invalid review policy: {exception.Message}"));
            return ExitCodes.UsageError;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error.WriteLine(securityBoundary.SanitizeDiagnostic(
                $"Unable to load review policy: {exception.Message}"));
            return ExitCodes.OperationalError;
        }

        var evaluationStatus = EvaluatePolicy(
            document, contextFilePaths, policyDefinition, policyMode, policyFilePath,
            error, securityBoundary, out var policyEvaluation);
        if (evaluationStatus is not null) return evaluationStatus.Value;

        WritePolicyEvaluationIfRequested(
            policyEvaluation!, policyDefinition, policyMode, policyFilePath, format,
            output, error, securityBoundary);
        if (policyMode != AdrReviewPolicyMode.Enforce || !policyEvaluation!.HasViolations)
        {
            return null;
        }

        error.WriteLine(
            "Review policy outcome: policy-failed. The review provider was not invoked.");
        return ExitCodes.PolicyFailed;
    }

    private static int? EvaluatePolicy(
        AdrDocument document,
        IReadOnlyList<string> contextFilePaths,
        AdrReviewPolicyDefinition policyDefinition,
        AdrReviewPolicyMode policyMode,
        string? policyFilePath,
        TextWriter error,
        AdrReviewSecurityBoundary securityBoundary,
        out AdrReviewPolicyEvaluation? evaluation)
    {
        try
        {
            evaluation = AdrReviewPolicyEvaluator.Evaluate(
                document, contextFilePaths, policyDefinition, policyMode, policyFilePath);
            return null;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            error.WriteLine(securityBoundary.SanitizeDiagnostic(
                $"Unable to evaluate review policy: {exception.Message}"));
            evaluation = null;
            return ExitCodes.OperationalError;
        }
    }

    private static void WritePolicyEvaluationIfRequested(
        AdrReviewPolicyEvaluation evaluation,
        AdrReviewPolicyDefinition definition,
        AdrReviewPolicyMode mode,
        string? policyFilePath,
        AdrReviewOutputFormat format,
        TextWriter output,
        TextWriter error,
        AdrReviewSecurityBoundary securityBoundary)
    {
        if (policyFilePath is null && mode != AdrReviewPolicyMode.Enforce)
        {
            return;
        }

        var writer = mode == AdrReviewPolicyMode.Enforce && evaluation.HasViolations
            ? error
            : format == AdrReviewOutputFormat.Json ? error : output;
        WritePolicyEvaluation(evaluation, definition.Rules.Length, writer, securityBoundary);
    }

    private static int? PrepareContext(
        string fullPath,
        string markdown,
        IReadOnlyList<string> contextFilePaths,
        bool includeExistingAdrs,
        GitHistoricalFile? comparison,
        AdrDocument document,
        AdrReviewSecurityBoundary securityBoundary,
        TextWriter error,
        out SecuredReviewMaterial? securedMaterial,
        CancellationToken cancellationToken)
    {
        AdrReviewContext reviewContext;
        try
        {
            reviewContext = AdrReviewContextBuilder.BuildAsync(
                    fullPath, markdown, contextFilePaths, includeExistingAdrs,
                    cancellationToken, comparison)
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or InvalidDataException or IOException
                or UnauthorizedAccessException or ArgumentException)
        {
            error.WriteLine(securityBoundary.SanitizeDiagnostic(
                $"Unable to build review context: {exception.Message}"));
            securedMaterial = null;
            return ExitCodes.OperationalError;
        }

        try
        {
            securedMaterial = new SecuredReviewMaterial(
                securityBoundary.SanitizeContext(reviewContext),
                securityBoundary.SanitizeDocumentMetadata(document));
            return null;
        }
        catch (Exception exception) when (
            exception is InvalidDataException or InvalidOperationException or ArgumentException)
        {
            error.WriteLine(securityBoundary.SanitizeDiagnostic(
                $"Unable to sanitize review material: {exception.Message}"));
            securedMaterial = null;
            return ExitCodes.OperationalError;
        }
    }

    private static int? InvokeProvider(
        Func<IAdrReviewProvider> providerFactory,
        AdrReviewContext reviewContext,
        string providerContext,
        TextWriter error,
        AdrReviewSecurityBoundary securityBoundary,
        out AdrReviewResult? result,
        CancellationToken cancellationToken)
    {
        IAdrReviewProvider provider;
        try
        {
            provider = providerFactory()
                ?? throw new InvalidOperationException("ADR review provider factory returned null.");
        }
        catch (ArgumentException exception)
        {
            error.WriteLine(securityBoundary.SanitizeDiagnostic(exception.Message));
            error.WriteLine("Run 'adr-guard review --help' for usage.");
            result = null;
            return ExitCodes.UsageError;
        }
        catch (InvalidOperationException exception)
        {
            error.WriteLine(securityBoundary.SanitizeDiagnostic(exception.Message));
            result = null;
            return ExitCodes.OperationalError;
        }

        try
        {
            result = securityBoundary.SanitizeResult(
                provider.ReviewAsync(
                        new AdrReviewRequest(
                            reviewContext.TargetSourceName,
                            reviewContext.TargetMarkdown,
                            providerContext),
                        cancellationToken)
                    .GetAwaiter()
                    .GetResult());
            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or InvalidDataException or ArgumentException)
        {
            error.WriteLine(securityBoundary.SanitizeDiagnostic(
                $"ADR review provider failed: {exception.Message}"));
            result = null;
            return ExitCodes.OperationalError;
        }
    }

    private static int WriteReport(
        AdrDocument document,
        AdrReviewContext reviewContext,
        AdrReviewResult result,
        string providerName,
        string model,
        AdrReviewOutputFormat format,
        string? outputPath,
        string fullPath,
        bool overwriteOutput,
        TextWriter output,
        TextWriter diagnosticWriter,
        TextWriter error,
        AdrReviewSecurityBoundary securityBoundary)
    {
        try
        {
            var report = AdrReviewReportBuilder.Build(
                document, reviewContext, result,
                securityBoundary.SanitizeDiagnostic(providerName),
                securityBoundary.SanitizeDiagnostic(model),
                DateTimeOffset.UtcNow);
            var renderedReport = format switch
            {
                AdrReviewOutputFormat.Json => AdrReviewReportSerializer.Serialize(report),
                AdrReviewOutputFormat.Text => AdrReviewTextRenderer.Render(report),
                _ => throw new InvalidOperationException("Unsupported review output format."),
            };
            var writtenPath = outputPath is null
                ? null
                : AdrReviewReportFileWriter.Write(
                    outputPath, renderedReport, format, fullPath, overwriteOutput);

            output.WriteLine(renderedReport);
            if (writtenPath is not null)
            {
                diagnosticWriter.WriteLine(securityBoundary.SanitizeDiagnostic(
                    $"Review report written to: {writtenPath}"));
            }

            return ExitCodes.Success;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or InvalidDataException or IOException
                or UnauthorizedAccessException or ArgumentException)
        {
            error.WriteLine(securityBoundary.SanitizeDiagnostic(
                $"ADR review report failed: {exception.Message}"));
            return ExitCodes.OperationalError;
        }
    }

    private sealed record PreparedReview(
        string Markdown,
        AdrDocument Document,
        GitHistoricalFile? Comparison);

    private sealed record SecuredReviewMaterial(
        AdrReviewContext Context,
        AdrDocument Document);

    private static void WritePolicyEvaluation(
        AdrReviewPolicyEvaluation evaluation,
        int ruleCount,
        TextWriter writer,
        AdrReviewSecurityBoundary securityBoundary)
    {
        var mode = evaluation.Mode == AdrReviewPolicyMode.Enforce
            ? "enforce"
            : "advisory";

        writer.WriteLine(
            $"Review policy: {mode} ({ruleCount} deterministic rule(s), {evaluation.Violations.Length} violation(s)).");

        foreach (var violation in evaluation.Violations)
        {
            writer.WriteLine(
                securityBoundary.SanitizeDiagnostic(
                    $"- [{violation.RuleName}] {violation.RuleType}: {violation.Evidence}"));
        }

        if (evaluation.Mode == AdrReviewPolicyMode.Advisory
            && evaluation.HasViolations)
        {
            writer.WriteLine(
                "Deterministic policy violations are advisory only in this mode; provider review continues.");
        }
    }

    private static void WriteSourceDisclosure(
        AdrReviewContext reviewContext,
        bool includeExistingAdrs,
        TextWriter writer)
    {
        writer.WriteLine(
            "Review material sent to the configured external provider:");
        writer.WriteLine(
            $"- target ADR [target]: {reviewContext.TargetSourceName}");

        if (reviewContext.Comparison is { } comparison)
        {
            writer.WriteLine(
                $"- prior ADR [comparison-base] from explicit Git reference '{comparison.Reference}': {comparison.FileName}");
        }

        for (var index = 0;
             index < reviewContext.ExplicitFiles.Count;
             index++)
        {
            var contextFile =
                reviewContext.ExplicitFiles[index];

            writer.WriteLine(
                $"- explicit context [context-{index + 1}]: {Path.GetFileName(contextFile.FilePath)}");
        }

        if (!includeExistingAdrs)
        {
            return;
        }

        var transmittedExistingSources =
            (reviewContext.ExistingAdrs?.IncludedSourceNames
                ?? Array.Empty<string>())
            .Concat(
                reviewContext.CrossAdrEvidence?.IncludedSourceNames
                ?? Array.Empty<string>())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        foreach (var sourceName
                 in transmittedExistingSources)
        {
            writer.WriteLine(
                $"- existing ADR: {sourceName}");
        }

        var totalCandidates = Math.Max(
            reviewContext.ExistingAdrs?.TotalCount ?? 0,
            reviewContext.CrossAdrEvidence?.TotalCandidateCount
                ?? 0);
        var isBounded =
            reviewContext.ExistingAdrs?.IsBounded == true
            || reviewContext.CrossAdrEvidence?.IsBounded
                == true;

        writer.WriteLine(
            $"- existing ADR selection: {transmittedExistingSources.Length} of {totalCandidates} candidate ADR source(s) transmitted"
            + (isBounded ? " (bounded)" : string.Empty));

        writer.WriteLine(
            "Warning: --include-existing-adrs transmits bounded parsed ADR content to the configured external provider.");
    }

    private static void WriteValidationIssues(
        ValidationResult result,
        TextWriter error,
        AdrReviewSecurityBoundary securityBoundary)
    {
        foreach (var issue in result.Issues)
        {
            error.WriteLine(
                securityBoundary.SanitizeDiagnostic(
                    $"{issue.FilePath}: {issue.Code} {issue.Message}"));
        }

        error.WriteLine(
            $"Validation failed with {result.Issues.Count} issue(s).");
    }

    private static bool IsWithinDirectory(
        string directory,
        string path)
    {
        var relativePath = Path.GetRelativePath(
            directory,
            path);

        return !Path.IsPathRooted(relativePath)
            && !string.Equals(
                relativePath,
                "..",
                StringComparison.Ordinal)
            && !relativePath.StartsWith(
                $"..{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal)
            && !relativePath.StartsWith(
                $"..{Path.AltDirectorySeparatorChar}",
                StringComparison.Ordinal);
    }
}
