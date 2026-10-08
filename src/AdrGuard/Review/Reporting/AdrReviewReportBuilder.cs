using AdrGuard.Model;
using System.Globalization;
using System.Text.RegularExpressions;

namespace AdrGuard.Review.Reporting;

internal static partial class AdrReviewReportBuilder
{
    internal const string SchemaVersion = "1.0";

    internal const string ReviewerName = "adr-guard";

    internal const string CostCaveat =
        "Provider token usage and charges may apply. ADR Guard does not estimate or report precise provider costs.";

    internal static AdrReviewReport Build(
        AdrDocument target,
        AdrReviewContext context,
        AdrReviewResult result,
        string providerName,
        string model,
        DateTimeOffset generatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        var sources = BuildSelectedSources(
            target,
            context);

        var dimensions = AdrReviewContract.Dimensions
            .Select(dimension =>
                new AdrReviewDimensionReport(
                    dimension,
                    result.Findings
                        .Where(finding => string.Equals(
                            finding.Dimension,
                            dimension,
                            StringComparison.Ordinal))
                        .Select(finding =>
                            BuildAssessment(
                                finding,
                                sources))
                        .ToArray()))
            .ToArray();

        var followUpFindings = AdrReviewContract.Dimensions
            .SelectMany(dimension =>
                result.Findings
                    .Where(finding =>
                        string.Equals(
                            finding.Dimension,
                            dimension,
                            StringComparison.Ordinal)
                        && IsFollowUpClassification(
                            finding.Classification))
                    .Select(finding =>
                    {
                        var assessment = BuildAssessment(
                            finding,
                            sources);

                        return new AdrReviewFollowUpFindingReport(
                            finding.Dimension,
                            finding.Classification,
                            assessment.FollowUpPriority!,
                            assessment.Uncertainty,
                            finding.Explanation,
                            finding.Guidance,
                            assessment.Evidence);
                    }))
            .ToArray();

        var limitations = BuildLimitations(
            dimensions);

        return new AdrReviewReport(
            SchemaVersion,
            generatedAtUtc
                .ToUniversalTime()
                .ToString(
                    "O",
                    CultureInfo.InvariantCulture),
            new AdrReviewTargetReport(
                "target",
                target.FileName,
                FormatAdrId(target.Id),
                target.Title,
                target.Status),
            new AdrReviewReviewerMetadata(
                ReviewerName,
                AdrReviewContract.Version),
            new AdrReviewProviderMetadata(
                providerName,
                model),
            BuildInputScope(
                context),
            DetermineOutcome(
                followUpFindings),
            dimensions,
            followUpFindings,
            limitations,
            CostCaveat);
    }

    private static AdrReviewAssessmentReport BuildAssessment(
        AdrReviewFinding finding,
        IReadOnlyList<SelectedSource> sources)
    {
        var evidence = ResolveEvidence(
            finding,
            sources);

        return new AdrReviewAssessmentReport(
            finding.Classification,
            finding.Explanation,
            finding.Guidance,
            FollowUpPriority(
                finding.Classification),
            Uncertainty(
                finding.Classification),
            evidence);
    }

    private static AdrReviewInputScopeReport BuildInputScope(
        AdrReviewContext context)
    {
        var explicitContext = context.ExplicitFiles
            .Select((file, index) =>
                new AdrReviewInputSourceReport(
                    $"context-{index + 1}",
                    Path.GetFileName(file.FilePath),
                    null))
            .ToArray();

        var existingNames =
            (context.ExistingAdrs?.IncludedSourceNames
                ?? Array.Empty<string>())
            .Concat(
                context.CrossAdrEvidence?.IncludedSourceNames
                ?? Array.Empty<string>())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var existing = existingNames
            .Select((fileName, index) =>
                new AdrReviewInputSourceReport(
                    $"existing-{index + 1}",
                    fileName,
                    ParseAdrId(fileName)))
            .ToArray();

        return new AdrReviewInputScopeReport(
            context.ExistingAdrs is not null
                || context.CrossAdrEvidence is not null,
            explicitContext,
            existing,
            context.ExistingAdrs?.IsBounded == true
                || context.CrossAdrEvidence?.IsBounded == true);
    }

    private static List<SelectedSource> BuildSelectedSources(
        AdrDocument target,
        AdrReviewContext context)
    {
        var sources = new List<SelectedSource>
        {
            new(
                "target",
                target.FileName,
                FormatAdrId(target.Id)),
        };

        sources.AddRange(
            context.ExplicitFiles
                .Select((file, index) =>
                    new SelectedSource(
                        $"context-{index + 1}",
                        Path.GetFileName(file.FilePath),
                        null)));

        var existingNames =
            (context.ExistingAdrs?.IncludedSourceNames
                ?? Array.Empty<string>())
            .Concat(
                context.CrossAdrEvidence?.IncludedSourceNames
                ?? Array.Empty<string>())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        sources.AddRange(
            existingNames.Select((fileName, index) =>
                new SelectedSource(
                    $"existing-{index + 1}",
                    fileName,
                    ParseAdrId(fileName))));

        return sources;
    }

