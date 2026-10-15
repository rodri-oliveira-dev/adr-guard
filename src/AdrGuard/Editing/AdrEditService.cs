using System.Security.Cryptography;
using System.Text;
using AdrGuard.Model;
using AdrGuard.Parsing;
using AdrGuard.Validation;

namespace AdrGuard.Editing;

internal sealed record AdrEditField(
    string Id,
    string Label,
    string Kind,
    bool Required,
    string Value,
    IReadOnlyList<string>? Options = null);

internal sealed record AdrEditDocument(
    string FilePath,
    string Fingerprint,
    string Title,
    IReadOnlyList<AdrEditField> Fields,
    bool Written = false);

internal sealed class AdrEditConflictException : IOException
{
    internal AdrEditConflictException()
        : base("The ADR changed after it was loaded. Reload it before saving.")
    {
    }
}

internal static class AdrEditService
{
    internal const int MaximumFileBytes = 256 * 1024;
    internal const int MaximumFieldCharacters = 32 * 1024;

    internal static AdrEditDocument Load(
        string filePath,
        string? adrDirectory,
        AdrValidationOptions options,
        CancellationToken cancellationToken)
    {
        var source = ReadSource(filePath, cancellationToken);
        filePath = source.FilePath;
        var layout = MarkdownLayout.Parse(source.Text, options);
        Validate(filePath, adrDirectory, source.Text, options, cancellationToken);
        return layout.ToDocument(filePath, source.Fingerprint);
    }

    internal static AdrEditDocument Save(
        string filePath,
        string expectedFingerprint,
        string title,
        IReadOnlyDictionary<string, string> values,
        string? adrDirectory,
        AdrValidationOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedFingerprint);
        ArgumentNullException.ThrowIfNull(values);
        ValidateTitle(title);

