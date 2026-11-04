using AdrGuard.Review.Policy;
using AdrGuard.Review.Reporting;

namespace AdrGuard.Cli;

internal sealed record ReviewCommandArguments(
    string TargetPath,
    string? ProviderName,
    string? Model,
    string? Endpoint,
    IReadOnlyList<string> ContextFilePaths,
    bool IncludeExistingAdrs,
    AdrReviewOutputFormat Format,
    string? OutputPath,
    bool OverwriteOutput,
    AdrReviewPolicyMode PolicyMode,
    string? PolicyFilePath,
    string? CompareReference)
{
    internal static bool TryParse(
        IReadOnlyList<string> args,
        out ReviewCommandArguments arguments)
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
        return !string.IsNullOrWhiteSpace(state.TargetPath)
            && (!state.OverwriteOutput || state.OutputPath is not null);
    }

    private static bool TryApplyArgument(
        IReadOnlyList<string> args,
        ref int index,
        ParseState state) =>
        args[index] switch
        {
            "--include-existing-adrs" => TryAssignFlag(ref state.IncludeExistingAdrs),
            "--overwrite" => TryAssignFlag(ref state.OverwriteOutput),
            "--provider" => TryAssignValue(args, ref index, ref state.ProviderName),
            "--model" => TryAssignValue(args, ref index, ref state.Model),
            "--compare-ref" => TryAssignValue(args, ref index, ref state.CompareReference),
            "--endpoint" => TryAssignValue(args, ref index, ref state.Endpoint),
            "--context-file" => TryAddValue(args, ref index, state.ContextFilePaths),
            "--policy" => TryAssignPolicy(args, ref index, state),
            "--policy-file" => TryAssignValue(args, ref index, ref state.PolicyFilePath),
            "--format" => TryAssignFormat(args, ref index, state),
            "--output" => TryAssignValue(args, ref index, ref state.OutputPath),
            _ => TryAssignTarget(args[index], state),
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

    private static bool TryAssignPolicy(
        IReadOnlyList<string> args,
        ref int index,
        ParseState state)
    {
        if (state.PolicyAssigned
            || !TryReadValue(args, ref index, out var value))
        {
            return false;
        }

        state.PolicyMode = value switch
        {
            "advisory" => AdrReviewPolicyMode.Advisory,
            "enforce" => AdrReviewPolicyMode.Enforce,
            _ => (AdrReviewPolicyMode)(-1),
        };
        state.PolicyAssigned = Enum.IsDefined(state.PolicyMode);
        return state.PolicyAssigned;
    }

    private static bool TryAssignFormat(
        IReadOnlyList<string> args,
        ref int index,
        ParseState state)
    {
        if (state.FormatAssigned
            || !TryReadValue(args, ref index, out var value))
        {
            return false;
        }

        state.Format = value switch
        {
            "text" => AdrReviewOutputFormat.Text,
            "json" => AdrReviewOutputFormat.Json,
            _ => (AdrReviewOutputFormat)(-1),
        };
        state.FormatAssigned = Enum.IsDefined(state.Format);
        return state.FormatAssigned;
    }

    private static bool TryAssignTarget(string value, ParseState state)
    {
        if (value.StartsWith('-') || state.TargetPath is not null)
        {
            return false;
        }

        state.TargetPath = value;
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

    internal static ReviewCommandArguments Empty { get; } =
        new(
            string.Empty,
            null,
            null,
            null,
            [],
            false,
            AdrReviewOutputFormat.Text,
            null,
            false,
            AdrReviewPolicyMode.Advisory,
            null,
            null);

    private sealed class ParseState
    {
        internal string? TargetPath;
        internal string? ProviderName;
        internal string? Model;
        internal string? Endpoint;
        internal string? CompareReference;
        internal string? OutputPath;
        internal string? PolicyFilePath;
        internal List<string> ContextFilePaths { get; } = [];
        internal bool IncludeExistingAdrs;
        internal AdrReviewOutputFormat Format { get; set; } = AdrReviewOutputFormat.Text;
        internal bool FormatAssigned { get; set; }
        internal bool OverwriteOutput;
        internal AdrReviewPolicyMode PolicyMode { get; set; } = AdrReviewPolicyMode.Advisory;
        internal bool PolicyAssigned { get; set; }

        internal ReviewCommandArguments ToArguments() =>
            new(
                TargetPath ?? string.Empty,
                ProviderName,
                Model,
                Endpoint,
                ContextFilePaths,
                IncludeExistingAdrs,
                Format,
                OutputPath,
                OverwriteOutput,
                PolicyMode,
                PolicyFilePath,
                CompareReference);
    }
}
