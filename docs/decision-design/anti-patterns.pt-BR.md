# Anti-patterns de ADR

[English](anti-patterns.md) · [Início da documentação](../index.pt-BR.md) · [Anterior](writing-effective-adrs.pt-BR.md)

## Escolha de ferramenta sem problema

“Adotar Redis” não basta. Descreva primeiro o problema de latência, consistência, carga ou operação e mostre como a ferramenta se encaixa.

## Sem alternativas em uma escolha importante

Um registro relevante que nunca considera o estado atual ou uma alternativa viável esconde o trade-off. Use Extended ou MADR quando a comparação explícita melhorar a revisão.

## Consequências unilaterais

Benefícios sem custos parecem propaganda. Inclua complexidade, migração, operação, modos de falha, dependência e competências necessárias.

## Aceitação prematura

Marcar um rascunho como `Accepted` antes da revisão transforma status em uma afirmação sem governança. `new` e `draft` assistido por IA produzem `Proposed` intencionalmente; aceitar é ação humana.

## Reescrita silenciosa do histórico

Editar uma ADR aceita para descrever sua substituta apaga o que era verdade. Crie uma nova ADR e substitua a anterior com relacionamento explícito.

## Cemitério de templates

Deixar `[EDITAR]`, análises vazias ou texto genérico cria aparência de documentação sem conhecimento. Remova instruções, escreva conteúdo específico e use um template menor se as seções não agregarem valor.

## Uma ADR para várias decisões independentes

Um registro sobre armazenamento, autenticação, implantação e observabilidade fica difícil de revisar e substituir. Separe escolhas que evoluem independentemente e relacione-as quando o contexto se sobrepuser.

## Validação como aprovação

Um `adr-guard check` limpo prova apenas que regras estruturais determinísticas passaram. Não prova que a escolha é segura, viável, econômica, implementada ou aceita.

## ADRs como lei permanente

Uma ADR registra a melhor decisão para um contexto. Observe premissas e substitua a decisão quando o contexto mudar; preserve o histórico em vez de tratá-la como política imutável.
