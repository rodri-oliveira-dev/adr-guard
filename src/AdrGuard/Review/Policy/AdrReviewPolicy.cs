using AdrGuard.Model;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AdrGuard.Review.Policy;

internal enum AdrReviewPolicyMode
{
    Advisory,
    Enforce,
}

internal sealed record AdrReviewPolicyDefinition(
    string SchemaVersion,
    AdrReviewPolicyRule[] Rules)
{
    internal const string CurrentSchemaVersion = "1.0";

    internal static AdrReviewPolicyDefinition Empty { get; } =
        new(CurrentSchemaVersion, []);
}

internal sealed record AdrReviewPolicyRule(
    string Name,
    string Type,
    string? Section,
    string? Path);

internal sealed record AdrReviewPolicyViolation(
    string RuleName,
    string RuleType,
    string Evidence);

internal sealed record AdrReviewPolicyEvaluation(
    AdrReviewPolicyMode Mode,
    AdrReviewPolicyViolation[] Violations)
{
    internal bool HasViolations => Violations.Length > 0;
}

internal static class AdrReviewPolicyLoader
{
    private const int MaximumPolicyCharacters = 100000;
    private const int MaximumRules = 100;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };

    internal static AdrReviewPolicyDefinition Load(
        string? policyFilePath)
    {
        if (policyFilePath is null)
        {
            return AdrReviewPolicyDefinition.Empty;
        }

        if (!string.Equals(
                Path.GetExtension(policyFilePath),
                ".json",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Review policy file must use the .json extension.");
        }

        var fullPath = Path.GetFullPath(policyFilePath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                "Review policy file does not exist.",
                fullPath);
        }

        var json = File.ReadAllText(fullPath);

        if (json.Length > MaximumPolicyCharacters)
        {
            throw new InvalidDataException(
                $"Review policy exceeds the {MaximumPolicyCharacters}-character limit.");
        }

        AdrReviewPolicyDefinition? definition;

        try
        {
            definition = JsonSerializer.Deserialize<AdrReviewPolicyDefinition>(
                json,
                JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "Review policy JSON is malformed or contains unsupported properties.",
                exception);
        }

        if (definition is null)
        {
            throw new InvalidDataException(
                "Review policy JSON is empty.");
        }

        Validate(definition);
        return definition;
    }

    private static void Validate(
        AdrReviewPolicyDefinition definition)
    {
        if (!string.Equals(
                definition.SchemaVersion,
                AdrReviewPolicyDefinition.CurrentSchemaVersion,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Unsupported review policy schema version '{definition.SchemaVersion}'.");
        }

        if (definition.Rules is null
            || definition.Rules.Length == 0)
        {
            throw new InvalidDataException(
                "Review policy must define at least one deterministic rule.");
        }

        if (definition.Rules.Length > MaximumRules)
        {
            throw new InvalidDataException(
                $"Review policy cannot define more than {MaximumRules} rules.");
        }

        var names = new HashSet<string>(
            StringComparer.Ordinal);

        foreach (var rule in definition.Rules)
        {
            if (rule is null)
            {
                throw new InvalidDataException(
                    "Review policy contains a null rule.");
            }

            if (!IsValidRuleName(rule.Name)
                || !names.Add(rule.Name))
            {
                throw new InvalidDataException(
                    $"Review policy rule name '{rule.Name}' is invalid or duplicated.");
            }

            switch (rule.Type)
            {
                case "required-section-content":
                    if (string.IsNullOrWhiteSpace(rule.Section)
                        || !string.IsNullOrWhiteSpace(rule.Path))
                    {
                        throw new InvalidDataException(
                            $"Rule '{rule.Name}' requires 'section' and must not define 'path'.");
                    }

                    break;

                case "required-context-file":
                    ValidateContextRule(rule);
                    break;

                default:
                    throw new InvalidDataException(
                        $"Rule '{rule.Name}' uses unsupported deterministic type '{rule.Type}'.");
            }
        }
    }

    private static void ValidateContextRule(
        AdrReviewPolicyRule rule)
    {
        if (string.IsNullOrWhiteSpace(rule.Path)
            || !string.IsNullOrWhiteSpace(rule.Section)
            || Path.IsPathRooted(rule.Path))
        {
            throw new InvalidDataException(
                $"Rule '{rule.Name}' requires a relative 'path' and must not define 'section'.");
        }

        var extension = Path.GetExtension(rule.Path);

        if (!string.Equals(
                extension,
                ".md",
                StringComparison.OrdinalIgnoreCase)
            && !string.Equals(
                extension,
                ".txt",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Rule '{rule.Name}' context path must use .md or .txt.");
        }
    }

    private static bool IsValidRuleName(
        string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 64
        && value.All(character =>
            char.IsLetterOrDigit(character)
            || character is '-' or '_' or '.');
}

internal static class AdrReviewPolicyEvaluator
{
    internal static AdrReviewPolicyEvaluation Evaluate(
        AdrDocument document,
        IReadOnlyList<string> contextFilePaths,
        AdrReviewPolicyDefinition definition,
        AdrReviewPolicyMode mode,
        string? policyFilePath)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(contextFilePaths);
        ArgumentNullException.ThrowIfNull(definition);

        var policyDirectory =
            policyFilePath is null
                ? Directory.GetCurrentDirectory()
                : Path.GetDirectoryName(
                    Path.GetFullPath(policyFilePath))
                    ?? Directory.GetCurrentDirectory();

        var selectedContextFiles = contextFilePaths
            .Select(Path.GetFullPath)
            .ToHashSet(GetPathComparer());

        var violations = new List<AdrReviewPolicyViolation>();

        foreach (var rule in definition.Rules)
        {
            switch (rule.Type)
            {
                case "required-section-content":
                    EvaluateRequiredSection(
                        document,
                        rule,
                        violations);
                    break;

                case "required-context-file":
                    EvaluateRequiredContextFile(
                        policyDirectory,
                        selectedContextFiles,
                        rule,
                        violations);
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Unsupported validated policy rule type '{rule.Type}'.");
            }
        }

        return new AdrReviewPolicyEvaluation(
            mode,
            violations
                .OrderBy(
                    violation => violation.RuleName,
                    StringComparer.Ordinal)
                .ToArray());
    }

    private static void EvaluateRequiredSection(
        AdrDocument document,
        AdrReviewPolicyRule rule,
        List<AdrReviewPolicyViolation> violations)
    {
        var hasNonEmptySection = document.Sections
            .Any(candidate =>
                candidate.Level == 2
                && string.Equals(
                    candidate.Heading,
                    rule.Section,
                    StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(
                    candidate.Content));

        if (hasNonEmptySection)
        {
            return;
        }

        violations.Add(
            new AdrReviewPolicyViolation(
                rule.Name,
                rule.Type,
                $"section '{rule.Section}' is missing or empty."));
    }

    private static void EvaluateRequiredContextFile(
        string policyDirectory,
        HashSet<string> selectedContextFiles,
        AdrReviewPolicyRule rule,
        List<AdrReviewPolicyViolation> violations)
    {
        var requiredPath = Path.GetFullPath(
            Path.Combine(
                policyDirectory,
                rule.Path!));

        if (File.Exists(requiredPath)
            && selectedContextFiles.Contains(requiredPath))
        {
            return;
        }

        violations.Add(
            new AdrReviewPolicyViolation(
                rule.Name,
                rule.Type,
                $"context file '{rule.Path}' does not exist or was not explicitly selected with --context-file."));
    }

    private static StringComparer GetPathComparer() =>
        OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
}
