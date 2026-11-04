using AdrGuard.Parsing;
using AdrGuard.Validation;

namespace AdrGuard.Cli;

internal sealed record IndexCommandArguments(
    string DirectoryPath,
    string? OutputPath,
    AdrFormat AdrFormat,
    string? LifecycleStatuses,
    bool ConventionalSupersession,
    string? FilenamePolicy,
    string? ValidationProfile,
    bool EnrichedCatalog)
{
    internal static bool TryParse(
        IReadOnlyList<string> args,
        out IndexCommandArguments arguments)
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
        return true;
    }

    private static bool TryApplyArgument(
        IReadOnlyList<string> args,
        ref int index,
        ParseState state) =>
        args[index] switch
        {
            "--catalog" => TryAssignCatalog(args, ref index, state),
            "--conventional-supersession" => TryAssignFlag(ref state.ConventionalSupersession),
            "--output" => TryAssignValue(args, ref index, ref state.OutputPath),
            "--adr-format" => TryAssignFormat(args, ref index, state),
            "--lifecycle-statuses" => TryAssignLifecycleStatuses(args, ref index, state),
            "--filename-policy" => TryAssignFilenamePolicy(args, ref index, state),
            "--validation-profile" => TryAssignValidationProfile(args, ref index, state),
            _ => TryAssignDirectory(args[index], state),
        };

    private static bool TryAssignCatalog(
        IReadOnlyList<string> args,
        ref int index,
        ParseState state)
    {
        if (state.EnrichedCatalog
            || !TryReadValue(args, ref index, out var value)
            || value != "enriched")
        {
            return false;
        }

        state.EnrichedCatalog = true;
        return true;
    }

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

    private static bool TryAssignFormat(
        IReadOnlyList<string> args,
        ref int index,
        ParseState state)
    {
        if (state.AdrFormatAssigned
            || !TryReadValue(args, ref index, out var value))
        {
            return false;
        }

        state.AdrFormat = value switch
        {
            "canonical" => AdrFormat.Canonical,
            "madr-4" => AdrFormat.Madr4,
            _ => (AdrFormat)(-1),
        };
        state.AdrFormatAssigned = Enum.IsDefined(state.AdrFormat);
        return state.AdrFormatAssigned;
    }

    private static bool TryAssignLifecycleStatuses(
        IReadOnlyList<string> args,
        ref int index,
        ParseState state)
    {
        if (state.LifecycleStatuses is not null
            || !TryReadValue(args, ref index, out var value))
        {
            return false;
        }

        try
        {
            AdrLifecyclePolicy.Parse(value);
        }
        catch (ArgumentException)
        {
            return false;
        }

        state.LifecycleStatuses = value;
        return true;
    }

    private static bool TryAssignFilenamePolicy(
        IReadOnlyList<string> args,
        ref int index,
        ParseState state)
    {
        if (state.FilenamePolicy is not null
            || !TryReadValue(args, ref index, out var value))
        {
            return false;
        }

        try
        {
            AdrFilenamePolicy.Parse(value);
        }
        catch (ArgumentException)
        {
            return false;
        }

        state.FilenamePolicy = value;
        return true;
    }

    private static bool TryAssignValidationProfile(
        IReadOnlyList<string> args,
        ref int index,
        ParseState state)
    {
        if (state.ValidationProfile is not null
            || !TryReadValue(args, ref index, out var value))
        {
            return false;
        }

        try
        {
            AdrValidationOptionsFactory.ParseProfile(value);
        }
        catch (ArgumentException)
        {
            return false;
        }

        state.ValidationProfile = value;
        return true;
    }

    private static bool TryAssignDirectory(string value, ParseState state)
    {
        if (value.StartsWith('-') || state.DirectoryAssigned)
        {
            return false;
        }

        state.DirectoryPath = value;
        state.DirectoryAssigned = true;
        return true;
    }

    private static bool TryReadValue(
        IReadOnlyList<string> args,
        ref int index,
        out string value)
    {
        if (index + 1 >= args.Count)
        {
            value = string.Empty;
            return false;
        }

        value = args[++index];
        return !string.IsNullOrWhiteSpace(value)
            && !value.StartsWith('-');
    }

    private static IndexCommandArguments Empty { get; } =
        new(
            ".",
            null,
            AdrFormat.Canonical,
            null,
            false,
            null,
            null,
            false);

    private sealed class ParseState
    {
        internal string DirectoryPath { get; set; } = ".";
        internal string? OutputPath;
        internal AdrFormat AdrFormat { get; set; } = AdrFormat.Canonical;
        internal string? LifecycleStatuses { get; set; }
        internal bool ConventionalSupersession;
        internal string? FilenamePolicy { get; set; }
        internal string? ValidationProfile { get; set; }
        internal bool EnrichedCatalog;
        internal bool DirectoryAssigned { get; set; }
        internal bool AdrFormatAssigned { get; set; }

        internal IndexCommandArguments ToArguments() =>
            new(
                DirectoryPath,
                OutputPath,
                AdrFormat,
                LifecycleStatuses,
                ConventionalSupersession,
                FilenamePolicy,
                ValidationProfile,
                EnrichedCatalog);
    }
}
