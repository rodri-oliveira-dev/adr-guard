using AdrGuard.Git;

namespace AdrGuard.Impact;

internal static class ImpactCorrelationService
{
    internal const int MaximumMatchEvaluations = 1_000_000;
    internal const int MaximumEvidenceEntries = 5_000;
    internal const int MaximumEvidencePerDecision = 500;

    internal static ImpactAnalysisResult Analyze(
        ImpactMappingManifest manifest,
        GitChangeInventory inventory)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(inventory);

        if (!PathsEqual(manifest.RepositoryRoot, inventory.RepositoryRoot))
        {
            throw new ImpactAnalysisException(
                "Impact manifest and Git inventory must belong to the same repository root.");
        }

        var decisionEvidence = manifest.Mappings.ToDictionary(
            mapping => mapping.Decision.StableId,
            _ => new HashSet<ImpactEvidence>(),
            StringComparer.OrdinalIgnoreCase);
        var changeMatches = new List<HashSet<string>>(inventory.Changes.Count);
        var changeUnknownMatches = new List<HashSet<string>>(inventory.Changes.Count);
        var evaluations = 0;
        var totalEvidence = 0;

        foreach (var change in inventory.Changes)
        {
            var matched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var unknown = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var mapping in manifest.Mappings)
            {
                foreach (var pattern in mapping.Patterns)
                {
                    foreach (var (path, role) in EnumeratePaths(change))
                    {
                        if (++evaluations > MaximumMatchEvaluations)
                        {
                            throw new ImpactAnalysisException(
                                $"Impact correlation exceeds the {MaximumMatchEvaluations}-evaluation complexity limit.");
                        }

                        if (!ImpactPathMatcher.IsMatch(pattern, path))
                        {
                            continue;
                        }

                        var evidence = new ImpactEvidence(
                            change.Scope,
                            change.Kind,
                            role,
                            path,
                            pattern,
                            mapping.Relationship,
                            mapping.Reason);
                        var evidenceSet = decisionEvidence[mapping.Decision.StableId];
                        if (evidenceSet.Add(evidence))
                        {
                            totalEvidence++;
                            if (totalEvidence > MaximumEvidenceEntries
                                || evidenceSet.Count > MaximumEvidencePerDecision)
                            {
                                throw new ImpactAnalysisException(
                                    "Impact evidence exceeds the bounded report limit; narrow the mapping patterns or change scope.");
                            }
                        }

                        if (mapping.Resolution.Kind is ImpactDecisionResolutionKind.Active
                            or ImpactDecisionResolutionKind.Proposed)
                        {
                            matched.Add(mapping.Decision.StableId);
                        }
                        else
                        {
                            unknown.Add(mapping.Decision.StableId);
                        }
                    }
                }
            }

