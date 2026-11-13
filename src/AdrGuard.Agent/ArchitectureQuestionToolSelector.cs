using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace AdrGuard.Agent;

internal sealed partial class ArchitectureQuestionToolSelector : IArchitectureQuestionToolSelector
{
    private static readonly HashSet<string> SearchStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "adr", "an", "and", "architecture", "architectural", "are", "as", "at", "chosen",
        "decision", "did", "do", "does", "for", "from", "how", "in", "is", "of", "on",
        "or", "our", "recorded", "the", "to", "was", "we", "were", "what", "which", "why",
        "about", "create", "prepare", "proposal", "write",
        "arquitetura", "arquitetural", "como", "com", "da", "das", "de", "decisão",
        "do", "dos", "e", "em", "escolhida", "escolhido", "foi", "foram", "na", "nas", "no",
        "nos", "nossa", "nosso", "o", "os", "ou", "para", "por", "qual", "quais", "que", "uma", "um",
        "criar", "escrever", "preparar", "proposta", "sobre",
    };

    internal static ArchitectureQuestionToolSelector Instance { get; } = new();

    [SuppressMessage(
        "Maintainability",
        "S3776:Cognitive Complexity of methods should not be too high",
        Justification = "The ordered closed-tool selection table is intentionally kept together for auditability.")]
    public ArchitectureToolSelection Select(
        AgentConfiguration configuration,
        ArchitectureWorkflow workflow)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(workflow);

        var question = configuration.Question;
        var identifier = ExtractIdentifier(question);
        var requests = new List<ArchitectureToolRequest>();
        var missingInformation = new List<string>();

        if (ContainsAnyWordPrefix(question, "impact", "impacto", "affected", "afetad", "change", "mudan"))
        {
            if (configuration.ImpactManifestPath is null)
            {
                missingInformation.Add(
                    "Impact analysis requires an explicit repository-relative --impact-manifest path.");
            }
            else
            {
                requests.Add(new ArchitectureToolRequest(ArchitectureToolKind.AnalyzeImpact));
            }
        }

        if (ContainsAny(question, "drift", "fitness", "architecture rule", "regra arquitet", "conform"))
        {
            if (configuration.ArchitectureRulesPath is null)
            {
                missingInformation.Add(
                    "Architecture verification requires an explicit repository-relative --architecture-rules path.");
            }
            else
            {
                requests.Add(new ArchitectureToolRequest(ArchitectureToolKind.VerifyArchitecture));
            }
        }

        if (ContainsAny(question, "valid", "diagnostic", "erro", "error", "check adr"))
        {
            requests.Add(new ArchitectureToolRequest(ArchitectureToolKind.ValidateAdrs));
        }

        if (workflow.Kind == ArchitectureWorkflowKind.AdvisoryTechnicalReview
            && requests.All(request => request.Kind != ArchitectureToolKind.ValidateAdrs))
        {
            requests.Add(new ArchitectureToolRequest(ArchitectureToolKind.ValidateAdrs));
        }

        if (ContainsAny(question, "relationship", "relation", "relaç", "depend", "supersed", "substitu"))
        {
            requests.Add(new ArchitectureToolRequest(
                ArchitectureToolKind.GetAdrRelationships,
                identifier));
        }

        if (ContainsAny(question, "list adr", "list all", "which adr", "quais adr", "listar adr", "todas as adr"))
        {
            requests.Add(new ArchitectureToolRequest(ArchitectureToolKind.ListAdrs));
        }

        if (identifier is not null
            && requests.All(request => request.Kind != ArchitectureToolKind.GetAdrRelationships))
        {
            requests.Add(new ArchitectureToolRequest(ArchitectureToolKind.GetAdr, identifier));
        }

        if (workflow.Kind == ArchitectureWorkflowKind.PrepareAdrProposal
            && requests.All(request => request.Kind is not (
                ArchitectureToolKind.ListAdrs
                or ArchitectureToolKind.GetAdr
                or ArchitectureToolKind.SearchAdrs)))
        {
            var relatedAdrQuery = CreateSearchQuery(question);
            requests.Add(relatedAdrQuery is null
                ? new ArchitectureToolRequest(ArchitectureToolKind.ListAdrs)
                : new ArchitectureToolRequest(ArchitectureToolKind.SearchAdrs, relatedAdrQuery));
        }

        if (requests.Count == 0 && missingInformation.Count == 0)
        {
            var searchQuery = CreateSearchQuery(question);
            requests.Add(searchQuery is null
                ? new ArchitectureToolRequest(ArchitectureToolKind.ListAdrs)
                : new ArchitectureToolRequest(ArchitectureToolKind.SearchAdrs, searchQuery));
        }

        return new ArchitectureToolSelection(
            requests.Distinct().ToArray(),
            missingInformation.AsReadOnly());
    }

    private static bool ContainsAny(string value, params string[] fragments) =>
        fragments.Any(fragment => value.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    private static bool ContainsAnyWordPrefix(string value, params string[] prefixes) =>
        SearchTermSeparatorRegex()
            .Split(value)
            .Any(word => prefixes.Any(prefix =>
                word.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));

    private static string? ExtractIdentifier(string question)
    {
        var match = AdrIdentifierRegex().Match(question);
        return match.Success ? $"ADR-{match.Groups[1].Value}" : null;
    }

    private static string? CreateSearchQuery(string question)
    {
        var terms = SearchTermSeparatorRegex()
            .Split(question)
            .Where(term => term.Length >= 3 && !SearchStopWords.Contains(term))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToArray();
        return terms.Length == 0 ? null : string.Join(' ', terms);
    }

    [GeneratedRegex(@"\bADR[-\s]?(\d{1,10})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AdrIdentifierRegex();

    [GeneratedRegex(@"[^\p{L}\p{Nd}-]+", RegexOptions.CultureInvariant)]
    private static partial Regex SearchTermSeparatorRegex();
}
