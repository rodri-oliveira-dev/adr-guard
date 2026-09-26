using AdrGuard.Review.Providers;
using System.Globalization;
using System.Text.Json;

namespace AdrGuard.Review.Reporting;

internal static class AdrReviewReportSerializer
{
    internal const int MaximumReportCharacters = 500000;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
        };

    internal static string Serialize(
        AdrReviewReport report)
    {
        Validate(report);

        var json = JsonSerializer.Serialize(
            report,
            JsonOptions);

        EnsureBounded(json);

        return json;
    }

    internal static AdrReviewReport Deserialize(
        string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        EnsureBounded(json);

        AdrReviewReport? report;

        try
        {
            report = JsonSerializer.Deserialize<AdrReviewReport>(
                json,
                JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "Review report JSON is malformed.",
                exception);
        }

        if (report is null)
        {
            throw new InvalidDataException(
                "Review report JSON is empty.");
        }

        Validate(report);
        return report;
    }

    internal static void EnsureBounded(
        string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (content.Length > MaximumReportCharacters)
        {
            throw new InvalidOperationException(
                $"Review report exceeds the {MaximumReportCharacters}-character output limit.");
        }
    }

    private static void Validate(
        AdrReviewReport report)
    {
        if (!string.Equals(
                report.SchemaVersion,
                AdrReviewReportBuilder.SchemaVersion,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Unsupported review report schema version '{report.SchemaVersion}'.");
        }

        if (!DateTimeOffset.TryParseExact(
                report.GeneratedAtUtc,
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out _))
        {
            throw new InvalidDataException(
                "Review report generatedAtUtc must be an ISO-8601 round-trip timestamp.");
        }

        ValidateTarget(
            report.Target);
        ValidateProvider(
            report.Provider);
        ValidateInputScope(
            report.InputScope);
        ValidateDimensions(
            report.Dimensions);
        ValidateFindings(
            report.Findings);
        ValidateOutcome(
            report.Outcome,
            report.Findings);

        if (report.Limitations is null
            || report.Limitations.Any(string.IsNullOrWhiteSpace)
            || string.IsNullOrWhiteSpace(
                report.CostCaveat))
        {
            throw new InvalidDataException(
                "Review report limitations and cost caveat must be explicit.");
        }

        var selectedSources = new HashSet<string>(
            StringComparer.Ordinal)
        {
            report.Target.Path,
        };

        foreach (var source in report.InputScope.ExplicitContext)
        {
            selectedSources.Add(source.Path);
        }

        foreach (var source in report.InputScope.ExistingAdrs)
        {
            selectedSources.Add(source.Path);
        }

        foreach (var evidence in report.Dimensions
                     .SelectMany(dimension => dimension.Assessments)
                     .SelectMany(assessment => assessment.Evidence)
                     .Concat(
                         report.Findings.SelectMany(
                             finding => finding.Evidence)))
        {
            if (evidence is null)
            {
                throw new InvalidDataException(
                    "Review report contains null evidence.");
            }

            ValidateSafePath(
                evidence.Path,
                "evidence path");

            if (!selectedSources.Contains(
                    evidence.Path))
            {
                throw new InvalidDataException(
                    $"Review report evidence references unselected source '{evidence.Path}'.");
            }

            if (evidence.Line is <= 0)
            {
                throw new InvalidDataException(
                    "Review report evidence line must be positive when present.");
            }
        }
    }

    private static void ValidateTarget(
        AdrReviewTargetReport target)
    {
        if (target is null
            || string.IsNullOrWhiteSpace(
                target.SourceId)
            || string.IsNullOrWhiteSpace(
                target.Path))
        {
            throw new InvalidDataException(
                "Review report target is incomplete.");
        }

        ValidateSafePath(
            target.Path,
            "target path");
    }

    private static void ValidateProvider(
        AdrReviewProviderMetadata provider)
    {
        if (provider is null
            || string.IsNullOrWhiteSpace(
                provider.Name)
            || string.IsNullOrWhiteSpace(
                provider.Model))
        {
            throw new InvalidDataException(
                "Review report provider/model metadata is incomplete.");
        }
    }

    private static void ValidateInputScope(
        AdrReviewInputScopeReport inputScope)
    {
        if (inputScope is null
            || inputScope.ExplicitContext is null
            || inputScope.ExistingAdrs is null)
        {
            throw new InvalidDataException(
                "Review report input scope is incomplete.");
        }

        var sourceIds = new HashSet<string>(
            StringComparer.Ordinal)
        {
            "target",
        };

        foreach (var source in inputScope.ExplicitContext
                     .Concat(inputScope.ExistingAdrs))
        {
            if (source is null
                || string.IsNullOrWhiteSpace(
                    source.SourceId)
                || !sourceIds.Add(
                    source.SourceId))
            {
                throw new InvalidDataException(
                    "Review report source IDs must be non-empty and unique.");
            }

            ValidateSafePath(
                source.Path,
                "input source path");
        }

        if (!inputScope.IncludeExistingAdrs
            && inputScope.ExistingAdrs.Length > 0)
        {
            throw new InvalidDataException(
                "Review report cannot include existing ADR sources without opt-in.");
        }
    }

    private static void ValidateDimensions(
        AdrReviewDimensionReport[] dimensions)
    {
        if (dimensions is null
            || dimensions.Length
            != AdrReviewContract.Dimensions.Length)
        {
            throw new InvalidDataException(
                "Review report must contain all eight analyzed dimensions.");
        }

        for (var index = 0;
             index < AdrReviewContract.Dimensions.Length;
             index++)
        {
            var dimension = dimensions[index];

            if (dimension is null
                || !string.Equals(
                    dimension.Name,
                    AdrReviewContract.Dimensions[index],
                    StringComparison.Ordinal)
                || dimension.Assessments is null)
            {
                throw new InvalidDataException(
                    "Review report dimensions must use the documented stable order.");
            }

            foreach (var assessment in dimension.Assessments)
            {
                ValidateAssessment(
                    assessment);
            }
        }
    }

    private static void ValidateFindings(
        AdrReviewFollowUpFindingReport[] findings)
    {
        if (findings is null)
        {
            throw new InvalidDataException(
                "Review report findings array is required.");
        }

        foreach (var finding in findings)
        {
            if (finding is null
                || !AdrReviewContract.Dimensions.Contains(
                    finding.Dimension,
                    StringComparer.Ordinal)
                || finding.Classification is not (
                    "potential-risk"
                    or "missing-context"
                    or "recommendation-for-human-investigation")
                || finding.FollowUpPriority is not (
                    "required"
                    or "recommended")
                || string.IsNullOrWhiteSpace(
                    finding.Explanation)
                || string.IsNullOrWhiteSpace(
                    finding.Guidance)
                || finding.Evidence is null)
            {
                throw new InvalidDataException(
                    "Review report contains an invalid follow-up finding.");
            }
        }
    }

    private static void ValidateAssessment(
        AdrReviewAssessmentReport assessment)
    {
        if (assessment is null
            || !AdrReviewJsonContract.Classifications.Contains(
                assessment.Classification,
                StringComparer.Ordinal)
            || string.IsNullOrWhiteSpace(
                assessment.Explanation)
            || string.IsNullOrWhiteSpace(
                assessment.Guidance)
            || assessment.Evidence is null)
        {
            throw new InvalidDataException(
                "Review report contains an invalid dimension assessment.");
        }

        var requiresPriority =
            assessment.Classification is
                "potential-risk"
                or "missing-context"
                or "recommendation-for-human-investigation";

        if (requiresPriority
            != !string.IsNullOrWhiteSpace(
                assessment.FollowUpPriority))
        {
            throw new InvalidDataException(
                "Follow-up priority is allowed only for actionable follow-up assessments.");
        }
    }

    private static void ValidateOutcome(
        string outcome,
        AdrReviewFollowUpFindingReport[] findings)
    {
        var expected =
            findings.Any(finding =>
                string.Equals(
                    finding.Classification,
                    "missing-context",
                    StringComparison.Ordinal))
                ? "needs-context"
                : findings.Length > 0
                    ? "follow-up-suggested"
                    : "no-follow-up-findings";

        if (!string.Equals(
                outcome,
                expected,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Review report outcome '{outcome}' is inconsistent with its findings.");
        }
    }

    private static void ValidateSafePath(
        string path,
        string field)
    {
        if (string.IsNullOrWhiteSpace(path)
            || Path.IsPathRooted(path)
            || path.Contains('/')
            || path.Contains('\\'))
        {
            throw new InvalidDataException(
                $"Review report {field} must be a privacy-safe selected source filename.");
        }
    }
}
