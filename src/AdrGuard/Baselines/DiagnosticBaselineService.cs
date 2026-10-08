using AdrGuard.Validation;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AdrGuard.Baselines;

internal static class DiagnosticBaselineService
{
    internal const string SchemaVersion = "1.0";
    private const int MaximumBytes = 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };

    internal static DiagnosticBaseline Create(
        ValidationResult result,
        string checkedDirectory)
    {
        ArgumentNullException.ThrowIfNull(result);
        var entries = result.Issues
            .Select(issue => CreateEntry(issue, checkedDirectory))
            .DistinctBy(entry => entry.Fingerprint, StringComparer.Ordinal)
            .OrderBy(entry => entry.Fingerprint, StringComparer.Ordinal)
            .ToArray();
        return new DiagnosticBaseline(SchemaVersion, entries);
    }

    internal static DiagnosticBaselineComparison Compare(
        ValidationResult current,
        DiagnosticBaseline baseline,
        string checkedDirectory)
    {
        ArgumentNullException.ThrowIfNull(current);
        Validate(baseline);

        var baselineByFingerprint = baseline.Diagnostics.ToDictionary(
            entry => entry.Fingerprint,
            StringComparer.Ordinal);
        var currentEntries = current.Issues
            .Select(issue => (Issue: issue, Entry: CreateEntry(issue, checkedDirectory)))
            .ToArray();
        var currentFingerprints = currentEntries
            .Select(item => item.Entry.Fingerprint)
            .ToHashSet(StringComparer.Ordinal);
        var newIssues = new List<ValidationIssue>();
        var existingIssues = new List<ValidationIssue>();
        var states = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var item in currentEntries)
        {
            var existing = baselineByFingerprint.ContainsKey(item.Entry.Fingerprint)
                && !IncrementalValidation.IsGlobalCode(item.Issue.Code);
            if (existing)
            {
                existingIssues.Add(item.Issue);
                states[item.Entry.Fingerprint] = "existing";
            }
            else
            {
                newIssues.Add(item.Issue);
                states[item.Entry.Fingerprint] = "new";
            }
        }

        var resolved = baseline.Diagnostics
            .Where(entry => !currentFingerprints.Contains(entry.Fingerprint))
            .OrderBy(entry => entry.Fingerprint, StringComparer.Ordinal)
            .ToArray();
        return new DiagnosticBaselineComparison(
            newIssues,
            existingIssues,
            resolved,
            states);
    }

    internal static string Serialize(DiagnosticBaseline baseline)
    {
        Validate(baseline);
        return JsonSerializer.Serialize(baseline, JsonOptions).ReplaceLineEndings("\n") + "\n";
    }

    internal static DiagnosticBaseline Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Diagnostic baseline does not exist.", fullPath);
        }

        if ((File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("Diagnostic baseline must not be a symbolic link or reparse point.");
        }

        var bytes = File.ReadAllBytes(fullPath);
        if (bytes.Length > MaximumBytes)
        {
            throw new InvalidDataException($"Diagnostic baseline exceeds the {MaximumBytes}-byte limit.");
        }

        try
        {
            var utf8 = new UTF8Encoding(false, true);
            var baseline = JsonSerializer.Deserialize<DiagnosticBaseline>(
                utf8.GetString(bytes),
                JsonOptions)
                ?? throw new InvalidDataException("Diagnostic baseline is empty.");
            Validate(baseline);
            return baseline;
        }
        catch (Exception exception) when (exception is JsonException or DecoderFallbackException)
        {
            throw new InvalidDataException("Diagnostic baseline is malformed or not valid UTF-8.", exception);
        }
    }

    internal static string Write(string path, DiagnosticBaseline baseline, bool update)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        if (!string.Equals(Path.GetExtension(fullPath), ".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Diagnostic baseline output must use a .json extension.");
        }

        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"Baseline output directory does not exist: '{directory}'.");
        }

        if (File.Exists(fullPath))
        {
            if ((File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException("Diagnostic baseline output cannot replace a symbolic link or reparse point.");
            }

            if (!update)
            {
                throw new InvalidOperationException(
                    $"Diagnostic baseline already exists: '{fullPath}'. Use --update to replace it explicitly.");
            }
        }

        var temporary = Path.Combine(
            directory,
            $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporary, Serialize(baseline), new UTF8Encoding(false));
            File.Move(temporary, fullPath, overwrite: update);
            return fullPath;
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    internal static string Fingerprint(ValidationIssue issue, string checkedDirectory) =>
        CreateEntry(issue, checkedDirectory).Fingerprint;

    private static DiagnosticBaselineEntry CreateEntry(
        ValidationIssue issue,
        string checkedDirectory)
    {
        var root = Path.GetFullPath(checkedDirectory);
        var fullPath = Path.GetFullPath(issue.FilePath);
        var relative = Path.GetRelativePath(root, fullPath)
            .Replace(Path.DirectorySeparatorChar, '/');
        if (relative == ".."
            || Path.IsPathRooted(relative)
            || relative.StartsWith("../", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Diagnostic path is outside the checked directory.");
        }

        var material = $"{issue.Code}\n{relative}\n{issue.Message.ReplaceLineEndings("\n")}";
        var fingerprint = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(material))).ToLowerInvariant();
        return new DiagnosticBaselineEntry(fingerprint, issue.Code, relative, issue.Message);
    }

    private static void Validate(DiagnosticBaseline baseline)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        if (baseline.SchemaVersion != SchemaVersion)
        {
            throw new InvalidDataException(
                $"Unsupported diagnostic baseline schema version '{baseline.SchemaVersion}'.");
        }

        if (baseline.Diagnostics is null)
        {
            throw new InvalidDataException("Diagnostic baseline must contain a diagnostics array.");
        }

        var fingerprints = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in baseline.Diagnostics)
        {
            if (entry is null
                || entry.Fingerprint.Length != 64
                || entry.Fingerprint.Any(character => !char.IsAsciiHexDigit(character) || char.IsUpper(character))
                || !fingerprints.Add(entry.Fingerprint)
                || entry.Code.Length != 6
                || !entry.Code.StartsWith("ADR", StringComparison.Ordinal)
                || entry.Code[3..].Any(character => !char.IsAsciiDigit(character))
                || string.IsNullOrWhiteSpace(entry.Message)
                || string.IsNullOrWhiteSpace(entry.File)
                || Path.IsPathRooted(entry.File)
                || entry.File == ".."
                || entry.File.StartsWith("../", StringComparison.Ordinal)
                || entry.File.Contains('\\'))
            {
                throw new InvalidDataException("Diagnostic baseline contains an invalid or duplicate entry.");
            }
        }

        if (!baseline.Diagnostics.SequenceEqual(
                baseline.Diagnostics.OrderBy(entry => entry.Fingerprint, StringComparer.Ordinal)))
        {
            throw new InvalidDataException("Diagnostic baseline entries must be ordered by fingerprint.");
        }
    }
}
