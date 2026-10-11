using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AdrGuard.Configuration;

namespace AdrGuard.Drift;

internal static class ExternalFitnessEvidenceAdapter
{
    internal const int MaximumEvidenceBytes = 1_048_576;
    internal const int MaximumFiles = 20;
    internal const int MaximumObservations = 10_000;
    internal static readonly TimeSpan DefaultMaximumAge = TimeSpan.FromDays(30);
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 16,
    };

    internal static ExternalEvidenceSet Load(string repositoryRoot, IReadOnlyList<string> evidencePaths, CancellationToken cancellationToken)
    {
        if (evidencePaths.Count is < 1 or > MaximumFiles) throw new FitnessManifestException($"External evidence requires between 1 and {MaximumFiles} explicit files.");
        var root = Path.GetFullPath(repositoryRoot);
        var observations = new List<ExternalObservation>();
        foreach (var path in evidencePaths.Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalized = NormalizeEvidencePath(root, path);
            var fullPath = RepositoryPath.ResolveContained(root, normalized, false);
            var bytes = File.ReadAllBytes(fullPath);
            if (bytes.Length is 0 or > MaximumEvidenceBytes) throw new FitnessManifestException($"External evidence '{normalized}' is empty or exceeds the {MaximumEvidenceBytes}-byte limit.");
            EvidenceDefinition definition;
            try
            {
                var json = StrictUtf8.GetString(bytes);
                using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
                RejectDuplicates(document.RootElement, "$");
                definition = JsonSerializer.Deserialize<EvidenceDefinition>(json, JsonOptions) ?? throw new JsonException();
            }
            catch (Exception exception) when (exception is JsonException or DecoderFallbackException)
            { throw new FitnessManifestException($"External evidence '{normalized}' is malformed or contains unsupported properties.", exception); }
            if (definition.SchemaVersion != "1.0" || definition.Producer is null || definition.Artifact is null || definition.Observations is null)
                throw new FitnessManifestException($"External evidence '{normalized}' is incomplete or has an unsupported schema version.");
            if (definition.Observations.Length is < 1 or > MaximumObservations || observations.Count + definition.Observations.Length > MaximumObservations)
                throw new FitnessManifestException("External evidence observation count exceeds the bounded limit.");
            var provenance = new ExternalEvidenceProvenance(
                Token(definition.Producer.Name, "producer name", 128), Token(definition.Producer.Version, "producer version", 64),
                Token(definition.Artifact.RunId, "run ID", 256), Token(definition.Artifact.Id, "artifact ID", 256), ValidateSha(definition.Artifact.Sha256), normalized);
            var localIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var observation in definition.Observations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var testId = Token(observation?.TestId, "test ID", 256);
                if (!localIds.Add(testId)) throw new FitnessManifestException($"External evidence '{normalized}' duplicates test ID '{testId}'.");
                if (observation!.Status is not ("passed" or "failed" or "skipped" or "error")) throw new FitnessManifestException($"External evidence test '{testId}' has unsupported status '{observation.Status}'.");
                observations.Add(new(testId, observation.Status!, observation.ObservedAt, provenance));
            }
        }
        return new ExternalEvidenceSet(observations.OrderBy(x => x.TestId, StringComparer.Ordinal).ThenBy(x => x.Provenance.EvidencePath, StringComparer.Ordinal).ToArray());
    }

    internal static IReadOnlyList<FitnessRuleResult> Evaluate(FitnessManifest manifest, ExternalEvidenceSet evidence, DateTimeOffset now, TimeSpan? maximumAge = null)
    {
        var maxAge = maximumAge ?? DefaultMaximumAge;
        return manifest.Rules.Where(x => x.CheckType == FitnessCheckType.ExternalTest).Select(rule =>
        {
            if (rule.Decision.State == FitnessDecisionState.Inactive) return Result(rule, FitnessOutcome.NotApplicable, "none", rule.Decision.Detail!, []);
            if (rule.Decision.State == FitnessDecisionState.Unknown) return Result(rule, FitnessOutcome.Unknown, "none", rule.Decision.Detail!, []);
            var matches = evidence.Observations.Where(x => x.TestId == rule.TestId).ToArray();
            if (matches.Length == 0) return Result(rule, FitnessOutcome.Unknown, "none", "No external observation matches the explicitly mapped test ID.", []);
            var items = matches.Select(item => new FitnessEvidence("external-test-observation", "external-test-report", item.Provenance.EvidencePath, item.Provenance.ArtifactId, null,
                $"Producer {item.Provenance.Producer} {item.Provenance.ProducerVersion}; run {item.Provenance.RunId}; sha256 {item.Provenance.Sha256}; reported {item.Status}; observedAt {(item.ObservedAt?.ToString("O") ?? "unavailable")}.")).ToArray();
            if (matches.Select(x => x.Status).Distinct(StringComparer.Ordinal).Count() > 1)
                return Result(rule, FitnessOutcome.Unknown, "conflicting-external", "External artifacts report contradictory observations.", items);
            if (matches.Any(x => x.ObservedAt is { } timestamp && (timestamp > now.AddMinutes(5) || now - timestamp > maxAge)))
                return Result(rule, FitnessOutcome.Unknown, "stale-external", "External evidence is stale or dated in the future.", items);
            return matches[0].Status switch
            {
                "passed" => Result(rule, FitnessOutcome.Pass, "reported-external", "The external producer reported the mapped test as passed; this is not a local static proof.", items),
                "failed" => Result(rule, FitnessOutcome.Violation, "reported-external", "The external producer reported the mapped test as failed; provenance is preserved but not attested.", items),
                _ => Result(rule, FitnessOutcome.Unknown, "inconclusive-external", $"The external producer reported '{matches[0].Status}'.", items),
            };
        }).OrderBy(x => x.AdrStableId, StringComparer.Ordinal).ThenBy(x => x.RuleId, StringComparer.Ordinal).ToArray();
    }

    private static string NormalizeEvidencePath(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || Uri.TryCreate(path, UriKind.Absolute, out _))
            throw new FitnessManifestException("External evidence paths must be local, normalized, repository-relative JSON paths.");
        try
        {
            var normalized = RepositoryPath.NormalizeRelative(root, path);
            if (normalized != path || !path.EndsWith(".json", StringComparison.Ordinal)) throw new FitnessManifestException($"External evidence path '{path}' is not normalized JSON.");
            return normalized;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException) { throw new FitnessManifestException($"External evidence path '{path}' escapes the repository boundary.", exception); }
    }

    private static string Token(string? value, string description, int max)
    {
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim() || value.Length > max || value.Any(char.IsControl)) throw new FitnessManifestException($"External evidence {description} is invalid.");
        return value;
    }
    private static string ValidateSha(string? value)
    {
        var sha = Token(value, "SHA-256", 64);
        if (sha.Length != 64 || sha.Any(x => !Uri.IsHexDigit(x))) throw new FitnessManifestException("External evidence SHA-256 must contain exactly 64 hexadecimal characters.");
        return sha.ToLowerInvariant();
    }
    private static void RejectDuplicates(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Object) { var names = new HashSet<string>(StringComparer.Ordinal); foreach (var property in element.EnumerateObject()) { if (!names.Add(property.Name)) throw new JsonException($"Duplicate property '{property.Name}' at '{path}'."); RejectDuplicates(property.Value, $"{path}.{property.Name}"); } }
        else if (element.ValueKind == JsonValueKind.Array) { var i = 0; foreach (var item in element.EnumerateArray()) RejectDuplicates(item, $"{path}[{i++}]"); }
    }
    private static FitnessRuleResult Result(FitnessRule rule, FitnessOutcome outcome, string confidence, string explanation, IReadOnlyList<FitnessEvidence> evidence) => new(rule.Id, rule.Decision.StableId, outcome, confidence, explanation, evidence);
    private sealed record EvidenceDefinition(string? SchemaVersion, ProducerDefinition? Producer, ArtifactDefinition? Artifact, ObservationDefinition[]? Observations);
    private sealed record ProducerDefinition(string? Name, string? Version);
    private sealed record ArtifactDefinition(string? RunId, string? Id, string? Sha256);
    private sealed record ObservationDefinition(string? TestId, string? Status, DateTimeOffset? ObservedAt);
}
