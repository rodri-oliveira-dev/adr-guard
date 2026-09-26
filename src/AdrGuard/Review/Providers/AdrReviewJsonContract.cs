using AdrGuard.Generation.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AdrGuard.Review.Providers;

internal static class AdrReviewJsonContract
{
    internal const int MaximumFindings = 64;
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    internal static readonly string[] Classifications =
    [
        "observed-evidence",
        "potential-risk",
        "missing-context",
        "recommendation-for-human-investigation",
        "not-applicable",
    ];

    internal static ReviewJsonSchema BuildSchema()
    {
        var findingProperties =
            new Dictionary<string, ReviewJsonSchemaProperty>(
                StringComparer.Ordinal)
            {
                ["dimension"] = new("string"),
                ["classification"] = new("string"),
                ["source"] = new("string"),
                ["excerpt"] = new("string"),
                ["explanation"] = new("string"),
                ["guidance"] = new("string"),
            };

        var findingSchema = new ReviewJsonSchema(
            "object",
            findingProperties,
            ["dimension", "classification", "source", "excerpt", "explanation", "guidance"],
            AdditionalProperties: false);

        return new ReviewJsonSchema(
            "object",
            new Dictionary<string, ReviewJsonSchemaProperty>(
                StringComparer.Ordinal)
            {
                ["findings"] = new("array", findingSchema),
            },
            ["findings"],
            AdditionalProperties: false);
    }

    internal static AdrReviewResult Parse(
        string json,
        string providerDisplayName)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw InvalidResponse(
                $"{providerDisplayName} returned an empty review response.");
        }

        ReviewPayload? payload;

        try
        {
            payload = JsonSerializer.Deserialize<ReviewPayload>(
                json,
                JsonOptions);
        }
        catch (JsonException)
        {
            throw InvalidResponse(
                $"{providerDisplayName} returned malformed review JSON.");
        }

        if (payload?.Findings is not { Length: > 0 })
        {
            throw InvalidResponse(
                $"{providerDisplayName} returned review JSON without findings.");
        }

        if (payload.Findings.Length > MaximumFindings)
        {
            throw InvalidResponse(
                $"{providerDisplayName} returned more than the {MaximumFindings}-finding review limit.");
        }

        var findings = new List<AdrReviewFinding>(
            payload.Findings.Length);

        foreach (var finding in payload.Findings)
        {
            if (finding is null
                || string.IsNullOrWhiteSpace(finding.Dimension)
                || !AdrReviewContract.Dimensions.Contains(
                    finding.Dimension,
                    StringComparer.Ordinal)
                || string.IsNullOrWhiteSpace(finding.Classification)
                || !Classifications.Contains(
                    finding.Classification,
                    StringComparer.Ordinal)
                || string.IsNullOrWhiteSpace(finding.Explanation)
                || string.IsNullOrWhiteSpace(finding.Guidance))
            {
                throw InvalidResponse(
                    $"{providerDisplayName} returned an invalid review finding.");
            }

            if (string.Equals(
                    finding.Classification,
                    "missing-context",
                    StringComparison.Ordinal)
                && !finding.Explanation.Contains(
                    "not enough information",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw InvalidResponse(
                    $"{providerDisplayName} returned a missing-context finding without explicit uncertainty.");
            }

            findings.Add(
                new AdrReviewFinding(
                    finding.Dimension.Trim(),
                    finding.Classification.Trim(),
                    NormalizeOptional(finding.Source),
                    NormalizeOptional(finding.Excerpt),
                    finding.Explanation.Trim(),
                    finding.Guidance.Trim()));
        }

        var missingDimensions = AdrReviewContract.Dimensions
            .Where(dimension => !findings.Any(finding =>
                string.Equals(
                    finding.Dimension,
                    dimension,
                    StringComparison.Ordinal)))
            .ToArray();

        if (missingDimensions.Length > 0)
        {
            throw InvalidResponse(
                $"{providerDisplayName} review omitted required dimensions: {string.Join(", ", missingDimensions)}.");
        }

        return new AdrReviewResult(findings);
    }

    internal static string RemoveSingleJsonFence(string content)
    {
        var trimmed = content.Trim();

        if (!trimmed.StartsWith("```", StringComparison.Ordinal)
            || !trimmed.EndsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var firstLineEnd = trimmed.IndexOf('\n');
        if (firstLineEnd < 0)
        {
            return trimmed;
        }

        var openingFence = trimmed[..firstLineEnd]
            .TrimEnd('\r')
            .Trim();

        if (openingFence is not "```"
            && !string.Equals(
                openingFence,
                "```json",
                StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        return trimmed[(firstLineEnd + 1)..^3].Trim();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();

    private static AiProviderException InvalidResponse(
        string message) =>
        new(
            AiProviderErrorKind.InvalidResponse,
            message);

    internal sealed record ReviewJsonSchema(
        string Type,
        IReadOnlyDictionary<string, ReviewJsonSchemaProperty> Properties,
        string[] Required,
        bool AdditionalProperties);

    internal sealed record ReviewJsonSchemaProperty(
        string Type,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        ReviewJsonSchema? Items = null);

    private sealed class ReviewPayload
    {
        public ReviewFindingPayload[]? Findings { get; init; }
    }

    private sealed class ReviewFindingPayload
    {
        public string? Dimension { get; init; }

        public string? Classification { get; init; }

        public string? Source { get; init; }

        public string? Excerpt { get; init; }

        public string? Explanation { get; init; }

        public string? Guidance { get; init; }
    }
}
