using AdrGuard.Generation;
using AdrGuard.Model;
using System.Text;

namespace AdrGuard.Review.Security;

internal sealed class AdrReviewSecurityBoundary
{
    private const string RedactedValue = "[REDACTED]";

    internal static readonly string[] CredentialEnvironmentVariableNames =
    [
        "OPENAI_API_KEY",
        "ANTHROPIC_API_KEY",
        "GEMINI_API_KEY",
        "ADR_GUARD_OPENAI_COMPATIBLE_API_KEY",
        "GITHUB_TOKEN",
        "GH_TOKEN",
    ];

    private readonly string[] _credentialValues;

    internal AdrReviewSecurityBoundary(
        IReadOnlyCollection<string> credentialValues)
    {
        ArgumentNullException.ThrowIfNull(credentialValues);

        _credentialValues = credentialValues
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(value => value.Length)
            .ToArray();
    }

    internal static string[] ReadCredentialValues(
        Func<string, string?>? environmentVariableReader = null)
    {
        var reader = environmentVariableReader
            ?? Environment.GetEnvironmentVariable;

        return CredentialEnvironmentVariableNames
            .Select(reader)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(value => value.Length)
            .ToArray();
    }

    internal AdrReviewContext SanitizeContext(
        AdrReviewContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var explicitFiles = context.ExplicitFiles
            .Select(file =>
                new ExplicitContextFile(
                    SanitizeDisplayName(
                        Path.GetFileName(file.FilePath)),
                    Redact(file.Content)))
            .ToArray();

        ExistingAdrContext? existing = null;

        if (context.ExistingAdrs is { } existingContext)
        {
            existing = existingContext with
            {
                Content = Redact(existingContext.Content),
                IncludedSourceNames = existingContext
                    .IncludedSourceNames
                    .Select(SanitizeDisplayName)
                    .ToArray(),
            };
        }

        AdrCrossAdrEvidence? crossAdr = null;

        if (context.CrossAdrEvidence is { } crossAdrEvidence)
        {
            crossAdr = crossAdrEvidence with
            {
                Content = Redact(crossAdrEvidence.Content),
                IncludedSourceNames = crossAdrEvidence
                    .IncludedSourceNames
                    .Select(SanitizeDisplayName)
                    .ToArray(),
            };
        }

        var comparison = context.Comparison is null
            ? null
            : context.Comparison with
            {
                Reference = SanitizeDisplayName(context.Comparison.Reference),
                FileName = SanitizeDisplayName(context.Comparison.FileName),
                Content = Redact(context.Comparison.Content),
            };

        return context with
        {
            TargetSourceName =
                SanitizeDisplayName(context.TargetSourceName),
            TargetMarkdown = Redact(context.TargetMarkdown),
            ExplicitFiles = explicitFiles,
            ExistingAdrs = existing,
            CrossAdrEvidence = crossAdr,
            Comparison = comparison,
        };
    }

    internal AdrDocument SanitizeDocumentMetadata(
        AdrDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return document with
        {
            FileName = SanitizeDisplayName(document.FileName),
            Title = SanitizeOptional(document.Title),
            Status = SanitizeOptional(document.Status),
        };
    }

    internal AdrReviewResult SanitizeResult(
        AdrReviewResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new AdrReviewResult(
            result.Findings
                .Select(finding =>
                    new AdrReviewFinding(
                        finding.Dimension,
                        finding.Classification,
                        SanitizeOptional(
                            finding.Source,
                            AdrReviewSecurityLimits.MaximumSourceCharacters),
                        SanitizeOptional(
                            finding.Excerpt,
                            AdrReviewSecurityLimits.MaximumExcerptCharacters),
                        SanitizeRequired(
                            finding.Explanation,
                            AdrReviewSecurityLimits.MaximumExplanationCharacters),
                        SanitizeRequired(
                            finding.Guidance,
                            AdrReviewSecurityLimits.MaximumGuidanceCharacters)))
                .ToArray());
    }

    internal string Redact(
        string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var redacted = value;

        foreach (var credential in _credentialValues)
        {
            redacted = redacted.Replace(
                credential,
                RedactedValue,
                StringComparison.Ordinal);
        }

        return redacted;
    }

    internal string SanitizeDiagnostic(
        string value) =>
        SanitizeUntrustedText(
            Redact(value),
            AdrReviewSecurityLimits.MaximumDiagnosticCharacters,
            rejectOversized: false);

    private string SanitizeDisplayName(
        string value) =>
        SanitizeUntrustedText(
            Redact(value),
            AdrReviewSecurityLimits.MaximumSourceCharacters,
            rejectOversized: false);

    private string? SanitizeOptional(
        string? value,
        int maximumCharacters =
            AdrReviewSecurityLimits.MaximumMetadataCharacters)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return SanitizeUntrustedText(
            Redact(value),
            maximumCharacters,
            rejectOversized: true);
    }

    private string SanitizeRequired(
        string value,
        int maximumCharacters) =>
        SanitizeUntrustedText(
            Redact(value),
            maximumCharacters,
            rejectOversized: true);

    internal static string SanitizeUntrustedText(
        string value,
        int maximumCharacters,
        bool rejectOversized)
    {
        ArgumentNullException.ThrowIfNull(value);

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            maximumCharacters);

        var builder = new StringBuilder(
            Math.Min(
                value.Length,
                maximumCharacters));

        foreach (var character in value)
        {
            if (character is '\r' or '\n' or '\t'
                or '\u0085' or '\u2028' or '\u2029'
                || char.IsControl(character))
            {
                if (builder.Length == 0
                    || builder[^1] != ' ')
                {
                    builder.Append(' ');
                }

                continue;
            }

            builder.Append(character);
        }

        var normalized = builder
            .ToString()
            .Trim()
            .Replace(
                "::",
                ":\u200B:",
                StringComparison.Ordinal);

        if (normalized.Length <= maximumCharacters)
        {
            return normalized;
        }

        if (rejectOversized)
        {
            throw new InvalidDataException(
                $"Provider review text exceeds the {maximumCharacters}-character field safety limit.");
        }

        return normalized[..maximumCharacters];
    }
}

internal static class AdrReviewSecurityLimits
{
    internal const int MaximumSourceCharacters = 512;
    internal const int MaximumExcerptCharacters = 2000;
    internal const int MaximumExplanationCharacters = 4000;
    internal const int MaximumGuidanceCharacters = 4000;
    internal const int MaximumMetadataCharacters = 1000;
    internal const int MaximumDiagnosticCharacters = 4000;
}
