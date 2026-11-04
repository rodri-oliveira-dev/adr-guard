namespace AdrGuard.Cli;

internal sealed record DraftCommandArguments(
    string DirectoryPath,
    string Title,
    string Context,
    string CultureName,
    string? ProviderName,
    string? Model,
    string? Endpoint,
    IReadOnlyList<string> ContextFilePaths,
    bool IncludeExistingAdrs,
    bool DryRun,
    string? TemplateName,
    string? TemplateFilePath)
{
    private const string DefaultCultureName = "en-US";

    internal static bool TryParse(
        IReadOnlyList<string> args,
        out DraftCommandArguments arguments)
    {
        var state = new ParseState();

        for (var index = 1; index < args.Count; index++)
        {
            if (!TryApplyArgument(args, ref index, state))
            {
                arguments = Empty;
                return false;
            }
        }

        arguments = state.ToArguments();
        return state.Title is not null && state.Context is not null;
    }

    private static bool TryApplyArgument(
        IReadOnlyList<string> args,
        ref int index,
        ParseState state) =>
        args[index] switch
        {
            "--include-existing-adrs" => TryAssignFlag(ref state.IncludeExistingAdrs),
            "--dry-run" or "--preview" => TryAssignFlag(ref state.DryRun),
            "--title" => TryAssignValue(args, ref index, ref state.Title),
            "--context" => TryAssignValue(args, ref index, ref state.Context),
            "--culture" => TryAssignCulture(args, ref index, state),
            "--provider" => TryAssignValue(args, ref index, ref state.ProviderName),
            "--model" => TryAssignValue(args, ref index, ref state.Model),
            "--endpoint" => TryAssignValue(args, ref index, ref state.Endpoint),
            "--context-file" => TryAddValue(args, ref index, state.ContextFilePaths),
            "--template" => TryAssignValue(args, ref index, ref state.TemplateName),
            "--template-file" => TryAssignValue(args, ref index, ref state.TemplateFilePath),
            _ => CliCommandArgumentReader.TryAssignDirectory(
                args[index],
                ref state.DirectoryPath,
                ref state.DirectoryAssigned),
        };

    private static bool TryAssignFlag(ref bool target)
    {
        if (target)
        {
            return false;
        }

        target = true;
        return true;
    }

    private static bool TryAssignValue(
        IReadOnlyList<string> args,
        ref int index,
        ref string? target)
    {
        if (target is not null
            || !TryReadValue(args, ref index, out var value))
        {
            return false;
        }

        target = value;
        return true;
    }

    private static bool TryAssignCulture(
        IReadOnlyList<string> args,
        ref int index,
        ParseState state)
    {
        if (state.CultureAssigned
            || !TryReadValue(args, ref index, out var value))
        {
            return false;
        }

        state.CultureName = value;
        state.CultureAssigned = true;
        return true;
    }

    private static bool TryAddValue(
        IReadOnlyList<string> args,
        ref int index,
        List<string> target)
    {
        if (!TryReadValue(args, ref index, out var value))
        {
            return false;
        }

        target.Add(value);
        return true;
    }

    private static bool TryReadValue(
        IReadOnlyList<string> args,
        ref int index,
        out string value) =>
        CliCommandArgumentReader.TryReadValue(
            args,
            ref index,
            rejectWhitespace: true,
            out value);

    internal static DraftCommandArguments Empty { get; } =
        new(
            ".",
            string.Empty,
            string.Empty,
            DefaultCultureName,
            null,
            null,
            null,
            [],
            false,
            false,
            null,
            null);

    private sealed class ParseState
    {
        internal string DirectoryPath = ".";
        internal string? Title;
        internal string? Context;
        internal string? CultureName = DefaultCultureName;
        internal bool CultureAssigned { get; set; }
        internal string? ProviderName;
        internal string? Model;
        internal string? Endpoint;
        internal List<string> ContextFilePaths { get; } = [];
        internal bool IncludeExistingAdrs;
        internal bool DryRun;
        internal string? TemplateName;
        internal string? TemplateFilePath;
        internal bool DirectoryAssigned;

        internal DraftCommandArguments ToArguments() =>
            new(
                DirectoryPath,
                Title ?? string.Empty,
                Context ?? string.Empty,
                CultureName ?? DefaultCultureName,
                ProviderName,
                Model,
                Endpoint,
                ContextFilePaths,
                IncludeExistingAdrs,
                DryRun,
                TemplateName,
                TemplateFilePath);
    }
}