        var source = ReadSource(filePath, cancellationToken);
        filePath = source.FilePath;
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(source.Fingerprint),
                Encoding.ASCII.GetBytes(expectedFingerprint)))
        {
            throw new AdrEditConflictException();
        }

        var layout = MarkdownLayout.Parse(source.Text, options);
        var expectedIds = layout.Fields.Select(field => field.Id).ToHashSet(StringComparer.Ordinal);
        if (values.Count != expectedIds.Count || values.Keys.Any(key => !expectedIds.Contains(key)))
        {
            throw new ArgumentException("ADR edit fields do not match the loaded document.", nameof(values));
        }

        var replacements = new List<TextReplacement>();
        if (!string.Equals(title, layout.Title, StringComparison.Ordinal))
            replacements.Add(new TextReplacement(layout.TitleStart, layout.TitleLength, title));
        foreach (var field in layout.Fields)
        {
            var value = values[field.Id];
            if (string.Equals(value, field.Value, StringComparison.Ordinal)) continue;
            ValidateField(field, value);
            var replacement = field.Kind == "section"
                ? NormalizeSectionValue(value, source.Text, field.Length)
                : value;
            replacements.Add(new TextReplacement(field.Start, field.Length, replacement));
        }

        if (replacements.Count == 0)
        {
            ResolveAdrDirectory(filePath, adrDirectory);
            return layout.ToDocument(filePath, source.Fingerprint);
        }
        var updated = Apply(source.Text, replacements);
        ValidateUpdate(filePath, adrDirectory, source.Text, updated, options, cancellationToken);
        Persist(filePath, source, updated, cancellationToken);
        var saved = ReadSource(filePath, cancellationToken);
        return MarkdownLayout.Parse(saved.Text, options).ToDocument(filePath, saved.Fingerprint, written: true);
    }

    private static SourceDocument ReadSource(string filePath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = Path.GetFullPath(filePath);
        var info = new FileInfo(fullPath);
        if (!info.Exists || (info.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("The ADR must be an existing regular, non-symbolic-link file.");
        fullPath = Directory.EnumerateFiles(info.DirectoryName!, info.Name, SearchOption.TopDirectoryOnly)
            .SingleOrDefault(candidate => PathsEqual(candidate, fullPath))
            ?? fullPath;
        if (!string.Equals(info.Extension, ".md", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The ADR edit target must be a Markdown file.");
        if (info.Length > MaximumFileBytes)
            throw new InvalidDataException($"The ADR exceeds the {MaximumFileBytes}-byte edit limit.");

        var bytes = File.ReadAllBytes(fullPath);
        cancellationToken.ThrowIfCancellationRequested();
        var hasBom = bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble);
        var content = hasBom ? bytes.AsSpan(Encoding.UTF8.Preamble.Length) : bytes.AsSpan();
        string text;
        try
        {
            text = new UTF8Encoding(false, true).GetString(content);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException("The ADR must contain valid UTF-8.", exception);
        }
        return new SourceDocument(fullPath, bytes, text, hasBom, Fingerprint(bytes));
    }

    private static void Validate(
        string filePath,
        string? adrDirectory,
        string markdown,
        AdrValidationOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        filePath = Path.GetFullPath(filePath);
        var directory = ResolveAdrDirectory(filePath, adrDirectory);
        var known = AdrFileDiscovery.FindMarkdownFiles(directory);
        var document = AdrMarkdownParser.Parse(filePath, markdown);
        var result = AdrValidator.Validate([document], known, options);
        if (!result.IsValid)
        {
            throw new AdrEditValidationException(result);
        }
    }

    private static void ValidateUpdate(
        string filePath,
        string? adrDirectory,
        string originalMarkdown,
        string updatedMarkdown,
        AdrValidationOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = Path.GetFullPath(filePath);
        var directory = ResolveAdrDirectory(fullPath, adrDirectory);
        var neighbors = AdrDocumentLoader.LoadDirectory(directory, cancellationToken)
            .Where(document => !PathsEqual(document.FilePath, fullPath))
            .ToArray();
        var original = AdrMarkdownParser.Parse(fullPath, originalMarkdown);
        var updated = AdrMarkdownParser.Parse(fullPath, updatedMarkdown);
        var before = AdrValidator.Validate([.. neighbors, original], additionalKnownPaths: null, options);
        var after = AdrValidator.Validate([.. neighbors, updated], additionalKnownPaths: null, options);
        var existing = before.Issues.ToHashSet();
        var blocking = after.Issues
            .Where(issue => PathsEqual(issue.FilePath, fullPath)
                || !existing.Contains(issue))
            .ToArray();
        var result = new ValidationResult(blocking);
        if (!result.IsValid)
        {
            throw new AdrEditValidationException(result);
        }
    }

    private static string ResolveAdrDirectory(string filePath, string? adrDirectory)
    {
        var directory = Path.GetFullPath(adrDirectory ?? Path.GetDirectoryName(filePath)!);
        var info = new DirectoryInfo(directory);
        if (!info.Exists || (info.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("The ADR directory must be an existing regular directory.");

        var relative = Path.GetRelativePath(directory, filePath);
        if (relative == ".."
            || Path.IsPathRooted(relative)
            || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new InvalidDataException("The ADR edit target must stay inside the ADR directory.");
        }

        for (var current = new DirectoryInfo(Path.GetDirectoryName(filePath)!);
             !PathsEqual(current.FullName, directory);
             current = current.Parent
                 ?? throw new InvalidDataException("The ADR edit target must stay inside the ADR directory."))
        {
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("The ADR edit target must not traverse a symbolic link.");
        }

        return directory;
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static void Persist(
        string filePath,
        SourceDocument source,
        string text,
        CancellationToken cancellationToken)
    {
        filePath = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(filePath)!;
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            var content = Encoding.UTF8.GetBytes(text);
            var bytes = source.HasBom ? [.. Encoding.UTF8.Preamble, .. content] : content;
            File.WriteAllBytes(temporaryPath, bytes);
            cancellationToken.ThrowIfCancellationRequested();
            var current = File.ReadAllBytes(filePath);
            if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(current), SHA256.HashData(source.Bytes)))
                throw new AdrEditConflictException();
            if ((File.GetAttributes(filePath) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("The ADR edit target became a symbolic link.");
            File.Move(temporaryPath, filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private static string Apply(string source, IEnumerable<TextReplacement> replacements)
    {
        var builder = new StringBuilder(source);
        foreach (var replacement in replacements.OrderByDescending(item => item.Start))
        {
            builder.Remove(replacement.Start, replacement.Length);
            builder.Insert(replacement.Start, replacement.Value);
        }
        return builder.ToString();
    }

    private static string NormalizeSectionValue(string value, string source, int originalLength)
    {
        var newline = source.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var normalized = value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Replace("\n", newline, StringComparison.Ordinal);
        return originalLength == 0 && normalized.Length > 0
            ? normalized + newline + newline
            : normalized;
    }

    private static void ValidateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length > 512
            || title.IndexOfAny(['\r', '\n', '\0']) >= 0)
            throw new ArgumentException("ADR title must be a non-empty single line with at most 512 characters.", nameof(title));
    }

    private static void ValidateField(LayoutField field, string value)
    {
        if (value is null || value.Length > MaximumFieldCharacters || value.Contains('\0'))
            throw new ArgumentException($"ADR edit field '{field.Label}' is invalid or exceeds the size limit.");
        if (field.Kind != "section" && value.IndexOfAny(['\r', '\n']) >= 0)
            throw new ArgumentException($"ADR edit field '{field.Label}' must be a single line.");
        if (field.QuoteDelimiter is not null && value.Contains(field.QuoteDelimiter.Value))
            throw new ArgumentException($"ADR edit field '{field.Label}' cannot contain its YAML quote delimiter.");
        if (field.Required && string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"ADR edit field '{field.Label}' is required.");
        if (field.Options is not null && !field.Options.Contains(value, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException($"ADR status '{value}' is not allowed by the configured lifecycle policy.");
    }

    private static string Fingerprint(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private sealed record SourceDocument(string FilePath, byte[] Bytes, string Text, bool HasBom, string Fingerprint);
    private sealed record TextReplacement(int Start, int Length, string Value);

    private sealed class MarkdownLayout
    {
        private MarkdownLayout(int titleStart, int titleLength, string title, IReadOnlyList<LayoutField> fields)
        {
            TitleStart = titleStart;
            TitleLength = titleLength;
            Title = title;
            Fields = fields;
        }

        internal int TitleStart { get; }
        internal int TitleLength { get; }
        internal string Title { get; }
        internal IReadOnlyList<LayoutField> Fields { get; }

        internal AdrEditDocument ToDocument(string filePath, string fingerprint, bool written = false) => new(
            filePath,
            fingerprint,
            Title,
            Fields.Select(field => new AdrEditField(
                field.Id, field.Label, field.Kind, field.Required, field.Value, field.Options)).ToArray(),
            written);

        internal static MarkdownLayout Parse(string markdown, AdrValidationOptions options)
        {
            var lines = Lines(markdown);
            var title = FindTitle(lines) ?? throw new InvalidDataException("The ADR does not contain an editable level-one title.");
            var fields = new List<LayoutField>();
            AddMetadataFields(lines, fields, options);
            AddSectionFields(markdown, lines, fields, options);
            if (fields.Count > 64) throw new InvalidDataException("The ADR contains more than 64 editable fields.");
            return new MarkdownLayout(title.ValueStart, title.ValueLength, title.Value, fields);
        }

        private static void AddMetadataFields(
            IReadOnlyList<LineInfo> lines,
            List<LayoutField> fields,
            AdrValidationOptions options)
        {
            if (lines.Count == 0 || lines[0].Text.Trim() != "---") return;
            for (var index = 1; index < lines.Count; index++)
            {
                var line = lines[index];
                if (line.Text.Trim() == "---") break;
                if (line.Text.TrimStart().StartsWith('#')) continue;
                var separator = line.Text.IndexOf(':');
                if (separator <= 0) continue;
                var key = line.Text[..separator].Trim();
                var raw = line.Text[(separator + 1)..];
                var leading = raw.Length - raw.TrimStart().Length;
                var trimmed = raw.Trim();
                var quoted = trimmed.Length >= 2 && ((trimmed[0] == '"' && trimmed[^1] == '"') || (trimmed[0] == '\'' && trimmed[^1] == '\''));
                var quoteDelimiter = quoted ? trimmed[0] : (char?)null;
                var value = quoted ? trimmed[1..^1] : trimmed;
                var valueOffset = separator + 1 + leading + (quoted ? 1 : 0);
                var kind = key.Equals("status", StringComparison.OrdinalIgnoreCase)
                    && options.Format == AdrFormat.Madr4 ? "status" : "metadata";
                IReadOnlyList<string>? choices = null;
                fields.Add(new LayoutField($"metadata-{fields.Count}", key, kind, Required: false,
                    line.Start + valueOffset, value.Length, value, choices, quoteDelimiter));
            }
        }

        private static void AddSectionFields(
            string markdown,
            IReadOnlyList<LineInfo> lines,
            List<LayoutField> fields,
            AdrValidationOptions options)
        {
            var headings = FindHeadings(lines).Where(heading => heading.Level <= 2).ToArray();
            for (var index = 0; index < headings.Length; index++)
            {
                var heading = headings[index];
                if (heading.Level != 2) continue;
                var bodyStart = heading.End;
                var bodyEnd = index + 1 < headings.Length ? headings[index + 1].Start : markdown.Length;
                var start = bodyStart;
                while (start < bodyEnd && char.IsWhiteSpace(markdown[start])) start++;
                var end = bodyEnd;
                while (end > start && char.IsWhiteSpace(markdown[end - 1])) end--;
                var value = markdown[start..end];
                var isStatus = heading.Value.Equals("Status", StringComparison.OrdinalIgnoreCase);
                var required = options.Format == AdrFormat.Canonical
                    ? isStatus || heading.Value is "Context" or "Decision" or "Consequences"
                    : heading.Value is "Context and Problem Statement" or "Considered Options" or "Decision Outcome";
                var choices = isStatus && options.Format == AdrFormat.Canonical
                    ? options.EffectiveLifecycle.StatusNames.ToArray()
                    : null;
                fields.Add(new LayoutField($"section-{fields.Count}", heading.Value,
                    isStatus ? "status" : "section", required, start, end - start, value, choices, QuoteDelimiter: null));
            }
        }

        private static HeadingInfo? FindTitle(IReadOnlyList<LineInfo> lines) =>
            FindHeadings(lines).FirstOrDefault(heading => heading.Level == 1);

        private static IEnumerable<HeadingInfo> FindHeadings(IReadOnlyList<LineInfo> lines)
        {
            char? fenceMarker = null;
            var fenceLength = 0;
            var inFrontMatter = lines.Count > 0 && lines[0].Text.Trim() == "---";
            for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
            {
                var line = lines[lineIndex];
                if (inFrontMatter)
                {
                    if (lineIndex > 0 && line.Text.Trim() == "---") inFrontMatter = false;
                    continue;
                }
                if (TryGetFence(line.Text, out var marker, out var markerLength))
                {
                    if (fenceMarker is null)
                    {
                        fenceMarker = marker;
                        fenceLength = markerLength;
                        continue;
                    }
                    if (marker == fenceMarker
                        && markerLength >= fenceLength
                        && IsClosingFence(line.Text, markerLength))
                    {
                        fenceMarker = null;
                        fenceLength = 0;
                        continue;
                    }
                }
                if (fenceMarker is not null) continue;
                var indentation = 0;
                while (indentation < line.Text.Length && line.Text[indentation] == ' ') indentation++;
                if (indentation >= 4) continue;
                var trimmed = line.Text.TrimStart();
                var hashes = 0;
                while (hashes < trimmed.Length && trimmed[hashes] == '#') hashes++;
                if (hashes is < 1 or > 6 || hashes >= trimmed.Length || !char.IsWhiteSpace(trimmed[hashes])) continue;
                var contentStartInTrimmed = hashes + 1;
                while (contentStartInTrimmed < trimmed.Length && char.IsWhiteSpace(trimmed[contentStartInTrimmed])) contentStartInTrimmed++;
                var value = trimmed[contentStartInTrimmed..].TrimEnd();
                var closing = value.Length;
                while (closing > 0 && value[closing - 1] == '#') closing--;
                if (closing < value.Length && closing > 0 && char.IsWhiteSpace(value[closing - 1])) value = value[..closing].TrimEnd();
                if (value.Length == 0) continue;
                var valueStart = line.Start + indentation + contentStartInTrimmed;
                yield return new HeadingInfo(hashes, line.Start, line.End, valueStart, value.Length, value);
            }
        }

        private static bool TryGetFence(string line, out char marker, out int markerLength)
        {
            marker = default;
            markerLength = 0;
            var indentation = 0;
            while (indentation < line.Length && line[indentation] == ' ') indentation++;
            if (indentation >= 4) return false;
            var trimmed = line.AsSpan(indentation);
            if (trimmed.Length < 3 || trimmed[0] is not ('`' or '~')) return false;
            marker = trimmed[0];
            while (markerLength < trimmed.Length && trimmed[markerLength] == marker) markerLength++;
            return markerLength >= 3;
        }

        private static bool IsClosingFence(string line, int markerLength) =>
            line.TrimStart()[markerLength..].All(char.IsWhiteSpace);

        private static List<LineInfo> Lines(string markdown)
        {
            var result = new List<LineInfo>();
            var start = 0;
            while (start < markdown.Length)
            {
                var newline = markdown.IndexOf('\n', start);
                var end = newline < 0 ? markdown.Length : newline + 1;
                var textEnd = newline < 0 ? end : newline > start && markdown[newline - 1] == '\r' ? newline - 1 : newline;
                result.Add(new LineInfo(start, end, markdown[start..textEnd]));
                start = end;
            }
            if (markdown.Length == 0) result.Add(new LineInfo(0, 0, string.Empty));
            return result;
        }
    }

    private sealed record LineInfo(int Start, int End, string Text);
    private sealed record HeadingInfo(int Level, int Start, int End, int ValueStart, int ValueLength, string Value);
    private sealed record LayoutField(string Id, string Label, string Kind, bool Required, int Start, int Length, string Value, IReadOnlyList<string>? Options, char? QuoteDelimiter);
}

internal sealed class AdrEditValidationException : Exception
{
    internal AdrEditValidationException(ValidationResult result)
        : base(string.Join(Environment.NewLine, result.Issues.Select(issue => $"{issue.Code}: {issue.Message}"))) =>
        Result = result;

    internal ValidationResult Result { get; }
}
