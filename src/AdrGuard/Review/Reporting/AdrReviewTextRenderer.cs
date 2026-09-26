using System.Text;

namespace AdrGuard.Review.Reporting;

internal static class AdrReviewTextRenderer
{
    internal static string Render(
        AdrReviewReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var builder = new StringBuilder();

        builder.AppendLine("# ADR Technical Review");
        builder.AppendLine();
        builder.Append("Schema version: ")
            .AppendLine(report.SchemaVersion);
        builder.Append("Generated at (UTC): ")
            .AppendLine(report.GeneratedAtUtc);
        builder.Append("Target: ")
            .Append(report.Target.Path);

        if (!string.IsNullOrWhiteSpace(
                report.Target.AdrId))
        {
            builder.Append(" (ADR ")
                .Append(report.Target.AdrId)
                .Append(')');
        }

        builder.AppendLine();
        builder.Append("Provider: ")
            .Append(report.Provider.Name)
            .Append(" / ")
            .AppendLine(report.Provider.Model);
        builder.Append("Outcome: ")
            .AppendLine(report.Outcome);
        builder.AppendLine();
        builder.AppendLine(
            "> Advisory AI-assisted review only. Human review remains authoritative; this report does not approve, reject, certify, or change the ADR.");
        builder.AppendLine();

        AppendInputScope(
            builder,
            report.InputScope);

        builder.AppendLine("## Analyzed dimensions");
        builder.AppendLine();

        foreach (var dimension in report.Dimensions)
        {
            builder.Append("### ")
                .AppendLine(dimension.Name);

            if (dimension.Assessments.Length == 0)
            {
                builder.AppendLine(
                    "No follow-up assessment was reported for this dimension.");
                builder.AppendLine();
                continue;
            }

            foreach (var assessment in dimension.Assessments)
            {
                builder.Append("- Classification: ")
                    .AppendLine(
                        assessment.Classification);

                if (!string.IsNullOrWhiteSpace(
                        assessment.FollowUpPriority))
                {
                    builder.Append("  Follow-up priority: ")
                        .AppendLine(
                            assessment.FollowUpPriority);
                }

                if (!string.IsNullOrWhiteSpace(
                        assessment.Uncertainty))
                {
                    builder.Append("  Uncertainty: ")
                        .AppendLine(
                            assessment.Uncertainty);
                }

                builder.Append("  Explanation: ")
                    .AppendLine(
                        assessment.Explanation);

                if (assessment.Evidence.Length == 0)
                {
                    builder.AppendLine(
                        "  Evidence: not available from verified selected sources.");
                }
                else
                {
                    foreach (var evidence in assessment.Evidence)
                    {
                        builder.Append("  Evidence: [")
                            .Append(evidence.SourceId)
                            .Append("] ")
                            .Append(evidence.Path);

                        if (!string.IsNullOrWhiteSpace(
                                evidence.AdrId))
                        {
                            builder.Append(" (ADR ")
                                .Append(evidence.AdrId)
                                .Append(')');
                        }

                        if (evidence.Line is { } line)
                        {
                            builder.Append(':')
                                .Append(line);
                        }

                        builder.AppendLine();

                        if (!string.IsNullOrWhiteSpace(
                                evidence.Excerpt))
                        {
                            builder.Append("  Excerpt: ")
                                .AppendLine(
                                    evidence.Excerpt);
                        }
                    }
                }

                builder.Append("  Guidance: ")
                    .AppendLine(
                        assessment.Guidance);
            }

            builder.AppendLine();
        }

        builder.AppendLine("## Follow-up findings");
        builder.AppendLine();

        if (report.Findings.Length == 0)
        {
            builder.AppendLine(
                "No follow-up findings were produced. This is not an architectural approval.");
        }
        else
        {
            foreach (var finding in report.Findings)
            {
                builder.Append("- ")
                    .Append(finding.Dimension)
                    .Append(" [")
                    .Append(finding.Classification)
                    .Append("] — ")
                    .Append(finding.FollowUpPriority)
                    .AppendLine();
                builder.Append("  ")
                    .AppendLine(finding.Explanation);
                builder.Append("  Guidance: ")
                    .AppendLine(finding.Guidance);
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Unknowns and limitations");
        builder.AppendLine();

        foreach (var limitation in report.Limitations)
        {
            builder.Append("- ")
                .AppendLine(limitation);
        }

        builder.Append("- ")
            .AppendLine(report.CostCaveat);

        var rendered = builder
            .ToString()
            .TrimEnd();

        AdrReviewReportSerializer.EnsureBounded(
            rendered);

        return rendered;
    }

    private static void AppendInputScope(
        StringBuilder builder,
        AdrReviewInputScopeReport inputScope)
    {
        builder.AppendLine("## Input scope");
        builder.AppendLine();

        if (inputScope.ExplicitContext.Length == 0)
        {
            builder.AppendLine(
                "- Explicit context files: none");
        }
        else
        {
            foreach (var source in inputScope.ExplicitContext)
            {
                builder.Append("- Explicit context [")
                    .Append(source.SourceId)
                    .Append("]: ")
                    .AppendLine(source.Path);
            }
        }

        if (!inputScope.IncludeExistingAdrs)
        {
            builder.AppendLine(
                "- Existing ADR context: not enabled");
        }
        else if (inputScope.ExistingAdrs.Length == 0)
        {
            builder.AppendLine(
                "- Existing ADR context: enabled, no candidate ADR was transmitted");
        }
        else
        {
            foreach (var source in inputScope.ExistingAdrs)
            {
                builder.Append("- Existing ADR [")
                    .Append(source.SourceId)
                    .Append("]: ")
                    .AppendLine(source.Path);
            }

            if (inputScope.ExistingAdrSelectionBounded)
            {
                builder.AppendLine(
                    "- Existing ADR selection was bounded by configured review context limits.");
            }
        }

        builder.AppendLine();
    }
}
