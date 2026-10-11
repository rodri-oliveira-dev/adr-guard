using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AdrGuard.Configuration;
using AdrGuard.Parsing;
using AdrGuard.Validation;

namespace AdrGuard.Drift;

internal static class FitnessManifestLoader
{
    internal const int MaximumBytes = 1_048_576;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 16,
    };

    internal static FitnessManifest Load(string repositoryRoot, string manifestPath)
    {
        var root = Path.GetFullPath(repositoryRoot);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException($"Fitness repository root does not exist: '{root}'.");
        var fullPath = ResolveJson(root, manifestPath, "Fitness manifest");
        var definition = ReadJson<ManifestDefinition>(fullPath, "Fitness manifest");
        if (definition.SchemaVersion != FitnessManifest.SchemaVersion)
            throw new FitnessManifestException($"Unsupported fitness manifest schema version '{definition.SchemaVersion ?? "<missing>"}'.");
        if (definition.Rules is null || definition.Rules.Length is < 1 or > 1000)
            throw new FitnessManifestException("Fitness manifest must contain between 1 and 1000 rules.");

        var configuration = AdrGuardConfigurationLoader.Load(root);
        var format = configuration?.AdrFormat == "madr-4" ? AdrFormat.Madr4 : AdrFormat.Canonical;
        AdrLifecyclePolicy lifecycle;
        try { lifecycle = AdrLifecyclePolicy.Parse(configuration?.LifecycleStatuses); }
        catch (ArgumentException exception) { throw new FitnessManifestException(exception.Message, exception); }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var rules = new List<FitnessRule>(definition.Rules.Length);
        foreach (var candidate in definition.Rules)
        {
            if (candidate is null || candidate.Decision is null || candidate.Scope is null || candidate.Check is null || candidate.Evidence is null || candidate.Failure is null)
                throw new FitnessManifestException("Fitness manifest contains an incomplete rule.");
            var id = Token(candidate.Id, "rule ID", 128);
            if (!ids.Add(id) || !System.Text.RegularExpressions.Regex.IsMatch(id, "^[a-z0-9][a-z0-9._-]*$"))
                throw new FitnessManifestException($"Fitness rule ID '{id}' is invalid or duplicated.");
            var decisionPath = Normalize(root, candidate.Decision.Path, ".md", "ADR path");
            var projects = NormalizeProjects(root, candidate.Scope.Projects);
            var type = candidate.Check.Type switch
            {
                "forbid-project-reference" => FitnessCheckType.ForbidProjectReference,
                "external-test" => FitnessCheckType.ExternalTest,
                _ => throw new FitnessManifestException($"Unsupported fitness check type '{candidate.Check.Type ?? "<missing>"}'."),
            };
            string? from = null; string? to = null; string? testId = null;
            if (type == FitnessCheckType.ForbidProjectReference)
            {
                from = Normalize(root, candidate.Check.From, ".csproj", "check from path");
                to = Normalize(root, candidate.Check.To, ".csproj", "check to path");
                if (candidate.Check.TestId is not null || !projects.Contains(from, PathComparer) || !projects.Contains(to, PathComparer))
                    throw new FitnessManifestException($"Rule '{id}' must declare exact from/to projects within its scope.");
                if (candidate.Evidence.Source != "dotnet-project-reference")
                    throw new FitnessManifestException($"Rule '{id}' has an incompatible evidence source.");
            }
            else
            {
                testId = Token(candidate.Check.TestId, "external test ID", 256);
                if (candidate.Check.From is not null || candidate.Check.To is not null || candidate.Evidence.Source != "external-test-report")
                    throw new FitnessManifestException($"Rule '{id}' has an incompatible external-test contract.");
            }
            if (candidate.Failure.OnInconclusive != "unknown" || candidate.Failure.Severity is not ("error" or "warning" or "notice"))
                throw new FitnessManifestException($"Rule '{id}' has unsupported failure semantics.");
            if (candidate.Gate is null)
                throw new FitnessManifestException($"Rule '{id}' must declare whether it gates enforcement.");
            rules.Add(new FitnessRule(id, ResolveDecision(root, Token(candidate.Decision.StableId, "ADR stable ID", 128), decisionPath, lifecycle, format), projects, type, from, to, testId, candidate.Evidence.Source!, candidate.Failure.Severity, candidate.Gate.Value));
        }

        return new FitnessManifest(root, Path.GetRelativePath(root, fullPath).Replace(Path.DirectorySeparatorChar, '/'), rules.OrderBy(x => x.Decision.StableId, StringComparer.Ordinal).ThenBy(x => x.Id, StringComparer.Ordinal).ToArray());
    }

    private static FitnessDecision ResolveDecision(string root, string stableId, string path, AdrLifecyclePolicy lifecycle, AdrFormat format)
    {
        string fullPath;
        try { fullPath = RepositoryPath.ResolveContained(root, path, false); }
        catch (Exception exception) when (exception is ArgumentException or IOException) { return new(stableId, path, FitnessDecisionState.Unknown, null, exception.Message); }
        try
        {
            var bytes = File.ReadAllBytes(fullPath);
            if (bytes.Length > MaximumBytes) return new(stableId, path, FitnessDecisionState.Unknown, null, "ADR exceeds the bounded input limit.");
            var document = AdrStatusResolver.ForFormat([AdrMarkdownParser.Parse(fullPath, StrictUtf8.GetString(bytes))], format)[0];
            if (!string.Equals(document.StableId, stableId, StringComparison.OrdinalIgnoreCase))
                return new(stableId, path, FitnessDecisionState.Unknown, document.Status, $"ADR resolves to '{document.StableId ?? "<unknown>"}'.");
            if (!lifecycle.TryResolve(document.Status, out var kind))
            {
                if (string.Equals(document.Status, "Rejected", StringComparison.OrdinalIgnoreCase)
                    || document.Status?.StartsWith("Superseded", StringComparison.OrdinalIgnoreCase) == true)
                    return new(stableId, path, FitnessDecisionState.Inactive, document.Status, $"ADR status '{document.Status}' is not effective governance.");
                return new(stableId, path, FitnessDecisionState.Unknown, document.Status, "ADR lifecycle status is missing or unrecognized.");
            }
            return kind == AdrLifecycleKind.Accepted
                ? new(stableId, path, FitnessDecisionState.Effective, document.Status, null)
                : new(stableId, path, FitnessDecisionState.Inactive, document.Status, $"ADR status '{document.Status}' is not effective governance.");
        }
        catch (Exception exception) when (exception is IOException or DecoderFallbackException)
        { return new(stableId, path, FitnessDecisionState.Unknown, null, exception.Message); }
    }

    private static string[] NormalizeProjects(string root, string[]? projects)
    {
        if (projects is null || projects.Length is < 1 or > 100) throw new FitnessManifestException("Rule scope must contain between 1 and 100 projects.");
        var normalized = projects.Select(x => Normalize(root, x, ".csproj", "scope project path")).ToArray();
        if (normalized.Distinct(PathComparer).Count() != normalized.Length) throw new FitnessManifestException("Rule scope contains duplicate project paths.");
        return normalized.Order(StringComparer.Ordinal).ToArray();
    }

    private static string Normalize(string root, string? path, string extension, string description)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 1024 || path.Contains('\\') || Path.IsPathRooted(path))
            throw new FitnessManifestException($"Fitness {description} must be a normalized repository-relative path.");
        try
        {
            var normalized = RepositoryPath.NormalizeRelative(root, path);
            if (normalized != path || !path.EndsWith(extension, StringComparison.Ordinal)) throw new FitnessManifestException($"Fitness {description} '{path}' is not normalized or does not end in '{extension}'.");
            return normalized;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException) { throw new FitnessManifestException($"Fitness {description} '{path}' escapes the repository boundary.", exception); }
    }

    private static string Token(string? value, string description, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim() || value.Length > maximum || value.Any(char.IsControl)) throw new FitnessManifestException($"Fitness {description} is invalid.");
        return value;
    }

    private static string ResolveJson(string root, string path, string description)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\')) throw new FitnessManifestException($"{description} path must be repository-relative.");
        try
        {
            var full = RepositoryPath.ResolveContained(root, path, false);
            if (!full.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) throw new FitnessManifestException($"{description} must use the .json extension.");
            return full;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException) { throw new FitnessManifestException($"{description} must stay within the repository.", exception); }
    }

    private static T ReadJson<T>(string path, string description)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length is 0 or > MaximumBytes) throw new FitnessManifestException($"{description} is empty or exceeds the {MaximumBytes}-byte limit.");
        try
        {
            var json = StrictUtf8.GetString(bytes);
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
            RejectDuplicates(document.RootElement, "$");
            return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? throw new JsonException("Empty document.");
        }
        catch (Exception exception) when (exception is JsonException or DecoderFallbackException) { throw new FitnessManifestException($"{description} is malformed or contains unsupported properties.", exception); }
    }

    private static void RejectDuplicates(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            { if (!names.Add(property.Name)) throw new JsonException($"Duplicate property '{property.Name}' at '{path}'."); RejectDuplicates(property.Value, $"{path}.{property.Name}"); }
        }
        else if (element.ValueKind == JsonValueKind.Array) { var i = 0; foreach (var item in element.EnumerateArray()) RejectDuplicates(item, $"{path}[{i++}]"); }
    }

    private static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private sealed record ManifestDefinition(string? SchemaVersion, RuleDefinition[]? Rules);
    private sealed record RuleDefinition(string? Id, DecisionDefinition? Decision, ScopeDefinition? Scope, CheckDefinition? Check, EvidenceDefinition? Evidence, FailureDefinition? Failure, bool? Gate);
    private sealed record DecisionDefinition(string? StableId, string? Path);
    private sealed record ScopeDefinition(string[]? Projects);
    private sealed record CheckDefinition(string? Type, string? From, string? To, string? TestId);
    private sealed record EvidenceDefinition(string? Source);
    private sealed record FailureDefinition(string? Severity, string? OnInconclusive);
}
