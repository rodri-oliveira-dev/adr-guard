namespace AdrGuard.Validation;

internal enum AdrLifecycleKind
{
    Proposed,
    Accepted,
    Rejected,
    Deprecated,
    Superseded,
}

internal sealed class AdrLifecyclePolicy
{
    private const int MaximumConfiguredStatuses = 32;
    private readonly IReadOnlyDictionary<string, AdrLifecycleKind> _statuses;

    internal static AdrLifecyclePolicy Legacy { get; } = new(
        new Dictionary<string, AdrLifecycleKind>(StringComparer.OrdinalIgnoreCase)
        {
            ["Proposed"] = AdrLifecycleKind.Proposed,
            ["Accepted"] = AdrLifecycleKind.Accepted,
            ["Deprecated"] = AdrLifecycleKind.Deprecated,
            ["Superseded"] = AdrLifecycleKind.Superseded,
        });

    private AdrLifecyclePolicy(IReadOnlyDictionary<string, AdrLifecycleKind> statuses) =>
        _statuses = statuses;

    internal IEnumerable<string> StatusNames => _statuses.Keys.Order(StringComparer.OrdinalIgnoreCase);

    internal bool TryResolve(string? value, out AdrLifecycleKind kind) =>
        _statuses.TryGetValue(value?.Trim() ?? string.Empty, out kind);

    internal static AdrLifecyclePolicy Parse(string? specification)
    {
        if (string.IsNullOrWhiteSpace(specification)) return Legacy;
        if (specification.Length > 2048)
            throw new ArgumentException("Lifecycle policy exceeds the 2048-character limit.", nameof(specification));

        var statuses = new Dictionary<string, AdrLifecycleKind>(
            Legacy._statuses,
            StringComparer.OrdinalIgnoreCase);
        var entries = specification.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (entries.Length > MaximumConfiguredStatuses)
            throw new ArgumentException($"Lifecycle policy supports at most {MaximumConfiguredStatuses} configured statuses.", nameof(specification));

        foreach (var entry in entries)
        {
            var separator = entry.IndexOf('=');
            if (separator <= 0 || separator == entry.Length - 1)
                throw new ArgumentException($"Lifecycle entry '{entry}' must use 'Name=semantic-kind'.", nameof(specification));

            var name = entry[..separator].Trim();
            var semantic = entry[(separator + 1)..].Trim();
            if (name.Length is < 1 or > 64 || name.Any(char.IsControl))
                throw new ArgumentException($"Lifecycle status name '{name}' is invalid.", nameof(specification));
            if (!Enum.TryParse<AdrLifecycleKind>(semantic, ignoreCase: true, out var kind))
                throw new ArgumentException($"Lifecycle semantic kind '{semantic}' is invalid.", nameof(specification));
            if (!statuses.TryAdd(name, kind))
                throw new ArgumentException($"Lifecycle status '{name}' duplicates an existing status or alias.", nameof(specification));
        }

        return new AdrLifecyclePolicy(statuses);
    }
}

internal sealed record AdrValidationOptions(
    AdrFormat Format,
    string? RepositoryRoot = null,
    AdrLifecyclePolicy? Lifecycle = null,
    bool ConventionalSupersession = false,
    AdrFilenamePolicy? FilenamePolicy = null,
    PlaceholderPolicy PlaceholderPolicy = PlaceholderPolicy.Off,
    bool ValidateMetadata = false)
{
    internal AdrLifecyclePolicy EffectiveLifecycle => Lifecycle ?? AdrLifecyclePolicy.Legacy;
    internal AdrFilenamePolicy EffectiveFilenamePolicy => FilenamePolicy ?? AdrFilenamePolicy.Canonical;
}

internal enum PlaceholderPolicy { Off, Warn, Error }
