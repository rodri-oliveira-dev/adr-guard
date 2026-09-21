namespace AdrGuard.Generation;

/// <summary>
/// Offline template catalog. Names and section headings are stable identifiers;
/// instructional text is deliberately editable and never implies acceptance.
/// Commands added later can map TryGet=false to a usage error.
/// </summary>
internal static class AdrBuiltInTemplates
{
    internal const string Minimal = "minimal";
    internal const string Extended = "extended";

    internal static IReadOnlyList<string> Names { get; } =
        [Minimal, Extended];

    internal static bool TryGet(
        string? name,
        string cultureName,
        out AdrTemplateDefinition? template)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cultureName);

        if (name is not null
            && name != Minimal
            && name != Extended)
        {
            template = null;
            return false;
        }

        template = Create(
            name ?? Minimal,
            cultureName);
        return true;
    }

    internal static AdrTemplateDefinition Get(
        string? name,
        string cultureName)
    {
        if (!TryGet(name, cultureName, out var template))
        {
            throw new ArgumentException(
                $"Unsupported built-in ADR template '{name}'. Use 'minimal' or 'extended'.",
                nameof(name));
        }

        return template!;
    }

    private static AdrTemplateDefinition Create(
        string name,
        string cultureName)
    {
        var ptBr = cultureName switch
        {
            "en-US" => false,
            "pt-BR" => true,
            _ => throw new ArgumentException(
                $"Unsupported template culture '{cultureName}'. Use 'en-US' or 'pt-BR'.",
                nameof(cultureName)),
        };

        var sections = new List<AdrTemplateSection>
        {
            new(
                "Context",
                "{{guidance-context}}\n\n"
                + Edit(ptBr,
                    "Replace this guidance with the problem, relevant constraints, and stakeholders. This template is only a starting point, not an approved architectural decision.",
                    "Substitua esta orientação pelo problema, pelas restrições relevantes e pelas partes interessadas. Este modelo é apenas um ponto de partida, não uma decisão arquitetural aprovada.")),
        };

        if (name == Extended)
        {
            sections.Add(new AdrTemplateSection(
                "Decision Drivers",
                Edit(ptBr,
                    "List the quality attributes, requirements, constraints, and trade-offs that guide this choice.",
                    "Liste os atributos de qualidade, requisitos, restrições e compromissos que orientam esta escolha.")));
            sections.Add(new AdrTemplateSection(
                "Options Considered",
                Edit(ptBr,
                    "List viable alternatives, including keeping the current solution, and summarize each option.",
                    "Liste alternativas viáveis, incluindo manter a solução atual, e resuma cada opção.")));
        }

        sections.Add(new AdrTemplateSection(
            "Decision",
            "{{guidance-decision}}\n\n"
            + Edit(ptBr,
                name == Minimal
                    ? "Replace this guidance with the proposed choice and why it addresses the problem."
                    : "State the proposed option, its scope, and the reasons it was selected over the alternatives.",
                name == Minimal
                    ? "Substitua esta orientação pela escolha proposta e explique como ela resolve o problema."
                    : "Descreva a opção proposta, seu escopo e os motivos para escolhê-la em vez das alternativas.")));

        if (name == Extended)
        {
            sections.Add(new AdrTemplateSection(
                "Rationale",
                Edit(ptBr,
                    "Explain how the proposed decision satisfies the drivers and where it falls short.",
                    "Explique como a decisão proposta atende aos critérios e em quais aspectos ela é insuficiente.")));
        }

        sections.Add(new AdrTemplateSection(
            "Consequences",
            "{{guidance-consequences}}\n\n"
            + Edit(ptBr,
                name == Minimal
                    ? "Replace this guidance with the expected benefits, costs, and trade-offs."
                    : "Summarize the expected impact, including operational and migration implications.",
                name == Minimal
                    ? "Substitua esta orientação pelos benefícios, custos e compromissos esperados."
                    : "Resuma o impacto esperado, incluindo implicações operacionais e de migração.")));

        if (name == Extended)
        {
            sections.Add(new AdrTemplateSection(
                "Positive Consequences",
                Edit(ptBr,
                    "Record anticipated benefits and the assumptions needed to realize them.",
                    "Registre os benefícios previstos e as premissas necessárias para alcançá-los.")));
            sections.Add(new AdrTemplateSection(
                "Negative Consequences",
                Edit(ptBr,
                    "Record limitations, added complexity, costs, and other downsides.",
                    "Registre limitações, complexidade adicional, custos e outras desvantagens.")));
            sections.Add(new AdrTemplateSection(
                "Risks",
                Edit(ptBr,
                    "Identify failure modes, uncertainties, mitigations, and accountable owners.",
                    "Identifique modos de falha, incertezas, mitigadores e responsáveis.")));
            sections.Add(new AdrTemplateSection(
                "References",
                Edit(ptBr,
                    "Add links to supporting evidence, relevant documents, and related ADRs; remove this instruction if none apply.",
                    "Adicione links para evidências, documentos relevantes e ADRs relacionados; remova esta orientação se não houver referências.")));
        }

        return new AdrTemplateDefinition(
            cultureName,
            sections);
    }

    private static string Edit(
        bool ptBr,
        string english,
        string portuguese) =>
        ptBr
            ? $"[EDITAR: {portuguese}]"
            : $"[EDIT: {english}]";
}
