using System.Globalization;
using System.Text;
using AdrGuard.Configuration;
using AdrGuard.Drift;
using AdrGuard.Git;
using AdrGuard.Impact;
using AdrGuard.Model;
using AdrGuard.Parsing;
using AdrGuard.Validation;

namespace AdrGuard.Mcp;

internal sealed class McpWorkspace(McpServerOptions serverOptions)
{
    internal const int MaximumAdrBytes = 524_288;
    internal const int MaximumAdrFiles = 1000;
    internal const int MaximumSearchLimit = 100;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal ListAdrsResult ListAdrs(CancellationToken cancellationToken)
    {
        var context = LoadContext(cancellationToken);
        var adrs = context.Documents
            .Select(document => Summary(document.Document, context.DocumentFormat))
            .ToArray();
        return McpResponseLimit.Ensure(new ListAdrsResult("1.0", adrs.Length, adrs));
    }

    internal GetAdrResult GetAdr(string identifier, CancellationToken cancellationToken)
    {
        var context = LoadContext(cancellationToken);
        var document = ResolveDocument(context, identifier);
        return McpResponseLimit.Ensure(new GetAdrResult(
            "1.0",
            Summary(document.Document, context.DocumentFormat),
            ContentIsUntrusted: true,
            document.Markdown));
    }

    internal SearchAdrsResult SearchAdrs(
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length > 256 || query.Any(char.IsControl))
        {
            throw new ArgumentException("Search query must contain 1 through 256 printable characters.", nameof(query));
        }

