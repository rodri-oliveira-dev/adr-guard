namespace AdrGuard.Cli;

internal static class CliCommandArgumentReader
{
    internal static bool TryAssignDirectory(
        string value,
        ref string directoryPath,
        ref bool directoryAssigned)
    {
        if (value.StartsWith('-') || directoryAssigned)
        {
            return false;
        }

        directoryPath = value;
        directoryAssigned = true;
        return true;
    }

    internal static bool TryReadValue(
        IReadOnlyList<string> args,
        ref int index,
        bool rejectWhitespace,
        out string value)
    {
        if (index + 1 >= args.Count)
        {
            value = string.Empty;
            return false;
        }

        value = args[++index];
        return (!rejectWhitespace || !string.IsNullOrWhiteSpace(value))
            && !value.StartsWith('-');
    }
}
