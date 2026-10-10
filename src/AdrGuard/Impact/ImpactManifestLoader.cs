using AdrGuard.Configuration;
using AdrGuard.Parsing;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AdrGuard.Impact;

internal static class ImpactManifestLoader
{
    internal const int MaximumManifestBytes = 1_048_576;
    internal const int MaximumMappings = 1000;
    internal const int MaximumPatternsPerMapping = 100;
    internal const int MaximumPatternLength = 1024;
    internal const int MaximumReasonLength = 2048;

    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 16,
    };

    internal static ImpactMappingManifest Load(
        string repositoryRoot,
        string manifestPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);

        var root = Path.GetFullPath(repositoryRoot);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException(
                $"Impact repository root does not exist: '{root}'.");
        }

        if (Path.IsPathRooted(manifestPath)
            || manifestPath.Contains('\\'))
        {
            throw new ImpactManifestException(
                "Impact manifest path must be repository-relative.");
        }

        string fullManifestPath;
        try
        {
            fullManifestPath = RepositoryPath.ResolveContained(
                root,
                manifestPath,
                allowMissingLeaf: false);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException)
        {
            throw new ImpactManifestException(
                "Impact manifest must stay within the repository and cannot traverse symbolic links or reparse points.",
                exception);
        }

        if (!string.Equals(Path.GetExtension(fullManifestPath), ".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new ImpactManifestException(
                "Impact manifest must use the .json extension.");
        }

        var bytes = File.ReadAllBytes(fullManifestPath);
        if (bytes.Length == 0)
        {
            throw new ImpactManifestException("Impact manifest is empty.");
        }

        if (bytes.Length > MaximumManifestBytes)
        {
            throw new ImpactManifestException(
                $"Impact manifest exceeds the {MaximumManifestBytes}-byte limit.");
        }

        string json;
        try
        {
            json = StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw new ImpactManifestException(
                "Impact manifest must contain valid UTF-8.",
                exception);
        }

        ManifestDefinition? definition;
        try
        {
            using var document = JsonDocument.Parse(
                json,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 16,
                });
            RejectDuplicateProperties(document.RootElement, "$" );
            definition = JsonSerializer.Deserialize<ManifestDefinition>(json, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new ImpactManifestException(
                "Impact manifest JSON is malformed, duplicated, too deeply nested, or contains unsupported properties.",
                exception);
        }

        if (definition is null)
        {
            throw new ImpactManifestException("Impact manifest JSON is empty.");
        }

        return Resolve(root, fullManifestPath, definition);
    }

    private static ImpactMappingManifest Resolve(
        string repositoryRoot,
        string manifestPath,
        ManifestDefinition definition)
    {
        if (!string.Equals(
                definition.SchemaVersion,
                ImpactMappingManifest.CurrentSchemaVersion,
                StringComparison.Ordinal))
        {
            throw new ImpactManifestException(
                $"Unsupported impact manifest schema version '{definition.SchemaVersion ?? "<missing>"}'.");
        }

        if (definition.Mappings is null
            || definition.Mappings.Length == 0
            || definition.Mappings.Length > MaximumMappings)
        {
            throw new ImpactManifestException(
                $"Impact manifest must contain between 1 and {MaximumMappings} mappings.");
        }

        var pathComparer = GetPathComparer();
        var stableIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var decisionPaths = new Dictionary<string, string>(pathComparer);
        var mappings = new List<ImpactMapping>(definition.Mappings.Length);

        foreach (var candidate in definition.Mappings)
        {
            if (candidate?.Decision is null)
            {
                throw new ImpactManifestException("Impact manifest contains a null mapping or decision reference.");
            }

            var stableId = ValidateStableId(candidate.Decision.StableId);
            var decisionPath = NormalizePath(
                repositoryRoot,
                candidate.Decision.Path,
                "ADR path");

            if (stableIds.TryGetValue(stableId, out var existingPath))
            {
                throw new ImpactManifestException(
                    $"Stable ADR identity '{stableId}' is mapped more than once (paths '{existingPath}' and '{decisionPath}').");
            }

            if (decisionPaths.TryGetValue(decisionPath, out var existingStableId))
            {
                throw new ImpactManifestException(
                    $"ADR path '{decisionPath}' is assigned to both '{existingStableId}' and '{stableId}'.");
            }

            stableIds.Add(stableId, decisionPath);
            decisionPaths.Add(decisionPath, stableId);

            var patterns = ValidatePatterns(candidate.Patterns, pathComparer);
            var relationship = ParseRelationship(candidate.Relationship);
            var reason = ValidateReason(candidate.Reason);
            var resolution = ResolveDecision(
                repositoryRoot,
                stableId,
                decisionPath);

            mappings.Add(
                new ImpactMapping(
                    new ImpactDecisionReference(stableId, decisionPath),
                    Array.AsReadOnly(patterns),
                    relationship,
                    reason,
                    resolution));
        }

        return new ImpactMappingManifest(
            ImpactMappingManifest.CurrentSchemaVersion,
            repositoryRoot,
            manifestPath,
            mappings
                .OrderBy(mapping => mapping.Decision.StableId, StringComparer.Ordinal)
                .ThenBy(mapping => mapping.Decision.Path, StringComparer.Ordinal)
                .ToArray());
    }

    private static ImpactDecisionResolution ResolveDecision(
        string repositoryRoot,
        string expectedStableId,
        string decisionPath)
    {
        string fullPath;
        try
        {
            fullPath = RepositoryPath.ResolveContained(
                repositoryRoot,
                decisionPath,
                allowMissingLeaf: true);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException)
        {
            throw new ImpactManifestException(
                $"ADR path '{decisionPath}' escapes the repository boundary.",
                exception);
        }

        if (!File.Exists(fullPath))
        {
            return Unknown(
                $"Mapped ADR path '{decisionPath}' does not exist; the mapping may be stale or renamed.");
        }

        AdrGuard.Model.AdrDocument document;
        try
        {
            document = AdrMarkdownParser.Parse(
                fullPath,
                StrictUtf8.GetString(File.ReadAllBytes(fullPath)));
        }
        catch (Exception exception) when (exception is IOException or DecoderFallbackException)
        {
            return Unknown(
                $"Mapped ADR '{decisionPath}' could not be read as valid UTF-8: {exception.Message}");
        }

        if (!string.Equals(document.StableId, expectedStableId, StringComparison.OrdinalIgnoreCase))
        {
            return Unknown(
                $"Mapped ADR '{decisionPath}' resolves to stable identity '{document.StableId ?? "<unknown>"}', not '{expectedStableId}'.",
                document);
        }

        var status = document.Status?.Trim();
        if (string.Equals(status, "Accepted", StringComparison.OrdinalIgnoreCase))
        {
            return new ImpactDecisionResolution(
                ImpactDecisionResolutionKind.Active,
                status,
                null,
                document);
        }

        if (string.Equals(status, "Proposed", StringComparison.OrdinalIgnoreCase))
        {
            return new ImpactDecisionResolution(
                ImpactDecisionResolutionKind.Proposed,
                status,
                "The decision is proposed and is not an effective architectural approval.",
                document);
        }

        if (status is not null
            && (string.Equals(status, "Rejected", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "Deprecated", StringComparison.OrdinalIgnoreCase)
                || status.StartsWith("Superseded", StringComparison.OrdinalIgnoreCase)))
        {
            return new ImpactDecisionResolution(
                ImpactDecisionResolutionKind.Inactive,
                status,
                $"The decision status '{status}' is not active governance.",
                document);
        }

        return Unknown(
            $"Mapped ADR '{decisionPath}' has unrecognized or missing lifecycle status '{status ?? "<missing>"}'.",
            document);
    }

    private static ImpactDecisionResolution Unknown(
        string detail,
        AdrGuard.Model.AdrDocument? document = null) =>
        new(
            ImpactDecisionResolutionKind.Unknown,
            document?.Status,
            detail,
            document);

    private static string ValidateStableId(string? stableId)
    {
        if (string.IsNullOrWhiteSpace(stableId)
            || stableId.Length > 128
            || !string.Equals(stableId, stableId.Trim(), StringComparison.Ordinal)
            || stableId.Any(char.IsControl))
        {
            throw new ImpactManifestException(
                "Mapping decision stableId must be a non-empty normalized value of at most 128 characters.");
        }

        return stableId;
    }

    private static string[] ValidatePatterns(
        string[]? patterns,
        StringComparer comparer)
    {
        if (patterns is null
            || patterns.Length == 0
            || patterns.Length > MaximumPatternsPerMapping)
        {
            throw new ImpactManifestException(
                $"Each impact mapping must contain between 1 and {MaximumPatternsPerMapping} patterns.");
        }

        var unique = new HashSet<string>(comparer);
        var normalized = new List<string>(patterns.Length);

        foreach (var pattern in patterns)
        {
            ValidatePattern(pattern);
            if (!unique.Add(pattern))
            {
                throw new ImpactManifestException(
                    $"Impact mapping pattern '{pattern}' is duplicated for the effective platform path rules.");
            }

            normalized.Add(pattern);
        }

        return normalized.Order(StringComparer.Ordinal).ToArray();
    }

    private static void ValidatePattern(string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern)
            || pattern.Length > MaximumPatternLength
            || pattern != pattern.Trim()
            || pattern.Contains('\\')
            || pattern.StartsWith('/')
            || pattern.EndsWith('/')
            || pattern.Contains("//", StringComparison.Ordinal)
            || pattern.Any(character => char.IsControl(character)
                || character is '?' or '[' or ']' or '{' or '}' or '!'))
        {
            throw new ImpactManifestException(
                $"Impact mapping pattern '{pattern ?? "<null>"}' is invalid. Use normalized repository-relative paths with only literal segments, '*', or a complete '**' segment.");
        }

        var wildcardCount = 0;
        foreach (var segment in pattern.Split('/'))
        {
            if (segment is "." or ".." || segment.Length == 0)
            {
                throw new ImpactManifestException(
                    $"Impact mapping pattern '{pattern}' contains an unsafe path segment.");
            }

            wildcardCount += segment.Count(character => character == '*');
            if (segment.Contains("**", StringComparison.Ordinal)
                && segment != "**")
            {
                throw new ImpactManifestException(
                    $"Impact mapping pattern '{pattern}' may use '**' only as a complete path segment.");
            }
        }

        if (wildcardCount > 32)
        {
            throw new ImpactManifestException(
                $"Impact mapping pattern '{pattern}' exceeds the wildcard complexity limit.");
        }
    }

    private static string NormalizePath(
        string repositoryRoot,
        string? relativePath,
        string description)
    {
        if (string.IsNullOrWhiteSpace(relativePath)
            || relativePath.Length > MaximumPatternLength
            || relativePath.Contains('\\')
            || relativePath.Any(char.IsControl))
        {
            throw new ImpactManifestException(
                $"Mapping {description} must be a normalized forward-slash repository-relative path.");
        }

        string normalized;
        try
        {
            normalized = RepositoryPath.NormalizeRelative(repositoryRoot, relativePath);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException)
        {
            throw new ImpactManifestException(
                $"Mapping {description} '{relativePath}' must stay inside the repository boundary.",
                exception);
        }

        if (!string.Equals(relativePath, normalized, StringComparison.Ordinal))
        {
            throw new ImpactManifestException(
                $"Mapping {description} '{relativePath}' is not normalized; use '{normalized}'.");
        }

        return normalized;
    }

    private static ImpactRelationship ParseRelationship(string? relationship) => relationship switch
    {
        "governs" => ImpactRelationship.Governs,
        "implements" => ImpactRelationship.Implements,
        "constrains" => ImpactRelationship.Constrains,
        "depends-on" => ImpactRelationship.DependsOn,
        _ => throw new ImpactManifestException(
            $"Unsupported impact relationship '{relationship ?? "<missing>"}'."),
    };

    private static string ValidateReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason)
            || reason.Length > MaximumReasonLength
            || reason != reason.Trim()
            || reason.Any(character => character == '\0'))
        {
            throw new ImpactManifestException(
                $"Impact mapping reason must be non-empty, normalized, and at most {MaximumReasonLength} characters.");
        }

        return reason;
    }

    private static void RejectDuplicateProperties(
        JsonElement element,
        string path)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new JsonException(
                        $"Duplicate JSON property '{property.Name}' at '{path}'.");
                }

                RejectDuplicateProperties(
                    property.Value,
                    $"{path}.{property.Name}");
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in element.EnumerateArray())
            {
                RejectDuplicateProperties(item, $"{path}[{index++}]");
            }
        }
    }

    private static StringComparer GetPathComparer() =>
        OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    private sealed record ManifestDefinition(
        string? SchemaVersion,
        MappingDefinition[]? Mappings);

    private sealed record MappingDefinition(
        DecisionDefinition? Decision,
        string[]? Patterns,
        string? Relationship,
        string? Reason);

    private sealed record DecisionDefinition(
        string? StableId,
        string? Path);
}