            changeMatches.Add(matched);
            changeUnknownMatches.Add(unknown);
        }

        var decisions = manifest.Mappings
            .Select(mapping => BuildDecisionAssessment(
                mapping,
                decisionEvidence[mapping.Decision.StableId]))
            .OrderBy(assessment => assessment.Decision.StableId, StringComparer.Ordinal)
            .ThenBy(assessment => assessment.Decision.Path, StringComparer.Ordinal)
            .ToArray();

        var changes = inventory.Changes
            .Select((change, index) => BuildChangeAssessment(
                change,
                changeMatches[index],
                changeUnknownMatches[index]))
            .ToArray();
        var coverage = new ImpactCoverage(
            changes.Length,
            changes.Count(change => change.Status == ImpactAssessmentStatus.Affected),
            changes.Count(change => change.Status == ImpactAssessmentStatus.Unknown),
            changes.Count(change => change.Status == ImpactAssessmentStatus.NotMatched));

        return new ImpactAnalysisResult(
            inventory.RepositoryRoot,
            inventory.BaseReference,
            inventory.MergeBase,
            Path.GetRelativePath(inventory.RepositoryRoot, manifest.ManifestPath)
                .Replace(Path.DirectorySeparatorChar, '/'),
            Array.AsReadOnly(decisions),
            Array.AsReadOnly(changes),
            coverage);
    }

    private static ImpactDecisionAssessment BuildDecisionAssessment(
        ImpactMapping mapping,
        HashSet<ImpactEvidence> evidence)
    {
        var ordered = evidence
            .OrderBy(item => item.Path, StringComparer.Ordinal)
            .ThenBy(item => item.PathRole)
            .ThenBy(item => item.Scope)
            .ThenBy(item => item.ChangeKind)
            .ThenBy(item => item.Pattern, StringComparer.Ordinal)
            .ThenBy(item => item.Relationship)
            .ThenBy(item => item.Reason, StringComparer.Ordinal)
            .ToArray();
        var status = ordered.Length == 0
            ? ImpactAssessmentStatus.NotMatched
            : mapping.Resolution.Kind is ImpactDecisionResolutionKind.Active
                or ImpactDecisionResolutionKind.Proposed
                    ? ImpactAssessmentStatus.Affected
                    : ImpactAssessmentStatus.Unknown;

        return new ImpactDecisionAssessment(
            mapping.Decision,
            mapping.Resolution,
            status,
            Array.AsReadOnly(ordered));
    }

    private static ImpactChangeAssessment BuildChangeAssessment(
        GitChangedPath change,
        HashSet<string> matches,
        HashSet<string> unknownMatches)
    {
        var status = matches.Count > 0
            ? ImpactAssessmentStatus.Affected
            : unknownMatches.Count > 0
                ? ImpactAssessmentStatus.Unknown
                : ImpactAssessmentStatus.NotMatched;
        var decisions = matches
            .Concat(unknownMatches)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal)
            .ToArray();

        return new ImpactChangeAssessment(
            change,
            status,
            Array.AsReadOnly(decisions));
    }

    private static IEnumerable<(string Path, ImpactEvidencePathRole Role)> EnumeratePaths(
        GitChangedPath change)
    {
        if (change.OldPath is not null)
        {
            yield return (change.OldPath, ImpactEvidencePathRole.Previous);
        }

        if (change.NewPath is not null
            && !string.Equals(change.NewPath, change.OldPath, StringComparison.Ordinal))
        {
            yield return (change.NewPath, ImpactEvidencePathRole.Current);
        }
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);
}

internal static class ImpactPathMatcher
{
    internal static bool IsMatch(string pattern, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var patternSegments = pattern.Split('/');
        var pathSegments = path.Split('/');
        var previous = new bool[pathSegments.Length + 1];
        previous[0] = true;

        foreach (var patternSegment in patternSegments)
        {
            var current = new bool[pathSegments.Length + 1];
            if (patternSegment == "**")
            {
                current[0] = previous[0];
                for (var pathIndex = 1; pathIndex <= pathSegments.Length; pathIndex++)
                {
                    current[pathIndex] = previous[pathIndex] || current[pathIndex - 1];
                }
            }
            else
            {
                for (var pathIndex = 1; pathIndex <= pathSegments.Length; pathIndex++)
                {
                    current[pathIndex] = previous[pathIndex - 1]
                        && IsSegmentMatch(patternSegment, pathSegments[pathIndex - 1]);
                }
            }

            previous = current;
        }

        return previous[pathSegments.Length];
    }

    private static bool IsSegmentMatch(string pattern, string value)
    {
        var patternIndex = 0;
        var valueIndex = 0;
        var wildcardIndex = -1;
        var wildcardValueIndex = -1;

        while (valueIndex < value.Length)
        {
            if (patternIndex < pattern.Length
                && pattern[patternIndex] != '*'
                && CharactersEqual(pattern[patternIndex], value[valueIndex]))
            {
                patternIndex++;
                valueIndex++;
                continue;
            }

            if (patternIndex < pattern.Length && pattern[patternIndex] == '*')
            {
                wildcardIndex = patternIndex++;
                wildcardValueIndex = valueIndex;
                continue;
            }

            if (wildcardIndex >= 0)
            {
                patternIndex = wildcardIndex + 1;
                valueIndex = ++wildcardValueIndex;
                continue;
            }

            return false;
        }

        while (patternIndex < pattern.Length && pattern[patternIndex] == '*')
        {
            patternIndex++;
        }

        return patternIndex == pattern.Length;
    }

    private static bool CharactersEqual(char left, char right) =>
        OperatingSystem.IsWindows()
            ? char.ToUpperInvariant(left) == char.ToUpperInvariant(right)
            : left == right;
}
