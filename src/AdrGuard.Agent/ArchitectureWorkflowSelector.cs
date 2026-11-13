namespace AdrGuard.Agent;

internal sealed class ArchitectureWorkflowSelector : IArchitectureWorkflowSelector
{
    internal static ArchitectureWorkflowSelector Instance { get; } = new();

    public ArchitectureWorkflow Select(string question)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(question);

        if (ContainsAny(
                question,
                "prepare an adr", "prepare adr", "adr proposal", "draft an adr", "create an adr",
                "preparar uma adr", "preparar adr", "proposta de adr", "rascunho de adr", "criar uma adr"))
        {
            return ArchitectureWorkflowCatalog.PrepareAdrProposal;
        }

        if (ContainsAny(
                question,
                "technical review", "review adr", "review the adr", "assess adr",
                "revisão técnica", "revisao tecnica", "revisar adr", "revise a adr"))
        {
            return ArchitectureWorkflowCatalog.AdvisoryTechnicalReview;
        }

        if (ContainsAny(
                question,
                "should we record", "should this be an adr", "need an adr", "worth an adr",
                "registrar esta decisão", "registrar essa decisão", "registrar a decisão",
                "precisa de adr", "merece uma adr", "quando registrar"))
        {
            return ArchitectureWorkflowCatalog.IdentifyDecisionToRecord;
        }

        if (ContainsAny(
                question,
                "trade-off", "tradeoff", "alternatives", "compare options", "compare the options",
                "between the options", "prós e contras", "pros e contras", "alternativas",
                "comparar opções", "comparar opcoes", "entre as opções", "entre as opcoes"))
        {
            return ArchitectureWorkflowCatalog.AnalyzeTradeOffs;
        }

        return ArchitectureWorkflowCatalog.ConsultAndValidateAdrs;
    }

    private static bool ContainsAny(string value, params string[] fragments) =>
        fragments.Any(fragment => value.Contains(fragment, StringComparison.OrdinalIgnoreCase));
}
