using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AdrGuard.Configuration;

namespace AdrGuard.Drift;

internal static class DriftSnapshotService
{
    internal const int MaximumBytes = 4 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        NewLine = "\n",
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 16,
    };

    internal static string Serialize(DriftSnapshot snapshot)
    {
        Validate(snapshot);
        return JsonSerializer.Serialize(snapshot, JsonOptions).ReplaceLineEndings("\n") + "\n";
    }

    internal static DriftSnapshot Load(string repositoryRoot, string relativePath)
    {
        var fullPath = Resolve(repositoryRoot, relativePath, false);
        var bytes = File.ReadAllBytes(fullPath);
        if (bytes.Length is 0 or > MaximumBytes) throw new InvalidDataException("Drift baseline is empty or exceeds the bounded size limit.");
        try
        {
            var text = new UTF8Encoding(false, true).GetString(bytes);
            using var document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 16 });
            RejectDuplicates(document.RootElement, "$");
            var snapshot = JsonSerializer.Deserialize<DriftSnapshot>(text, JsonOptions) ?? throw new JsonException();
            Validate(snapshot);
            return snapshot;
        }
        catch (Exception exception) when (exception is JsonException or DecoderFallbackException) { throw new InvalidDataException("Drift baseline is malformed, duplicated, or not valid UTF-8.", exception); }
    }

    internal static string Write(string repositoryRoot, string relativePath, DriftSnapshot snapshot, bool update)
    {
        var fullPath = Resolve(repositoryRoot, relativePath, true);
        var directory = Path.GetDirectoryName(fullPath)!;
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException($"Drift baseline directory does not exist: '{directory}'.");
        if (File.Exists(fullPath))
        {
            if ((File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Drift baseline cannot replace a symbolic link or reparse point.");
            if (!update) throw new InvalidOperationException("Drift baseline already exists; an explicit update is required.");
        }
        var temporary = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporary, Serialize(snapshot), new UTF8Encoding(false));
            File.Move(temporary, fullPath, overwrite: update);
            return fullPath;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static string Resolve(string root, string path, bool allowMissing)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || !path.EndsWith(".json", StringComparison.Ordinal)) throw new InvalidDataException("Drift baseline path must be normalized repository-relative JSON.");
        try
        {
            var repository = Path.GetFullPath(root);
            var full = RepositoryPath.ResolveContained(repository, path, allowMissing);
            if (Path.GetRelativePath(repository, full).Replace(Path.DirectorySeparatorChar, '/') != path) throw new InvalidDataException("Drift baseline path is not normalized.");
            return full;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException) { throw new InvalidDataException("Drift baseline path escapes the repository or traverses a symbolic link.", exception); }
    }

    private static void Validate(DriftSnapshot snapshot)
    {
        if (snapshot.SchemaVersion != DriftSnapshot.CurrentSchemaVersion || snapshot.Repository is null || string.IsNullOrWhiteSpace(snapshot.Repository.RulesManifest) || snapshot.Findings is null) throw new InvalidDataException("Drift baseline contract is incomplete or unsupported.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var finding in snapshot.Findings)
        {
            if (finding is null || string.IsNullOrEmpty(finding.Fingerprint) || finding.Fingerprint.Length != 64 || finding.Fingerprint.Any(x => !char.IsAsciiHexDigit(x) || char.IsUpper(x)) || !ids.Add(finding.Fingerprint)
                || finding.Outcome is not ("pass" or "violation" or "unknown" or "not-applicable")
                || finding.CheckType is not ("forbid-project-reference" or "external-test") || string.IsNullOrWhiteSpace(finding.Confidence)
                || string.IsNullOrWhiteSpace(finding.RuleId) || string.IsNullOrWhiteSpace(finding.AdrStableId))
                throw new InvalidDataException("Drift baseline contains an invalid or duplicate finding.");
        }
        var sorted = snapshot.Findings.OrderBy(x => x.AdrStableId, StringComparer.Ordinal).ThenBy(x => x.RuleId, StringComparer.Ordinal).ThenBy(x => x.Fingerprint, StringComparer.Ordinal);
        if (!snapshot.Findings.SequenceEqual(sorted)) throw new InvalidDataException("Drift baseline findings must use stable ordering.");
    }

    private static void RejectDuplicates(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Object) { var names = new HashSet<string>(StringComparer.Ordinal); foreach (var property in element.EnumerateObject()) { if (!names.Add(property.Name)) throw new JsonException($"Duplicate property '{property.Name}' at '{path}'."); RejectDuplicates(property.Value, $"{path}.{property.Name}"); } }
        else if (element.ValueKind == JsonValueKind.Array) { var i = 0; foreach (var item in element.EnumerateArray()) RejectDuplicates(item, $"{path}[{i++}]"); }
    }
}