        if (limit is < 1 or > MaximumSearchLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit),
                $"Search result limit must be between 1 and {MaximumSearchLimit}.");
        }

        var terms = query.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var context = LoadContext(cancellationToken);
        var matches = new List<SearchAdrMatch>();
        foreach (var candidate in context.Documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var match = CreateSearchMatch(candidate, context.DocumentFormat, terms);
            if (match is not null) matches.Add(match);
            if (matches.Count == limit) break;
        }

        return McpResponseLimit.Ensure(new SearchAdrsResult(
            "1.0",
            query,
            ExcerptsAreUntrusted: true,
            matches.Count,
            matches));
    }

    private SearchAdrMatch? CreateSearchMatch(
        LoadedAdr candidate,
        AdrFormat format,
        IReadOnlyList<string> terms)
    {
        var document = candidate.Document;
        var identifier = Identifier(document);
        var searchable = string.Join('\n', identifier, document.Title, candidate.Markdown);
        if (!terms.All(term => searchable.Contains(term, StringComparison.OrdinalIgnoreCase))) return null;

        var fields = new List<string>(3);
        if (terms.Any(term => identifier.Contains(term, StringComparison.OrdinalIgnoreCase)))
            fields.Add("identifier");
        if (terms.Any(term => document.Title?.Contains(term, StringComparison.OrdinalIgnoreCase) == true))
            fields.Add("title");
        if (terms.Any(term => candidate.Markdown.Contains(term, StringComparison.OrdinalIgnoreCase)))
            fields.Add("content");
        return new SearchAdrMatch(
            Summary(document, format),
            fields,
            fields.Contains("content", StringComparer.Ordinal)
                ? Excerpt(candidate.Markdown, terms)
                : null);
    }

    internal GetAdrRelationshipsResult GetRelationships(
        string? identifier,
        CancellationToken cancellationToken)
    {
        if (identifier?.Length > 1024 || identifier?.Any(char.IsControl) == true)
        {
            throw new ArgumentException("ADR identifier is too long or contains control characters.", nameof(identifier));
        }

        var context = LoadContext(cancellationToken);
        var sources = identifier is null
            ? context.Documents
            : [ResolveDocument(context, identifier)];
        var sourcePaths = sources.Select(item => item.Document.FilePath).ToHashSet(PathComparer);
        var graph = AdrRelationshipGraphBuilder.Build(
            context.Documents.Select(item => item.Document).ToArray(),
            context.ValidationOptions);
        var relationships = graph.Relationships
            .Where(edge => sourcePaths.Contains(edge.Source.FilePath))
            .Where(edge => context.DocumentFormat == AdrFormat.Madr4
                           || edge.Origin != AdrRelationshipOrigin.MadrMetadata)
            .Select(edge => new AdrRelationshipResult(
                Identifier(edge.Source),
                Relative(edge.Source.FilePath),
                edge.Type,
                edge.DeclaredTarget,
                edge.Resolution == AdrRelationshipResolution.Resolved
                    ? Identifier(edge.Targets[0])
                    : null,
                edge.Resolution == AdrRelationshipResolution.Resolved
                    ? Relative(edge.Targets[0].FilePath)
                    : null,
                edge.Resolution switch
                {
                    AdrRelationshipResolution.Resolved => "valid",
                    AdrRelationshipResolution.Ambiguous => "ambiguous",
                    _ => "unresolved",
                }))
            .ToArray();
        var issues = AdrValidator.Validate(
                context.Documents.Select(item => item.Document).ToArray(),
                additionalKnownPaths: null,
                context.ValidationOptions)
            .Issues
            .Where(issue => sourcePaths.Contains(issue.FilePath)
                            && AdrRelationshipGraphBuilder.IsRelationshipDiagnostic(issue.Code))
            .Select(issue => new AdrRelationshipIssue(
                issue.Code,
                issue.Message,
                Relative(issue.FilePath),
                Severity(issue.Severity)))
            .ToArray();

        var ordered = relationships
            .Distinct()
            .OrderBy(item => item.SourceIdentifier, StringComparer.Ordinal)
            .ThenBy(item => item.SourcePath, StringComparer.Ordinal)
            .ThenBy(item => item.Type, StringComparer.Ordinal)
            .ThenBy(item => item.DeclaredTarget, StringComparer.Ordinal)
            .ToArray();
        return McpResponseLimit.Ensure(new GetAdrRelationshipsResult("1.0", ordered, issues));
    }

    internal ValidateAdrsResult ValidateAdrs(
        string? validationProfile,
        CancellationToken cancellationToken)
    {
        var context = LoadContext(cancellationToken, validationProfile);
        var documents = context.Documents.Select(item => item.Document).ToArray();
        var result = AdrValidator.Validate(documents, additionalKnownPaths: null, context.ValidationOptions);
        var diagnostics = result.Issues.Select(issue => new ValidationDiagnosticResult(
            issue.Code,
            issue.Message,
            Relative(issue.FilePath),
            Severity(issue.Severity))).ToArray();
        var configuration = context.Configuration;
        var effectiveProfile = validationProfile ?? configuration?.ValidationProfile ?? "legacy";
        var policy = new ValidationPolicyResult(
            Format(context.DocumentFormat),
            effectiveProfile,
            context.ValidationOptions.ConventionalSupersession,
            configuration?.FilenamePolicy ?? "canonical",
            configuration?.PlaceholderPolicy ?? effectiveProfile switch
            {
                "advisory" or "standard" => "warn",
                "strict" => "error",
                _ => "off",
            },
            context.ValidationOptions.ValidateMetadata);
        return McpResponseLimit.Ensure(new ValidateAdrsResult(
            "1.0",
            result.IsValid,
            documents.Length,
            policy,
            diagnostics));
    }

    internal ImpactToolResult AnalyzeImpact(
        string baseReference,
        string manifestPath,
        CancellationToken cancellationToken)
    {
        ValidateToken(baseReference, nameof(baseReference), 512);
        ValidateRelativePath(manifestPath, nameof(manifestPath));
        var inventory = GitChangeInventoryReader.Read(
            serverOptions.RepositoryRoot,
            baseReference,
            cancellationToken);
        var manifest = ImpactManifestLoader.Load(inventory.RepositoryRoot, manifestPath);
        var report = ImpactReportFactory.Create(ImpactCorrelationService.Analyze(manifest, inventory));
        return McpResponseLimit.Ensure(new ImpactToolResult(
            report.SchemaVersion,
            report.Advisory,
            new ImpactRepositoryResult(report.Repository.BaseReference, report.Repository.MergeBase, report.Repository.ManifestPath),
            new ImpactSummaryResult(report.Summary.TotalChanges, report.Summary.AffectedDecisions, report.Summary.UnknownDecisions, report.Summary.NotMatchedDecisions),
            new ImpactCoverageResult(report.Coverage.AffectedChanges, report.Coverage.UnknownChanges, report.Coverage.NotMatchedChanges),
            new ImpactUncertaintyResult(report.Uncertainty.HasUnknowns, report.Uncertainty.DecisionStableIds, report.Uncertainty.Explanation),
            report.ChangedFiles.Select(item => new ImpactChangeResult(item.Scope, item.ChangeType, item.OldPath, item.NewPath, item.Similarity, item.Status, item.DecisionStableIds)).ToArray(),
            report.Decisions.Select(item => new ImpactDecisionResult(
                item.StableId,
                item.Path,
                item.Title,
                item.LifecycleStatus,
                item.Resolution,
                item.Status,
                item.Uncertainty,
                item.Evidence.Select(evidenceItem => new ImpactEvidenceResult(
                    evidenceItem.Scope,
                    evidenceItem.ChangeType,
                    evidenceItem.PathRole,
                    evidenceItem.Path,
                    evidenceItem.Pattern,
                    evidenceItem.Relationship,
                    evidenceItem.Reason)).ToArray())).ToArray()));
    }

    internal DriftToolResult VerifyArchitecture(
        string rulesPath,
        IReadOnlyList<string>? evidencePaths,
        string? baselinePath,
        string policy,
        CancellationToken cancellationToken)
    {
        ValidateRelativePath(rulesPath, nameof(rulesPath));
        if (baselinePath is not null) ValidateRelativePath(baselinePath, nameof(baselinePath));
        if (evidencePaths is { Count: > 20 })
        {
            throw new ArgumentException("At most 20 evidence paths may be supplied.", nameof(evidencePaths));
        }

        foreach (var evidencePath in evidencePaths ?? []) ValidateRelativePath(evidencePath, nameof(evidencePaths));
        var selectedPolicy = policy switch
        {
            "advisory" => DriftPolicy.Advisory,
            "enforce" => DriftPolicy.Enforce,
            _ => throw new ArgumentException("Drift policy must be advisory or enforce.", nameof(policy)),
        };

        var manifest = FitnessManifestLoader.Load(serverOptions.RepositoryRoot, rulesPath);
        var evidence = evidencePaths is null or { Count: 0 }
            ? null
            : ExternalFitnessEvidenceAdapter.Load(serverOptions.RepositoryRoot, evidencePaths, cancellationToken);
        var baseline = baselinePath is null
            ? null
            : DriftSnapshotService.Load(serverOptions.RepositoryRoot, baselinePath);
        var commit = TryReadCommit(cancellationToken);
        var analysis = DriftEngine.Analyze(
            manifest,
            evidence,
            baseline,
            commit,
            DateTimeOffset.UtcNow,
            cancellationToken);
        var evaluation = DriftPolicyEvaluator.Evaluate(analysis, manifest, selectedPolicy);
        var report = evaluation.Report;
        return McpResponseLimit.Ensure(new DriftToolResult(
            report.SchemaVersion,
            report.Policy,
            new DriftRepositoryResult(report.Repository.RulesManifest, report.Repository.Commit),
            new DriftCoverageResult(
                report.Coverage.TotalRules,
                report.Coverage.Pass,
                report.Coverage.Violations,
                report.Coverage.Unknown,
                report.Coverage.NotApplicable),
            report.Findings.Select(item => new DriftFindingResult(
                item.Fingerprint,
                item.RuleId,
                item.AdrStableId,
                item.Outcome,
                item.CheckType,
                item.Confidence,
                item.BaselineState,
                item.Explanation,
                item.Evidence.Select(evidenceItem => new DriftEvidenceResult(
                    evidenceItem.Kind,
                    evidenceItem.Source,
                    evidenceItem.Subject,
                    evidenceItem.Target,
                    evidenceItem.Line,
                    evidenceItem.Detail)).ToArray())).ToArray(),
            evaluation.Inconclusive,
            evaluation.PolicyFailed));
    }

    private WorkspaceContext LoadContext(
        CancellationToken cancellationToken,
        string? validationProfile = null)
    {
        var configuration = AdrGuardConfigurationLoader.Load(serverOptions.RepositoryRoot);
        var format = configuration?.AdrFormat == "madr-4" ? AdrFormat.Madr4 : AdrFormat.Canonical;
        var options = AdrValidationOptionsFactory.Create(
            format,
            serverOptions.RepositoryRoot,
            configuration?.LifecycleStatuses,
            configuration?.ConventionalSupersession ?? false,
            configuration?.FilenamePolicy,
            configuration?.PlaceholderPolicy,
            configuration?.ValidateMetadata ?? false,
            validationProfile ?? configuration?.ValidationProfile);
        var documents = LoadDocuments(options, format, cancellationToken);
        return new WorkspaceContext(configuration, format, options, documents);
    }

    private List<LoadedAdr> LoadDocuments(
        AdrValidationOptions validationOptions,
        AdrFormat format,
        CancellationToken cancellationToken)
    {
        var adrRoot = RepositoryPath.ResolveContained(
            serverOptions.RepositoryRoot,
            serverOptions.AdrDirectory,
            allowMissingLeaf: false);
        var enumerationOptions = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = false,
            AttributesToSkip = FileAttributes.ReparsePoint,
            ReturnSpecialDirectories = false,
        };
        var paths = Directory.EnumerateFiles(adrRoot, "*", enumerationOptions)
            .Where(path => string.Equals(Path.GetExtension(path), ".md", StringComparison.OrdinalIgnoreCase)
                           && !string.Equals(Path.GetFileName(path), "README.md", StringComparison.OrdinalIgnoreCase))
            .Select(path => RepositoryPath.ResolveContained(
                serverOptions.RepositoryRoot,
                Path.GetRelativePath(serverOptions.RepositoryRoot, path),
                allowMissingLeaf: false))
            .Order(StringComparer.Ordinal)
            .Take(MaximumAdrFiles + 1)
            .ToArray();
        if (paths.Length > MaximumAdrFiles)
        {
            throw new InvalidDataException($"ADR directory exceeds the {MaximumAdrFiles}-file limit.");
        }

        var documents = new List<LoadedAdr>(paths.Length);
        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length > MaximumAdrBytes)
            {
                throw new InvalidDataException(
                    $"ADR '{Relative(path)}' exceeds the {MaximumAdrBytes}-byte limit.");
            }

            var markdown = StrictUtf8.GetString(bytes);
            var document = AdrMarkdownParser.Parse(path, markdown);
            document = validationOptions.EffectiveFilenamePolicy.NormalizeIdentity(document);
            document = AdrStatusResolver.ForFormat([document], format)[0];
            documents.Add(new LoadedAdr(document, markdown));
        }

        return documents;
    }

    private LoadedAdr ResolveDocument(WorkspaceContext context, string identifier)
    {
        ValidateToken(identifier, nameof(identifier), 1024);
        var normalizedPath = identifier.Replace('\\', '/').TrimStart('.', '/');
        var matches = context.Documents.Where(candidate =>
                string.Equals(Identifier(candidate.Document), identifier, StringComparison.OrdinalIgnoreCase)
                || candidate.Document.Id?.ToString(CultureInfo.InvariantCulture) == identifier
                || string.Equals(Relative(candidate.Document.FilePath), normalizedPath, PathComparison)
                || string.Equals(
                    Path.GetRelativePath(
                            RepositoryPath.ResolveContained(serverOptions.RepositoryRoot, serverOptions.AdrDirectory, false),
                            candidate.Document.FilePath)
                        .Replace(Path.DirectorySeparatorChar, '/'),
                    normalizedPath,
                    PathComparison))
            .ToArray();
        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new ArgumentException("ADR identifier or path was not found in the configured ADR directory.", nameof(identifier)),
            _ => throw new ArgumentException("ADR identifier is ambiguous in the configured ADR directory.", nameof(identifier)),
        };
    }

    private string? TryReadCommit(CancellationToken cancellationToken)
    {
        try
        {
            return GitCommandRunner.RunBounded(
                    serverOptions.RepositoryRoot,
                    ["rev-parse", "--verify", "HEAD"],
                    TimeSpan.FromSeconds(5),
                    256,
                    cancellationToken)
                .Trim();
        }
        catch (GitOperationException)
        {
            return null;
        }
    }

    private AdrSummary Summary(AdrDocument document, AdrFormat format) => new(
        Identifier(document),
        document.Title,
        document.Status,
        Relative(document.FilePath),
        Format(format));

    private string Relative(string path) =>
        Path.GetRelativePath(serverOptions.RepositoryRoot, Path.GetFullPath(path))
            .Replace(Path.DirectorySeparatorChar, '/');

    private static string Identifier(AdrDocument document) =>
        document.StableId
        ?? (document.Id is null
            ? $"path:{document.FileName}"
            : $"ADR-{document.Id.Value.ToString(CultureInfo.InvariantCulture)}");

    private static string Format(AdrFormat format) => format == AdrFormat.Madr4 ? "madr-4" : "canonical";

    private static string Severity(ValidationSeverity severity) =>
        severity == ValidationSeverity.Warning ? "warning" : "error";

    private static string Excerpt(string content, IReadOnlyList<string> terms)
    {
        var index = terms.Select(term => content.IndexOf(term, StringComparison.OrdinalIgnoreCase))
            .Where(item => item >= 0)
            .DefaultIfEmpty(0)
            .Min();
        var start = Math.Max(0, index - 80);
        var length = Math.Min(240, content.Length - start);
        return string.Join(' ', content.Substring(start, length)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static void ValidateToken(string value, string parameterName, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > maximumLength
            || value.Any(char.IsControl))
        {
            throw new ArgumentException(
                $"{parameterName} must contain 1 through {maximumLength} printable characters.",
                parameterName);
        }
    }

    private static void ValidateRelativePath(string value, string parameterName)
    {
        ValidateToken(value, parameterName, 1024);
        if (Path.IsPathRooted(value) || value.Contains('\\'))
        {
            throw new ArgumentException($"{parameterName} must be a forward-slash repository-relative path.", parameterName);
        }
    }

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private sealed record LoadedAdr(AdrDocument Document, string Markdown);

    private sealed record WorkspaceContext(
        AdrGuardConfiguration? Configuration,
        AdrFormat DocumentFormat,
        AdrValidationOptions ValidationOptions,
        IReadOnlyList<LoadedAdr> Documents);
}
