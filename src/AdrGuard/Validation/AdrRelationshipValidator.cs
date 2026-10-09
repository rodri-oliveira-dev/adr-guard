using AdrGuard.Model;
using System.Globalization;

namespace AdrGuard.Validation;

internal static class AdrRelationshipValidator
{
    private enum RelationProvenance
    {
        Section,
        StatusText,
        MadrMetadata,
    }

    private sealed record NormalizedRelation(AdrDocument Target, RelationProvenance Provenance, bool Effective);

    internal static void Validate(
        IReadOnlyList<AdrDocument> documents,
        List<ValidationIssue> issues,
        AdrLifecyclePolicy? lifecycle = null) =>
        Validate(documents, issues, new AdrValidationOptions(AdrFormat.Canonical, Lifecycle: lifecycle));

    internal static void Validate(
        IReadOnlyList<AdrDocument> documents,
        List<ValidationIssue> issues,
        AdrValidationOptions options)
    {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(issues);

        var lifecycle = options.EffectiveLifecycle;
        var byPath = documents.ToDictionary(
            document => Path.GetFullPath(document.FilePath),
            StringComparer.Ordinal);
        var byId = documents
            .Where(document => document.Id.HasValue)
            .GroupBy(document => document.Id!.Value)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single());
        var successors = new Dictionary<AdrDocument, IReadOnlyList<AdrDocument>>();

        foreach (var document in documents)
        {
            var targets = ResolveSupersedingTargets(document, byPath, byId, issues, options)
                .Select(relation => relation.Target)
                .Distinct()
                .OrderBy(target => target.FilePath, StringComparer.Ordinal)
                .ToArray();
            successors[document] = targets;
        }

        foreach (var document in documents)
        {
            var targets = successors[document];
            ValidateSupersessionTargets(document, targets, issues, lifecycle);
            ValidateSupersedesDeclarations(document, byPath, successors, issues, lifecycle);
            ValidateDependencies(document, byPath, issues, lifecycle);
        }

