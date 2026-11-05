using System.Text;
using System.Xml;
using System.Xml.Linq;
using AdrGuard.Configuration;

namespace AdrGuard.Drift;

internal static class DotNetProjectReferenceChecker
{
    internal const long MaximumProjectBytes = 1_048_576;
    internal const int MaximumReferences = 10_000;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly string[] ImplicitBuildImportFileNames = ["Directory.Build.props", "Directory.Build.targets"];

    internal static IReadOnlyList<FitnessRuleResult> Check(FitnessManifest manifest, CancellationToken cancellationToken)
    {
        var results = new List<FitnessRuleResult>();
        foreach (var rule in manifest.Rules.Where(x => x.CheckType == FitnessCheckType.ForbidProjectReference))
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(CheckRule(manifest.RepositoryRoot, rule, cancellationToken));
        }
        return results.OrderBy(x => x.AdrStableId, StringComparer.Ordinal).ThenBy(x => x.RuleId, StringComparer.Ordinal).ToArray();
    }

    private static FitnessRuleResult CheckRule(string root, FitnessRule rule, CancellationToken cancellationToken)
    {
        if (rule.Decision.State == FitnessDecisionState.Inactive) return Result(rule, FitnessOutcome.NotApplicable, "none", rule.Decision.Detail!, []);
        if (rule.Decision.State == FitnessDecisionState.Unknown) return Result(rule, FitnessOutcome.Unknown, "none", rule.Decision.Detail!, []);
        try
        {
            var fromFull = RepositoryPath.ResolveContained(root, rule.From!, false);
            _ = RepositoryPath.ResolveContained(root, rule.To!, false);
            var info = new FileInfo(fromFull);
            if (!info.Exists) return Unknown(rule, "A scoped project file is missing.");
            if (info.Length > MaximumProjectBytes) return Unknown(rule, $"Project exceeds the {MaximumProjectBytes}-byte limit.");
            cancellationToken.ThrowIfCancellationRequested();
            var document = LoadProject(fromFull);
            var references = document.Descendants().Where(x => x.Name.LocalName == "ProjectReference").ToArray();
            if (references.Length > MaximumReferences) return Unknown(rule, "ProjectReference count exceeds the bounded processing limit.");
            var evidence = new List<FitnessEvidence>();
            var ambiguous = AddImportEvidence(root, fromFull, rule, document, evidence);
            foreach (var reference in references)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ambiguous |= InspectReference(root, fromFull, rule, reference, evidence);
            }
            return CreateResult(rule, references.Length, ambiguous, evidence);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or XmlException or DecoderFallbackException or ArgumentException)
        { return Unknown(rule, $"Project evidence could not be read safely: {exception.Message}"); }
    }

    private static XDocument LoadProject(string path)
    {
        using var stream = new MemoryStream(StrictUtf8.GetBytes(StrictUtf8.GetString(File.ReadAllBytes(path))));
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaximumProjectBytes,
            IgnoreComments = true,
            Async = false,
        });
        return XDocument.Load(reader, LoadOptions.SetLineInfo);
    }

    private static bool AddImportEvidence(
        string root,
        string projectPath,
        FitnessRule rule,
        XDocument document,
        List<FitnessEvidence> evidence)
    {
        var imports = document.Descendants().Where(element => element.Name.LocalName == "Import").ToArray();
        var implicitImports = FindImplicitBuildImports(root, projectPath);
        if (imports.Length > 0)
        {
            var import = imports[0];
            evidence.Add(new(
                "project-import",
                rule.EvidenceSource,
                rule.From,
                import.Attribute("Project")?.Value,
                Line(import),
                $"Project declares {imports.Length} explicit Import element(s); imported project references are not evaluated."));
        }

        if (implicitImports.Length > 0)
        {
            evidence.Add(new(
                "project-import",
                rule.EvidenceSource,
                rule.From,
                implicitImports[0],
                null,
                $"Found {implicitImports.Length} implicit Directory.Build import(s); imported project references are not evaluated."));
        }

        return imports.Length > 0 || implicitImports.Length > 0;
    }

    private static bool InspectReference(
        string root,
        string projectPath,
        FitnessRule rule,
        XElement reference,
        List<FitnessEvidence> evidence)
    {
        var include = reference.Attribute("Include")?.Value;
        var conditional = reference.AncestorsAndSelf().Any(element => element.Attribute("Condition") is not null);
        var excluded = reference.Attribute("Exclude") is not null;
        if (string.IsNullOrWhiteSpace(include) || conditional || excluded || IsDynamicInclude(include))
        {
            evidence.Add(new(
                "project-reference-ambiguous",
                rule.EvidenceSource,
                rule.From,
                include,
                Line(reference),
                "Conditional, excluded, missing, wildcard, expression-expanded, or multi-item ProjectReference cannot be evaluated statically."));
            return true;
        }

        string target;
        try
        {
            var platformInclude = include
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.GetFullPath(platformInclude, Path.GetDirectoryName(projectPath)!);
            var relativeToRoot = Path.GetRelativePath(root, fullPath).Replace(Path.DirectorySeparatorChar, '/');
            target = RepositoryPath.NormalizeRelative(root, relativeToRoot);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException)
        {
            evidence.Add(new(
                "project-reference-invalid",
                rule.EvidenceSource,
                rule.From,
                include,
                Line(reference),
                exception.Message));
            return true;
        }

        if (string.Equals(target, rule.To, PathComparison))
        {
            evidence.Add(new(
                "project-reference",
                rule.EvidenceSource,
                rule.From,
                rule.To,
                Line(reference),
                "Exact static ProjectReference matches the forbidden dependency."));
        }

        return false;
    }

    private static FitnessRuleResult CreateResult(
        FitnessRule rule,
        int referenceCount,
        bool ambiguous,
        IReadOnlyList<FitnessEvidence> evidence)
    {
        var violations = evidence.Where(item => item.Kind == "project-reference").ToArray();
        if (violations.Length > 0)
        {
            return Result(
                rule,
                FitnessOutcome.Violation,
                "deterministic-static",
                "The forbidden project reference is present.",
                violations);
        }

        if (ambiguous)
        {
            return Result(
                rule,
                FitnessOutcome.Unknown,
                "incomplete-static",
                "One or more project references are not statically resolvable; absence cannot be proven.",
                evidence);
        }

        return Result(
            rule,
            FitnessOutcome.Pass,
            "deterministic-static",
            "The complete static project-reference set does not contain the forbidden dependency.",
            [new(
                "project-reference-set",
                rule.EvidenceSource,
                rule.From,
                rule.To,
                null,
                $"Inspected {referenceCount} static ProjectReference element(s).")]);
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static bool IsDynamicInclude(string include) =>
        include.Contains("$(", StringComparison.Ordinal)
        || include.Contains("@(", StringComparison.Ordinal)
        || include.Contains("%(", StringComparison.Ordinal)
        || include.Contains(';', StringComparison.Ordinal)
        || include.IndexOfAny(['*', '?']) >= 0;

    private static string[] FindImplicitBuildImports(string root, string projectPath)
    {
        var imports = new List<string>();
        var directory = new DirectoryInfo(Path.GetDirectoryName(projectPath)!);
        while (directory is not null)
        {
            foreach (var fileName in ImplicitBuildImportFileNames)
            {
                var candidate = Path.Combine(directory.FullName, fileName);
                if (File.Exists(candidate))
                    imports.Add(Path.GetRelativePath(root, candidate).Replace(Path.DirectorySeparatorChar, '/'));
            }
            if (string.Equals(directory.FullName, root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) break;
            directory = directory.Parent;
        }
        return imports.Order(StringComparer.Ordinal).ToArray();
    }

    private static int? Line(XElement element) => (element as IXmlLineInfo)?.HasLineInfo() == true ? ((IXmlLineInfo)element).LineNumber : null;
    private static FitnessRuleResult Unknown(FitnessRule rule, string explanation) => Result(rule, FitnessOutcome.Unknown, "none", explanation, []);
    private static FitnessRuleResult Result(FitnessRule rule, FitnessOutcome outcome, string confidence, string explanation, IReadOnlyList<FitnessEvidence> evidence) => new(rule.Id, rule.Decision.StableId, outcome, confidence, explanation, evidence);
}