    private static AdrReviewEvidenceReport[] ResolveEvidence(
        AdrReviewFinding finding,
        IReadOnlyList<SelectedSource> sources)
    {
        if (string.IsNullOrWhiteSpace(finding.Source))
        {
            return [];
        }

        var sourceText = finding.Source.Trim();

        if (sourceText.Contains('/')
            || sourceText.Contains('\\')
            || WindowsAbsolutePath().IsMatch(sourceText))
        {
            throw new InvalidOperationException(
                "Review finding contains an undocumented path. Only selected source IDs and filenames are allowed.");
        }

        var fileTokenMatches = SourceFileToken()
            .Matches(sourceText)
            .Cast<Match>()
            .ToArray();
        var fileTokens = fileTokenMatches
            .Select(match => match.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        foreach (var token in fileTokens)
        {
            if (!sources.Any(source =>
                    string.Equals(
                        source.Path,
                        token,
                        StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"Review finding references an unselected source '{token}'.");
            }
        }

        var matches =
            new Dictionary<string, (SelectedSource Source, int Position)>(
                StringComparer.Ordinal);

        foreach (var source in sources)
        {
            var idMarker = $"[{source.SourceId}]";
            var idPosition = sourceText.IndexOf(
                idMarker,
                StringComparison.Ordinal);

            if (idPosition >= 0)
            {
                matches[source.SourceId] = (
                    source,
                    idPosition);
            }
        }

        foreach (var tokenMatch in fileTokenMatches)
        {
            var candidates = sources
                .Where(source =>
                    string.Equals(
                        source.Path,
                        tokenMatch.Value,
                        StringComparison.Ordinal))
                .ToArray();

            if (candidates.Length == 0)
            {
                continue;
            }

            if (candidates.Length > 1)
            {
                var explicitlyIdentified = candidates
                    .Any(candidate =>
                        sourceText.Contains(
                            $"[{candidate.SourceId}]",
                            StringComparison.Ordinal));

                if (!explicitlyIdentified)
                {
                    throw new InvalidOperationException(
                        $"Review finding source '{sourceText}' is ambiguous; use the visible source ID.");
                }

                continue;
            }

            var candidate = candidates[0];

            if (!matches.TryGetValue(
                    candidate.SourceId,
                    out var existing)
                || tokenMatch.Index < existing.Position)
            {
                matches[candidate.SourceId] = (
                    candidate,
                    tokenMatch.Index);
            }
        }

        if (matches.Count == 0)
        {
            throw new InvalidOperationException(
                $"Review finding source '{sourceText}' does not match any selected review source.");
        }

        return matches.Values
            .OrderBy(match => match.Position)
            .ThenBy(
                match => match.Source.SourceId,
                StringComparer.Ordinal)
            .Select(match =>
                new AdrReviewEvidenceReport(
                    match.Source.SourceId,
                    match.Source.Path,
                    match.Source.AdrId,
                    Line: null,
                    finding.Excerpt))
            .ToArray();
    }

    private static string DetermineOutcome(
        AdrReviewFollowUpFindingReport[] findings)
    {
        if (findings.Any(finding =>
                string.Equals(
                    finding.Classification,
                    "missing-context",
                    StringComparison.Ordinal)))
        {
            return "needs-context";
        }

        return findings.Length > 0
            ? "follow-up-suggested"
            : "no-follow-up-findings";
    }

    private static string[] BuildLimitations(
        IReadOnlyList<AdrReviewDimensionReport> dimensions)
    {
        var assessments = dimensions
            .SelectMany(dimension => dimension.Assessments)
            .ToArray();

        var limitations = new List<string>
        {
            "AI-assisted review is advisory and may be incomplete or incorrect; human review remains authoritative.",
            "Evidence line numbers are null unless verified line information is available; ADR Guard does not infer them.",
        };

        if (assessments.Any(assessment =>
                string.Equals(
                    assessment.Classification,
                    "missing-context",
                    StringComparison.Ordinal)))
        {
            limitations.Add(
                "One or more dimensions have insufficient context and require additional human-provided evidence.");
        }

        if (assessments.Any(assessment =>
                assessment.Evidence.Length == 0))
        {
            limitations.Add(
                "One or more assessments have no verified source evidence because the selected material did not provide it.");
        }

        return limitations.ToArray();
    }

    private static bool IsFollowUpClassification(
        string classification) =>
        classification is
            "potential-risk"
            or "missing-context"
            or "recommendation-for-human-investigation";

    private static string? FollowUpPriority(
        string classification) =>
        classification switch
        {
            "missing-context" => "required",
            "potential-risk" => "recommended",
            "recommendation-for-human-investigation" => "recommended",
            _ => null,
        };

    private static string? Uncertainty(
        string classification) =>
        classification switch
        {
            "missing-context" => "not-enough-information",
            "potential-risk" => "potential",
            "recommendation-for-human-investigation" =>
                "requires-human-investigation",
            _ => null,
        };

    private static string? FormatAdrId(
        int? id) =>
        id is { } value
            ? value.ToString(
                "D4",
                CultureInfo.InvariantCulture)
            : null;

    private static string? ParseAdrId(
        string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(
            fileName);
        var separator = stem.IndexOf('-');

        if (separator <= 0)
        {
            return null;
        }

        var candidate = stem[..separator];

        return candidate.All(char.IsDigit)
            ? candidate
            : null;
    }

    [GeneratedRegex(
        @"[A-Za-z0-9][A-Za-z0-9._-]*\.(?:md|txt)",
        RegexOptions.CultureInvariant)]
    private static partial Regex SourceFileToken();

    [GeneratedRegex(
        @"(?:^|\s)[A-Za-z]:\\",
        RegexOptions.CultureInvariant)]
    private static partial Regex WindowsAbsolutePath();

    private sealed record SelectedSource(
        string SourceId,
        string Path,
        string? AdrId);
}
