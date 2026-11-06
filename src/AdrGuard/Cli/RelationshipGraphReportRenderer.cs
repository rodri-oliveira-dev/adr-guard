using AdrGuard.Model;
using AdrGuard.Validation;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace AdrGuard.Cli;

internal static class RelationshipGraphReportRenderer
{
    internal const int MaximumResponseBytes = 1_048_576;

    internal static void Write(
        AdrRelationshipGraph graph,
        IReadOnlyList<ValidationIssue> diagnostics,
        string repositoryRoot,
        string adrDirectory,
        AdrFormat format,
        TextWriter output)
    {
        var newLine = output.NewLine;
        var maximumJsonBytes = MaximumResponseBytes - Encoding.UTF8.GetByteCount(newLine);
        if (maximumJsonBytes < 0)
        {
            throw ResponseTooLarge();
        }

        using var stream = new BoundedMemoryStream(maximumJsonBytes);
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("schemaVersion", "1.0");
            writer.WriteString("adrDirectory", Relative(repositoryRoot, adrDirectory));
            WriteSummary(writer, graph, diagnostics);
            WriteNodes(writer, graph.Nodes, repositoryRoot, format);
            WriteRelationships(writer, graph.Relationships, repositoryRoot);
            WriteDiagnostics(writer, diagnostics, repositoryRoot);
            writer.WriteEndObject();
        }

        output.Write(Encoding.UTF8.GetString(stream.GetBuffer(), 0, checked((int)stream.Length)));
        output.Write(newLine);
    }

    private static void WriteSummary(
        Utf8JsonWriter writer,
        AdrRelationshipGraph graph,
        IReadOnlyList<ValidationIssue> diagnostics)
    {
        writer.WriteStartObject("summary");
        writer.WriteNumber("nodes", graph.Nodes.Count);
        writer.WriteNumber("relationships", graph.Relationships.Count);
        writer.WriteNumber("resolved", graph.Relationships.Count(edge => edge.Resolution == AdrRelationshipResolution.Resolved));
        writer.WriteNumber("unresolved", graph.Relationships.Count(edge => edge.Resolution == AdrRelationshipResolution.Unresolved));
        writer.WriteNumber("ambiguous", graph.Relationships.Count(edge => edge.Resolution == AdrRelationshipResolution.Ambiguous));
        writer.WriteNumber("diagnostics", diagnostics.Count);
        writer.WriteEndObject();
    }

    private static void WriteNodes(
        Utf8JsonWriter writer,
        IReadOnlyList<AdrDocument> nodes,
        string repositoryRoot,
        AdrFormat format)
    {
        writer.WriteStartArray("nodes");
        foreach (var node in nodes
                     .OrderBy(Identifier, StringComparer.Ordinal)
                     .ThenBy(node => Relative(repositoryRoot, node.FilePath), StringComparer.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("id", Identifier(node));
            WriteOptionalString(writer, "title", node.Title);
            WriteOptionalString(writer, "status", node.Status);
            writer.WriteString("path", Relative(repositoryRoot, node.FilePath));
            writer.WriteString("format", format == AdrFormat.Madr4 ? "madr-4" : "canonical");
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static void WriteRelationships(
        Utf8JsonWriter writer,
        IReadOnlyList<AdrRelationshipEdge> relationships,
        string repositoryRoot)
    {
        writer.WriteStartArray("relationships");
        foreach (var relationship in relationships
                     .OrderBy(edge => Identifier(edge.Source), StringComparer.Ordinal)
                     .ThenBy(edge => Relative(repositoryRoot, edge.Source.FilePath), StringComparer.Ordinal)
                     .ThenBy(edge => edge.Type, StringComparer.Ordinal)
                     .ThenBy(edge => edge.DeclaredTarget, StringComparer.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("sourceId", Identifier(relationship.Source));
            writer.WriteString("sourcePath", Relative(repositoryRoot, relationship.Source.FilePath));
            writer.WriteString("type", relationship.Type);
            writer.WriteString("declaredTarget", relationship.DeclaredTarget);
            writer.WriteString("resolution", Resolution(relationship.Resolution));
            writer.WriteStartArray("targets");
            foreach (var target in relationship.Targets
                         .OrderBy(Identifier, StringComparer.Ordinal)
                         .ThenBy(target => Relative(repositoryRoot, target.FilePath), StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteString("id", Identifier(target));
                writer.WriteString("path", Relative(repositoryRoot, target.FilePath));
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static void WriteDiagnostics(
        Utf8JsonWriter writer,
        IReadOnlyList<ValidationIssue> diagnostics,
        string repositoryRoot)
    {
        writer.WriteStartArray("diagnostics");
        foreach (var diagnostic in diagnostics)
        {
            writer.WriteStartObject();
            writer.WriteString("code", diagnostic.Code);
            writer.WriteString("message", diagnostic.Message);
            writer.WriteString("path", Relative(repositoryRoot, diagnostic.FilePath));
            writer.WriteString("severity", diagnostic.Severity == ValidationSeverity.Warning ? "warning" : "error");
            if (diagnostic.Line.HasValue) writer.WriteNumber("line", diagnostic.Line.Value);
            if (diagnostic.Column.HasValue) writer.WriteNumber("column", diagnostic.Column.Value);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static string Identifier(AdrDocument document) =>
        document.StableId
        ?? (document.Id is null
            ? $"path:{document.FileName}"
            : $"ADR-{document.Id.Value.ToString(CultureInfo.InvariantCulture)}");

    private static string Relative(string repositoryRoot, string path) =>
        Path.GetRelativePath(repositoryRoot, Path.GetFullPath(path))
            .Replace(Path.DirectorySeparatorChar, '/');

    private static string Resolution(AdrRelationshipResolution resolution) => resolution switch
    {
        AdrRelationshipResolution.Resolved => "resolved",
        AdrRelationshipResolution.Ambiguous => "ambiguous",
        _ => "unresolved",
    };

    private static void WriteOptionalString(Utf8JsonWriter writer, string propertyName, string? value)
    {
        if (value is null) writer.WriteNull(propertyName);
        else writer.WriteString(propertyName, value);
    }

    private static InvalidDataException ResponseTooLarge() => new(
        $"Relationship graph response exceeds the {MaximumResponseBytes}-byte limit.");

    private sealed class BoundedMemoryStream(int maximumBytes) : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count)
        {
            EnsureCapacityFor(count);
            base.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            EnsureCapacityFor(buffer.Length);
            base.Write(buffer);
        }

        public override void WriteByte(byte value)
        {
            EnsureCapacityFor(1);
            base.WriteByte(value);
        }

        private void EnsureCapacityFor(int count)
        {
            if (Position > maximumBytes - count)
            {
                throw ResponseTooLarge();
            }
        }
    }
}
