namespace AdrGuard.Generation;

internal static class MadrBuiltInTemplates
{
    internal static MadrTemplateDefinition Get(
        string? name,
        string cultureName)
    {
        var resolvedName = name ?? AdrBuiltInTemplates.Minimal;
        if (resolvedName is not (AdrBuiltInTemplates.Minimal or AdrBuiltInTemplates.Extended))
        {
            throw new ArgumentException(
                $"Unsupported built-in MADR template '{name}'. Use 'minimal' or 'extended'.",
                nameof(name));
        }

        var ptBr = cultureName switch
        {
            "en-US" => false,
            "pt-BR" => true,
            _ => throw new ArgumentException(
                $"Unsupported template culture '{cultureName}'. Use 'en-US' or 'pt-BR'.",
                nameof(cultureName)),
        };

        var sections = new List<MadrTemplateSection>
        {
            Section(
                2,
                "Context and Problem Statement",
                "contextAndProblemStatement",
                ptBr,
                "Replace this guidance with the context, problem, constraints, and scope of the proposed decision.",
                "Substitua esta orientação pelo contexto, problema, restrições e escopo da decisão proposta."),
        };

        if (resolvedName == AdrBuiltInTemplates.Extended)
        {
            sections.Add(Section(
                2,
                "Decision Drivers",
                "decisionDrivers",
                ptBr,
                "List the requirements, constraints, and trade-offs that guide this choice.",
                "Liste os requisitos, restrições e compromissos que orientam esta escolha."));
        }

        sections.Add(Section(
            2,
            "Considered Options",
            "consideredOptions",
            ptBr,
            "List the viable options, including keeping the current solution when applicable.",
            "Liste as opções viáveis, incluindo manter a solução atual quando aplicável."));
        sections.Add(Section(
            2,
            "Decision Outcome",
            "decisionOutcome",
            ptBr,
            "Replace this guidance with the proposed outcome and why it best addresses the problem. This draft is not an approved decision.",
            "Substitua esta orientação pelo resultado proposto e explique por que ele resolve melhor o problema. Este rascunho não é uma decisão aprovada."));

        if (resolvedName == AdrBuiltInTemplates.Extended)
        {
            sections.Add(Section(
                3,
                "Consequences",
                "consequences",
                ptBr,
                "Record expected benefits, costs, limitations, and trade-offs.",
                "Registre benefícios, custos, limitações e compromissos esperados."));
            sections.Add(Section(
                3,
                "Confirmation",
                "confirmation",
                ptBr,
                "Describe how implementation of the proposed decision can be confirmed.",
                "Descreva como a implementação da decisão proposta pode ser confirmada."));
            sections.Add(Section(
                2,
                "Pros and Cons of the Options",
                "prosAndConsOfOptions",
                ptBr,
                "Compare the positive, neutral, and negative arguments for each option.",
                "Compare os argumentos positivos, neutros e negativos de cada opção."));
            sections.Add(Section(
                2,
                "More Information",
                "moreInformation",
                ptBr,
                "Add relevant evidence, follow-up information, and links; remove this section if none apply.",
                "Adicione evidências, informações de acompanhamento e links relevantes; remova esta seção se não se aplicar."));
        }

        return new MadrTemplateDefinition(
            resolvedName,
            cultureName,
            sections);
    }

    private static MadrTemplateSection Section(
        int level,
        string heading,
        string fieldId,
        bool ptBr,
        string english,
        string portuguese) =>
        new(
            level,
            heading,
            fieldId,
            ptBr ? $"[EDITAR: {portuguese}]" : $"[EDIT: {english}]");
}
