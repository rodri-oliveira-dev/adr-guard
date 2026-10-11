using AdrGuard.Configuration;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace AdrGuard.Drift;

internal static class DotNetProjectReferenceChecker
{
    internal const long MaximumProjectBytes = 1_048_576;
    internal const int MaximumReferences = 10_000;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

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
            using var stream = new MemoryStream(StrictUtf8.GetBytes(StrictUtf8.GetString(File.ReadAllBytes(fromFull))));
            using var reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MaximumProjectBytes,
                IgnoreComments = true,
                Async = false,
            });
            var document = XDocument.Load(reader, LoadOptions.SetLineInfo);
            var references = document.Descendants().Where(x => x.Name.LocalName == "ProjectReference").ToArray();
            if (references.Length > MaximumReferences) return Unknown(rule, "ProjectReference count exceeds the bounded processing limit.");
            var evidence = new List<FitnessEvidence>();
            var ambiguous = false;
            foreach (var reference in references)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var include = reference.Attribute("Include")?.Value;
                var conditional = reference.AncestorsAndSelf().Any(x => x.Attribute("Condition") is not null);
                if (string.IsNullOrWhiteSpace(include) || conditional || include.Contains("$(", StringComparison.Ordinal) || include.IndexOfAny(['*', '?']) >= 0)
                {
                    ambiguous = true;
                    evidence.Add(new("project-reference-ambiguous", rule.EvidenceSource, rule.From, include, Line(reference), "Conditional, missing, wildcard, or property-expanded ProjectReference cannot be evaluated statically."));
                    continue;
                }
                string target;
                try
                {
                    var platformInclude = include
                        .Replace('\\', Path.DirectorySeparatorChar)
                        .Replace('/', Path.DirectorySeparatorChar);
                    var relativeToRoot = Path.GetRelativePath(root, Path.GetFullPath(platformInclude, Path.GetDirectoryName(fromFull)!)).Replace(Path.DirectorySeparatorChar, '/');
                    target = RepositoryPath.NormalizeRelative(root, relativeToRoot);
                }
                catch (Exception exception) when (exception is ArgumentException or IOException)
                {
                    ambiguous = true;
                    evidence.Add(new("project-reference-invalid", rule.EvidenceSource, rule.From, include, Line(reference), exception.Message));
                    continue;
                }
                if (string.Equals(target, rule.To, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                    evidence.Add(new("project-reference", rule.EvidenceSource, rule.From, rule.To, Line(reference), "Exact static ProjectReference matches the forbidden dependency."));
            }
            var violations = evidence.Where(x => x.Kind == "project-reference").ToArray();
            if (violations.Length > 0) return Result(rule, FitnessOutcome.Violation, "deterministic-static", "The forbidden project reference is present.", violations);
            if (ambiguous) return Result(rule, FitnessOutcome.Unknown, "incomplete-static", "One or more project references are not statically resolvable; absence cannot be proven.", evidence);
            return Result(rule, FitnessOutcome.Pass, "deterministic-static", "The complete static project-reference set does not contain the forbidden dependency.", [new("project-reference-set", rule.EvidenceSource, rule.From, rule.To, null, $"Inspected {references.Length} static ProjectReference element(s).")]);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or XmlException or DecoderFallbackException or ArgumentException)
        { return Unknown(rule, $"Project evidence could not be read safely: {exception.Message}"); }
    }

    private static int? Line(XElement element) => (element as IXmlLineInfo)?.HasLineInfo() == true ? ((IXmlLineInfo)element).LineNumber : null;
    private static FitnessRuleResult Unknown(FitnessRule rule, string explanation) => Result(rule, FitnessOutcome.Unknown, "none", explanation, []);
    private static FitnessRuleResult Result(FitnessRule rule, FitnessOutcome outcome, string confidence, string explanation, IReadOnlyList<FitnessEvidence> evidence) => new(rule.Id, rule.Decision.StableId, outcome, confidence, explanation, evidence);
}
