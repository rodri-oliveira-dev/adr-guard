using System.Text.Json;
using AdrGuard.Exporting;

namespace AdrGuard.Cli;

internal static class AdrRelationshipsExportSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        NewLine = "\n",
    };

    internal static void Write(AdrRelationshipsExport export, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(export);
        ArgumentNullException.ThrowIfNull(output);

        output.Write(JsonSerializer.Serialize(export, JsonOptions));
        output.Write('\n');
    }
}
