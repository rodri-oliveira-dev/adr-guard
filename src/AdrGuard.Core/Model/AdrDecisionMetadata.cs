namespace AdrGuard.Model;

internal sealed record AdrDecisionMetadata(
    string? DecisionDate,
    string? LastReviewedDate,
    string? Owner,
    IReadOnlyList<string> DecisionMakers,
    IReadOnlyList<string> Stakeholders,
    IReadOnlyList<string> Requirements,
    IReadOnlyList<string> FollowUps,
    string? ReviewTrigger,
    string? Category)
{
    internal static AdrDecisionMetadata From(IReadOnlyDictionary<string, string> values) => new(
        Get(values, "date"),
        Get(values, "last-reviewed"),
        Get(values, "owner"),
        GetList(values, "decision-makers"),
        GetList(values, "stakeholders", "consulted", "informed"),
        GetList(values, "requirements"),
        GetList(values, "follow-ups"),
        Get(values, "review-trigger"),
        Get(values, "category"));

    private static string? Get(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    private static string[] GetList(IReadOnlyDictionary<string, string> values, params string[] keys) =>
        keys.SelectMany(key => Get(values, key)?.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [])
            .Distinct(StringComparer.Ordinal).ToArray();
}
