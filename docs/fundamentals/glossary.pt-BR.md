# Glossário de ADRs e referências

[English](glossary.md) · [Início da documentação](../index.pt-BR.md) · [Anterior](lifecycle.pt-BR.md)

## Glossário

| Termo | Significado |
| --- | --- |
| Decisão de arquitetura | Escolha com efeito relevante sobre estrutura, atributos de qualidade, dependências, interfaces, operação ou construção de um sistema. |
| ADR | Architecture Decision Record: registro durável de uma decisão, seu contexto e suas consequências. |
| Formato canônico | Formato padrão do ADR Guard, com seções de nível dois `Status`, `Context`, `Decision` e `Consequences`. |
| Categoria de decisão | Domínio afetado, como dados, segurança ou infraestrutura. Não determina o formato do arquivo. |
| Direcionador da decisão | Requisito, atributo de qualidade, restrição ou prioridade usado para comparar opções. |
| MADR | Markdown Architectural Decision Records, família externa de templates. O ADR Guard aceita um subconjunto explícito do MADR 4.0. |
| Template | Forma inicial e orientação usadas para escrever um registro. O ADR Guard gera registros canônicos Minimal, Extended ou Custom estruturalmente compatíveis. |
| Validação | Verificação determinística de estrutura e relacionamentos. Não é revisão nem aprovação arquitetural. |
| Substituir | Trocar uma decisão histórica por uma ADR mais nova preservando os dois registros e seu relacionamento. |

## Para aprofundar

- [Documenting Architecture Decisions](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions), de Michael Nygard, proposta original do formato curto de ADR.
- O site comunitário [Architecture Decision Records](https://adr.github.io/) para práticas, templates e ferramentas.
- O [catálogo de templates de ADR](https://adr.github.io/adr-templates/) para comparação educacional. Estar listado não significa que um formato seja suportado pelo ADR Guard.
- O [projeto MADR](https://adr.github.io/madr/) e seus [templates 4.0.0](https://github.com/adr/madr/tree/4.0.0/template).
- O [guia de seleção de formatos do ADR Guard](../decision-design/choosing-a-template.pt-BR.md) para saber exatamente o que é suportado.
