using AdrGuard.Parsing;
using AdrGuard.Review;
using AdrGuard.Review.Policy;
using AdrGuard.Review.Reporting;
using AdrGuard.Validation;

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
        Func<IAdrReviewProvider> providerFactory,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        ArgumentNullException.ThrowIfNull(contextFilePaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentNullException.ThrowIfNull(providerFactory);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

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
                $"ADR file does not exist: '{fullPath}'.");
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
                    $"Invalid review report output: {exception.Message}");
                return ExitCodes.OperationalError;
            }
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var markdown = File.ReadAllText(fullPath);
            var directory = Path.GetDirectoryName(fullPath)
                ?? Directory.GetCurrentDirectory();
            var document = AdrMarkdownParser.Parse(
                fullPath,
                markdown);

            var referencedSiblingPaths = AdrReference
                .FindAll(document)
                .Select(reference => reference.ResolvedPath)
                .Where(path =>
                    IsWithinDirectory(directory, path)
                    && File.Exists(path))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            var validation = AdrValidator.Validate(
                [document],
                referencedSiblingPaths);

            if (!validation.IsValid)
            {
                error.WriteLine(
                    "Selected ADR is structurally invalid and was not sent to the review provider.");
                ValidationOutput.WriteIssues(
                    validation,
                    error);
                return ExitCodes.ValidationFailed;
            }

            AdrReviewPolicyDefinition policyDefinition;

            try
            {
                policyDefinition = AdrReviewPolicyLoader.Load(
                    policyFilePath);
            }
            catch (Exception exception) when (
                exception is FileNotFoundException
                    or InvalidDataException
                    or ArgumentException)
            {
                error.WriteLine(
                    $"Invalid review policy: {exception.Message}");
                return ExitCodes.UsageError;
            }
            catch (Exception exception) when (
                exception is IOException
                    or UnauthorizedAccessException)
            {
                error.WriteLine(
                    $"Unable to load review policy: {exception.Message}");
                return ExitCodes.OperationalError;
            }

            AdrReviewPolicyEvaluation policyEvaluation;

            try
            {
                policyEvaluation = AdrReviewPolicyEvaluator.Evaluate(
                    document,
                    contextFilePaths,
                    policyDefinition,
                    policyMode,
                    policyFilePath);
            }
            catch (Exception exception) when (
                exception is IOException
                    or UnauthorizedAccessException
                    or ArgumentException)
            {
                error.WriteLine(
                    $"Unable to evaluate review policy: {exception.Message}");
                return ExitCodes.OperationalError;
            }

            if (policyFilePath is not null
                || policyMode == AdrReviewPolicyMode.Enforce)
            {
                var policyWriter =
                    policyMode == AdrReviewPolicyMode.Enforce
                    && policyEvaluation.HasViolations
                        ? error
                        : format == AdrReviewOutputFormat.Json
                            ? error
                            : output;

                WritePolicyEvaluation(
                    policyEvaluation,
                    policyDefinition.Rules.Length,
                    policyWriter);
            }

            if (policyMode == AdrReviewPolicyMode.Enforce
                && policyEvaluation.HasViolations)
            {
                error.WriteLine(
                    "Review policy outcome: policy-failed. The review provider was not invoked.");
                return ExitCodes.PolicyFailed;
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
            catch (Exception exception) when (
                exception is InvalidOperationException
                    or InvalidDataException
                    or IOException
                    or UnauthorizedAccessException
                    or ArgumentException)
            {
                error.WriteLine(
                    $"Unable to build review context: {exception.Message}");
                return ExitCodes.OperationalError;
            }

            var diagnosticWriter =
                format == AdrReviewOutputFormat.Json
                    ? error
                    : output;

            WriteSourceDisclosure(
                fullPath,
                reviewContext,
                includeExistingAdrs,
                diagnosticWriter);

            var providerContext =
                AdrReviewContextBuilder.ComposeProviderContext(
                    reviewContext);

            IAdrReviewProvider provider;

            try
            {
                provider = providerFactory()
                    ?? throw new InvalidOperationException(
                        "ADR review provider factory returned null.");
            }
            catch (ArgumentException exception)
            {
                error.WriteLine(exception.Message);
                error.WriteLine("Run 'adr-guard review --help' for usage.");
                return ExitCodes.UsageError;
            }
            catch (InvalidOperationException exception)
            {
                error.WriteLine(exception.Message);
                return ExitCodes.OperationalError;
            }

            AdrReviewResult result;

            try
            {
                result = provider
                    .ReviewAsync(
                        new AdrReviewRequest(
                            fullPath,
                            markdown,
                            providerContext),
                        cancellationToken)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (InvalidOperationException exception)
            {
                error.WriteLine(
                    $"ADR review provider failed: {exception.Message}");
                return ExitCodes.OperationalError;
            }
            catch (ArgumentException exception)
            {
                error.WriteLine(
                    $"ADR review provider failed: {exception.Message}");
                return ExitCodes.OperationalError;
            }

            string renderedReport;
            string? writtenPath = null;

            try
            {
                var report = AdrReviewReportBuilder.Build(
                    document,
                    reviewContext,
                    result,
                    providerName,
                    model,
                    DateTimeOffset.UtcNow);

                renderedReport = format switch
                {
                    AdrReviewOutputFormat.Json =>
                        AdrReviewReportSerializer.Serialize(
                            report),
                    AdrReviewOutputFormat.Text =>
                        AdrReviewTextRenderer.Render(
                            report),
                    _ => throw new InvalidOperationException(
                        "Unsupported review output format."),
                };

                if (outputPath is not null)
                {
                    writtenPath =
                        AdrReviewReportFileWriter.Write(
                            outputPath,
                            renderedReport,
                            format,
                            fullPath,
                            overwriteOutput);
                }
            }
            catch (Exception exception) when (
                exception is InvalidOperationException
                    or InvalidDataException
                    or IOException
                    or UnauthorizedAccessException
                    or ArgumentException)
            {
                error.WriteLine(
                    $"ADR review report failed: {exception.Message}");
                return ExitCodes.OperationalError;
            }

            output.WriteLine(renderedReport);

            if (writtenPath is not null)
            {
                diagnosticWriter.WriteLine(
                    $"Review report written to: {writtenPath}");
            }

            return ExitCodes.Success;
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
                $"Unable to review ADR: {exception.Message}");
            return ExitCodes.OperationalError;
        }
        catch (UnauthorizedAccessException exception)
        {
            error.WriteLine(
                $"Unable to review ADR: {exception.Message}");
            return ExitCodes.OperationalError;
        }
        catch (ArgumentException exception)
        {
            error.WriteLine(
                $"ADR review failed: {exception.Message}");
            return ExitCodes.OperationalError;
        }
    }

    private static void WritePolicyEvaluation(
        AdrReviewPolicyEvaluation evaluation,
        int ruleCount,
        TextWriter writer)
    {
        var mode = evaluation.Mode == AdrReviewPolicyMode.Enforce
            ? "enforce"
            : "advisory";

        writer.WriteLine(
            $"Review policy: {mode} ({ruleCount} deterministic rule(s), {evaluation.Violations.Length} violation(s)).");

        foreach (var violation in evaluation.Violations)
        {
            writer.WriteLine(
                $"- [{violation.RuleName}] {violation.RuleType}: {violation.Evidence}");
        }

        if (evaluation.Mode == AdrReviewPolicyMode.Advisory
            && evaluation.HasViolations)
        {
            writer.WriteLine(
                "Deterministic policy violations are advisory only in this mode; provider review continues.");
        }
    }

    private static void WriteSourceDisclosure(
        string fullPath,
        AdrReviewContext reviewContext,
        bool includeExistingAdrs,
        TextWriter writer)
    {
        writer.WriteLine(
            "Review material sent to the configured external provider:");
        writer.WriteLine(
            $"- target ADR [target]: {Path.GetFileName(fullPath)}");

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