        ValidateCycles(successors, issues);
    }

    private static IEnumerable<NormalizedRelation> ResolveSupersedingTargets(
        AdrDocument document,
        Dictionary<string, AdrDocument> byPath,
        Dictionary<int, AdrDocument> byId,
        List<ValidationIssue> issues,
        AdrValidationOptions options)
    {
        foreach (var reference in AdrReference.FindInSections(document, "Superseded by"))
        {
            if (byPath.TryGetValue(reference.ResolvedPath, out var target))
            {
                yield return new NormalizedRelation(target, RelationProvenance.Section, Effective: true);
            }
        }

        if (options.ConventionalSupersession)
        {
            foreach (var reference in AdrReference.FindInStatusText(document, options.RepositoryRoot))
            {
                if (byPath.TryGetValue(reference.ResolvedPath, out var target))
                {
                    yield return new NormalizedRelation(target, RelationProvenance.StatusText, Effective: true);
                }
            }
        }

        if (!TryParseMadrSupersedingId(document.Status, out var targetId))
        {
            yield break;
        }

        if (byId.TryGetValue(targetId, out var madrTarget))
        {
            yield return new NormalizedRelation(madrTarget, RelationProvenance.MadrMetadata, Effective: true);
            yield break;
        }

        issues.Add(new ValidationIssue(
            ValidationCodes.BrokenReference,
            document.FilePath,
            $"MADR supersession status references ADR-{targetId:D4}, which does not resolve to a unique ADR in the validated set."));
    }

    private static void ValidateSupersessionTargets(
        AdrDocument document,
        IReadOnlyList<AdrDocument> targets,
        List<ValidationIssue> issues,
        AdrLifecyclePolicy lifecycle)
    {
        if (targets.Any(target => target == document))
        {
            issues.Add(new ValidationIssue(
                ValidationCodes.SelfSupersession,
                document.FilePath,
                "ADR must not declare itself as its superseding decision."));
        }

        if (targets.Count > 1)
        {
            issues.Add(new ValidationIssue(
                ValidationCodes.MultipleSuperseders,
                document.FilePath,
                $"ADR declares {targets.Count} distinct superseding decisions; exactly one is allowed."));
        }

        if (targets.Count > 0 && !IsSuperseded(document.Status, lifecycle))
        {
            issues.Add(new ValidationIssue(
                ValidationCodes.InconsistentSupersession,
                document.FilePath,
                "ADR declares a superseding decision but its status is not Superseded."));
        }

    }

    private static void ValidateSupersedesDeclarations(
        AdrDocument document,
        Dictionary<string, AdrDocument> byPath,
        Dictionary<AdrDocument, IReadOnlyList<AdrDocument>> successors,
        List<ValidationIssue> issues,
        AdrLifecyclePolicy lifecycle)
    {
        foreach (var reference in AdrReference.FindInSections(document, "Supersedes"))
        {
            if (!byPath.TryGetValue(reference.ResolvedPath, out var predecessor))
            {
                continue;
            }

            var isPending = lifecycle.TryResolve(document.Status, out var successorKind)
                && successorKind == AdrLifecycleKind.Proposed;
            if (!isPending && !IsSuperseded(predecessor.Status, lifecycle))
            {
                issues.Add(new ValidationIssue(
                    ValidationCodes.InconsistentSupersession,
                    document.FilePath,
                    $"ADR declares that it supersedes '{predecessor.FileName}', but that ADR is not Superseded."));
            }

            if (successors.TryGetValue(predecessor, out var declared)
                && declared.Count > 0
                && !declared.Contains(document))
            {
                issues.Add(new ValidationIssue(
                    ValidationCodes.InconsistentSupersession,
                    document.FilePath,
                    $"ADR '{predecessor.FileName}' names a different superseding decision."));
            }
        }
    }

    private static void ValidateDependencies(
        AdrDocument document,
        Dictionary<string, AdrDocument> byPath,
        List<ValidationIssue> issues,
        AdrLifecyclePolicy lifecycle)
    {
        foreach (var reference in AdrReference.FindInSections(document, "Depends on", "Dependencies"))
        {
            if (!byPath.TryGetValue(reference.ResolvedPath, out var dependency)
                || !IsInactive(dependency.Status, lifecycle))
            {
                continue;
            }

            issues.Add(new ValidationIssue(
                ValidationCodes.InactiveDependency,
                document.FilePath,
                $"ADR explicitly depends on inactive decision '{dependency.FileName}' ({dependency.Status})."));
        }
    }

    private static void ValidateCycles(
        IReadOnlyDictionary<AdrDocument, IReadOnlyList<AdrDocument>> successors,
        List<ValidationIssue> issues)
    {
        var state = new Dictionary<AdrDocument, int>();
        var cycleNodes = new HashSet<AdrDocument>();

        foreach (var start in successors.Keys.OrderBy(item => item.FilePath, StringComparer.Ordinal))
        {
            if (state.ContainsKey(start))
            {
                continue;
            }

            var path = new List<AdrDocument>();
            var positions = new Dictionary<AdrDocument, int>();
            var stack = new Stack<(AdrDocument Node, int Next)>();
            state[start] = 1;
            positions[start] = 0;
            path.Add(start);
            stack.Push((start, 0));

            while (stack.Count > 0)
            {
                var (node, next) = stack.Pop();
                var edges = successors[node];
                if (next >= edges.Count)
                {
                    state[node] = 2;
                    positions.Remove(node);
                    path.RemoveAt(path.Count - 1);
                    continue;
                }

                stack.Push((node, next + 1));
                var target = edges[next];
                state.TryGetValue(target, out var targetState);
                if (targetState == 0)
                {
                    state[target] = 1;
                    positions[target] = path.Count;
                    path.Add(target);
                    stack.Push((target, 0));
                }
                else if (targetState == 1 && positions.TryGetValue(target, out var cycleStart))
                {
                    foreach (var cycleNode in path.Skip(cycleStart))
                    {
                        cycleNodes.Add(cycleNode);
                    }
                }
            }
        }

        foreach (var document in cycleNodes.OrderBy(item => item.FilePath, StringComparer.Ordinal))
        {
            issues.Add(new ValidationIssue(
                ValidationCodes.SupersessionCycle,
                document.FilePath,
                "ADR participates in a cycle of superseding decisions."));
        }
    }

    private static bool TryParseMadrSupersedingId(string? status, out int id)
    {
        id = default;
        if (string.IsNullOrWhiteSpace(status)
            || !status.StartsWith("superseded by ADR-", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return int.TryParse(
            status["superseded by ADR-".Length..].Trim(),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out id)
            && id > 0;
    }

    private static bool IsSuperseded(string? status, AdrLifecyclePolicy lifecycle) =>
        lifecycle.TryResolve(status, out var kind) && kind == AdrLifecycleKind.Superseded
        || status?.StartsWith("superseded by ADR-", StringComparison.OrdinalIgnoreCase) == true
        || status?.StartsWith("superseded by [", StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsInactive(string? status, AdrLifecyclePolicy lifecycle) =>
        lifecycle.TryResolve(status, out var kind)
        && kind is AdrLifecycleKind.Superseded or AdrLifecycleKind.Deprecated or AdrLifecycleKind.Rejected;
}
