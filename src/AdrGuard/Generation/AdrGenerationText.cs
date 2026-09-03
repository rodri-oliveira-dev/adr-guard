namespace AdrGuard.Generation;

internal static class AdrGenerationText
{
    internal const char NewLine = '\n';

    internal const string DoubleNewLine = "\n\n";

    internal static string NormalizeNewLines(
        string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
    }
}
