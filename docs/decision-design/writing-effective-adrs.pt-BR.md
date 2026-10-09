# Como escrever ADRs eficazes

[English](writing-effective-adrs.md) · [Início da documentação](../index.pt-BR.md) · [Anterior](choosing-a-template.pt-BR.md) · [Próximo](anti-patterns.pt-BR.md)

Uma boa ADR permite que alguém no futuro reconstrua a decisão sem ter participado da reunião original. Ela é específica o bastante para orientar a implementação e focada em um único resultado.

## Construa o argumento

1. **Nomeie o resultado.** Prefira “Usar PostgreSQL como sistema de registro de pedidos” a “Decisão de banco de dados”.
2. **Descreva o contexto de forma neutra.** Apresente problema, envolvidos, restrições, estado atual e evidências relevantes antes de defender uma opção.
3. **Identifique alternativas reais.** Inclua o estado atual quando viável. Em escolhas importantes, explique cada opção séria diante dos direcionadores.
4. **Declare a decisão de forma ativa.** Defina o que foi escolhido, seu escopo e limites. Evite “considerar usar”.
5. **Registre consequências equilibradas.** Inclua benefícios, custos, riscos, responsabilidade operacional, migração e o que ficará mais difícil.
6. **Torne a incerteza visível.** Registre premissas, evidências ausentes, experimentos e condições que exigiriam revisão.
7. **Convide as pessoas certas.** Inclua afetados por segurança, operações, dados, produto, custos ou contratos entre equipes.

## Afirmações boas e fracas

| Fraca | Mais forte |
| --- | --- |
| “Usar PostgreSQL porque é popular.” | “Usar PostgreSQL como sistema de registro de pedidos porque atualizações atômicas e restrições relacionais atendem transações e auditoria; a equipe aceita migração e responsabilidade operacional.” |
| “Kafka é escalável.” | “Usar Kafka para distribuição durável de eventos quando consumidores precisam de replay independente; não usá-lo para request/response síncrono.” |
| “Não há desvantagens.” | “A escolha adiciona backup, correções, monitoramento e plantão; a equipe de plataforma será responsável pela instância gerenciada.” |
| “Aprovado.” | “Aceito pelos responsáveis de serviço e plataforma após o ensaio demonstrar rollback dentro da janela acordada.” |

Mantenha links de evidências estáveis e resuma na ADR o fato crítico para a decisão. Remova instruções e placeholders pendentes antes de buscar aceitação.

A validação do ADR Guard confirma estrutura e relacionamentos esperados. `draft` e `review` assistidos por IA podem sugerir texto ou achados, mas não fornecem evidência, consentimento ou aprovação.
