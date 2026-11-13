namespace AdrGuard.Agent;

internal static class ArchitectureWorkflowCatalog
{
    internal static readonly string[] AuthorizedSkillNames =
    [
        "adr-guard-audit",
        "adr-guard-ci-setup",
        "adr-guard-create",
        "adr-guard-init",
        "adr-guard-lifecycle",
        "adr-guard-supersede",
        "adr-guard-team-adoption",
        "adr-guard-technical-review",
        "adr-guard-tradeoff-analysis",
        "adr-guard-validate",
        "adr-guard-when-to-record",
    ];

    internal static ArchitectureWorkflow IdentifyDecisionToRecord { get; } = Create(
        ArchitectureWorkflowKind.IdentifyDecisionToRecord,
        "Identify when to record a decision",
        "adr-guard-when-to-record");

    internal static ArchitectureWorkflow AnalyzeTradeOffs { get; } = Create(
        ArchitectureWorkflowKind.AnalyzeTradeOffs,
        "Analyze architectural trade-offs",
        "adr-guard-tradeoff-analysis");

    internal static ArchitectureWorkflow ConsultAndValidateAdrs { get; } = Create(
        ArchitectureWorkflowKind.ConsultAndValidateAdrs,
        "Consult and validate ADRs",
        "adr-guard-validate");

    internal static ArchitectureWorkflow AdvisoryTechnicalReview { get; } = Create(
        ArchitectureWorkflowKind.AdvisoryTechnicalReview,
        "Perform an advisory technical review",
        "adr-guard-technical-review");

    internal static ArchitectureWorkflow PrepareAdrProposal { get; } = Create(
        ArchitectureWorkflowKind.PrepareAdrProposal,
        "Prepare an ADR proposal",
        "adr-guard-create");

    private static ArchitectureWorkflow Create(
        ArchitectureWorkflowKind kind,
        string name,
        string skillName)
    {
        if (!AuthorizedSkillNames.Contains(skillName, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("The workflow references a skill outside the authorized catalog.");
        }

        return new ArchitectureWorkflow(
            kind,
            name,
            skillName,
            $"skills/{skillName}/SKILL.md");
    }
}
