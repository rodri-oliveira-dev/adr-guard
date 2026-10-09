# Por que usar ADRs?

[English](why-use-adrs.md) · [Início da documentação](../index.pt-BR.md) · [Anterior](what-is-an-adr.pt-BR.md) · [Próximo](when-to-write-an-adr.pt-BR.md)

ADRs tornam revisável e duradoura a justificativa de mudanças importantes. Seu valor está na conversa e na memória compartilhada que apoiam, não em produzir documentos por obrigação.

## Benefícios para pessoas e entregas

- **Justificativa da decisão:** revisores enxergam forças, alternativas e trade-offs.
- **Revisão melhor:** uma proposta focada oferece a segurança, operações, dados e produto um artefato concreto para questionar.
- **Onboarding e continuidade:** novos colaboradores entendem por que o sistema é assim sem reconstruir toda discussão.
- **Rastreabilidade:** uma decisão pode apontar para requisitos, experimentos, incidentes, ADRs relacionadas e implementação.
- **Segurança na mudança:** quando o contexto muda, a equipe revisita premissas originais em vez de preservar ou substituir cegamente o desenho.
- **Governança:** convenções leves de status e relacionamentos mostram quais decisões estão propostas, vigentes, históricas ou substituídas.

Esses benefícios apoiam desenvolvedores, arquitetos, lideranças técnicas, auditores e operações. ADRs também reduzem a dependência da memória de uma única pessoa. Elas não eliminam discordâncias; tornam a discordância e sua resolução mais fáceis de inspecionar.

## Impacto organizacional

Uma prática saudável cria uma trilha comum de decisões entre equipes. Ela pode esclarecer responsabilidades, expor dependências mais cedo e preservar conhecimento institucional durante reorganizações ou transições. Quando ADRs ficam com o código, a revisão normal do controle de versão oferece um histórico visível de propostas e mudanças.

As ADRs ainda precisam ser acessíveis a participantes que não usam Git diariamente. A equipe pode compartilhar links renderizados, envolver as pessoas relevantes na revisão e resumir decisões nos canais que elas utilizam.

## Limitações e custos

- Escrever e revisar uma boa ADR exige tempo.
- Registros se tornam enganosos quando ninguém mantém status ou links de substituição.
- ADRs demais para escolhas de baixo impacto criam ruído.
- Um template incentiva completude, mas não substitui evidência ou julgamento.
- O acesso ao repositório pode excluir participantes importantes sem inclusão deliberada.
- ADRs descrevem decisões; controles de implementação, operação e medição de resultados são trabalhos separados.

Use o registro mais leve que preserve justificativa suficiente. O próximo guia explica [quais escolhas merecem uma ADR](when-to-write-an-adr.pt-BR.md).
