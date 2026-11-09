using AdrGuard.Model;

namespace AdrGuard.Validation;

internal static class AdrRelationshipValidator
{
    internal static void Validate(
        IReadOnlyList<AdrDocument> documents,
        List<ValidationIssue> issues,
        AdrLifecyclePolicy? lifecycle = null) =>
        Validate(
            AdrRelationshipGraphBuilder.Build(
                documents,
                new AdrValidationOptions(AdrFormat.Canonical, Lifecycle: lifecycle)),
            issues,
            new AdrValidationOptions(AdrFormat.Canonical, Lifecycle: lifecycle));

    internal static void Validate(
        AdrRelationshipGraph graph,
        List<ValidationIssue> issues,
        AdrValidationOptions options)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(issues);

        var lifecycle = options.EffectiveLifecycle;
        var successors = new Dictionary<AdrDocument, IReadOnlyList<AdrDocument>>();
        var relationshipsBySource = graph.Relationships.ToLookup(edge => edge.Source);

        foreach (var document in graph.Nodes)
        {
            var declared = relationshipsBySource[document]
                .Where(edge => edge.Type == "superseded-by")
                .ToArray();
            foreach (var unresolvedMadr in declared.Where(edge =>
                         edge.Origin == AdrRelationshipOrigin.MadrMetadata
                         && edge.Resolution != AdrRelationshipResolution.Resolved))
            {
                if (!AdrRelationshipGraphBuilder.TryParseMadrSupersedingId(document.Status, out var targetId))
                {
                    continue;
                }
                issues.Add(new ValidationIssue(
                    ValidationCodes.BrokenReference,
                    document.FilePath,
                    $"MADR supersession status references ADR-{targetId:D4}, which does not resolve to a unique ADR in the validated set."));
            }

            var targets = declared
                .Where(edge => edge.Resolution == AdrRelationshipResolution.Resolved)
                .SelectMany(edge => edge.Targets)
                .Distinct()
                .OrderBy(target => target.FilePath, StringComparer.Ordinal)
                .ToArray();
            successors[document] = targets;
        }

        foreach (var document in graph.Nodes)
        {
            var targets = successors[document];
            ValidateSupersessionTargets(document, targets, issues, lifecycle);
            ValidateSupersedesDeclarations(document, relationshipsBySource[document], successors, issues, lifecycle);
            ValidateDependencies(document, relationshipsBySource[document], issues, lifecycle);
        }

        ValidateCycles(successors, issues);
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
        IEnumerable<AdrRelationshipEdge> relationships,
        Dictionary<AdrDocument, IReadOnlyList<AdrDocument>> successors,
        List<ValidationIssue> issues,
        AdrLifecyclePolicy lifecycle)
    {
        foreach (var edge in relationships.Where(candidate =>
                     candidate.Type == "supersedes"
                     && candidate.Resolution == AdrRelationshipResolution.Resolved))
        {
            var predecessor = edge.Targets[0];

            var isPending = lifecycle.TryResolve(document.Status, out var successorKind)
                && successorKind == AdrLifecycleKind.Proposed;
            if (!isPending && !IsSuperseded(predecessor.Status, lifecycle))
            {
                issues.Add(new ValidationIssue(
                    ValidationCodes.InconsistentSupersession,
                    document.FilePath,
                    $"ADR declares that it supersedes '{predecessor.FileName}', but that ADR is not Superseded.",
                    Line: edge.Line,
                    Column: edge.Column));
            }

            if (successors.TryGetValue(predecessor, out var declared)
                && declared.Count > 0
                && !declared.Contains(document))
            {
                issues.Add(new ValidationIssue(
                    ValidationCodes.InconsistentSupersession,
                    document.FilePath,
                    $"ADR '{predecessor.FileName}' names a different superseding decision.",
                    Line: edge.Line,
                    Column: edge.Column));
            }
        }
    }

    private static void ValidateDependencies(
        AdrDocument document,
        IEnumerable<AdrRelationshipEdge> relationships,
        List<ValidationIssue> issues,
        AdrLifecyclePolicy lifecycle)
    {
        foreach (var edge in relationships.Where(candidate =>
                     candidate.Type == "depends-on"
                     && candidate.Resolution == AdrRelationshipResolution.Resolved))
        {
            var dependency = edge.Targets[0];
            if (!IsInactive(dependency.Status, lifecycle))
            {
                continue;
            }

            issues.Add(new ValidationIssue(
                ValidationCodes.InactiveDependency,
                document.FilePath,
                $"ADR explicitly depends on inactive decision '{dependency.FileName}' ({dependency.Status}).",
                Line: edge.Line,
                Column: edge.Column));
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

            FindCyclesFrom(start, successors, state, cycleNodes);
        }

        foreach (var document in cycleNodes.OrderBy(item => item.FilePath, StringComparer.Ordinal))
        {
            issues.Add(new ValidationIssue(
                ValidationCodes.SupersessionCycle,
                document.FilePath,
                "ADR participates in a cycle of superseding decisions."));
        }
    }

    private static void FindCyclesFrom(
        AdrDocument start,
        IReadOnlyDictionary<AdrDocument, IReadOnlyList<AdrDocument>> successors,
        Dictionary<AdrDocument, int> state,
        HashSet<AdrDocument> cycleNodes)
    {
        var path = new List<AdrDocument> { start };
        var positions = new Dictionary<AdrDocument, int> { [start] = 0 };
        var stack = new Stack<(AdrDocument Node, int Next)>();
        state[start] = 1;
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
            VisitSuccessor(edges[next], path, positions, stack, state, cycleNodes);
        }
    }

    private static void VisitSuccessor(
        AdrDocument target,
        List<AdrDocument> path,
        Dictionary<AdrDocument, int> positions,
        Stack<(AdrDocument Node, int Next)> stack,
        Dictionary<AdrDocument, int> state,
        HashSet<AdrDocument> cycleNodes)
    {
        state.TryGetValue(target, out var targetState);
        if (targetState == 0)
        {
            state[target] = 1;
            positions[target] = path.Count;
            path.Add(target);
            stack.Push((target, 0));
            return;
        }

        if (targetState != 1 || !positions.TryGetValue(target, out var cycleStart)) return;
        foreach (var cycleNode in path.Skip(cycleStart)) cycleNodes.Add(cycleNode);
    }

    private static bool IsSuperseded(string? status, AdrLifecyclePolicy lifecycle) =>
        lifecycle.TryResolve(status, out var kind) && kind == AdrLifecycleKind.Superseded
        || status?.StartsWith("superseded by ADR-", StringComparison.OrdinalIgnoreCase) == true
        || status?.StartsWith("superseded by [", StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsInactive(string? status, AdrLifecyclePolicy lifecycle) =>
        lifecycle.TryResolve(status, out var kind)
        && kind is AdrLifecycleKind.Superseded or AdrLifecycleKind.Deprecated or AdrLifecycleKind.Rejected;
}
