# Ciclo de vida de uma ADR

O ADR Guard mantém `Proposed`, `Accepted`, `Deprecated` e `Superseded` como padrão compatível. Valores adicionais são opt-in e devem mapear para um tipo semântico fechado: `proposed`, `accepted`, `rejected`, `deprecated` ou `superseded`. Por exemplo, `Rejected=rejected,Under Review=proposed` preserva propostas rejeitadas no histórico e trata uma etapa organizacional de revisão como ativa, mas não aceita. A validação nunca altera um status nem interpreta metadados como aprovação das partes interessadas.

[English](lifecycle.md) · [Início da documentação](../index.pt-BR.md) · [Anterior](when-to-write-an-adr.pt-BR.md) · [Próximo](glossary.pt-BR.md)

Uma ADR é um registro vivo com histórico preservado. A equipe discute e refina uma proposta, aceita-a conforme seu processo de governança e mantém o registro quando a decisão muda.

## Fluxo típico

1. **Identificar:** reconhecer uma escolha relevante e sua pessoa ou grupo responsável.
2. **Propor:** descrever contexto, opções viáveis, decisão recomendada e consequências.
3. **Revisar:** envolver pessoas afetadas, testar premissas e registrar trade-offs importantes.
4. **Aceitar ou recusar:** uma pessoa ou grupo autorizado decide. O ADR Guard nunca realiza essa aprovação.
5. **Implementar e observar:** relacionar entrega ou evidências quando útil e verificar as premissas.
6. **Revisitar:** marcar como descontinuada uma decisão desencorajada sem substituta direta ou substituí-la por uma nova ADR.

Equipes podem acrescentar estados como rejeitada em seu processo mais amplo, mas o validador canônico do ADR Guard aceita exatamente estes valores:

| Status canônico | Significado |
| --- | --- |
| `Proposed` | Em discussão e ainda não aprovada. `new` e `draft` criam este status. |
| `Accepted` | Aprovada pelo processo humano da equipe e atualmente aplicável. |
| `Deprecated` | Mantida como histórico, mas não mais recomendada; pode não haver substituta única. |
| `Superseded` | Substituída por uma ADR posterior. O registro canônico deve ter `Superseded by` com link para uma ADR do conjunto validado. |

O MADR 4.0 usa metadata opcional de status como texto. O ADR Guard não limita essa metadata aos quatro valores canônicos; ele interpreta a forma explícita `superseded by ADR-NNNN` para integridade de relacionamentos. Consulte os [limites de compatibilidade MADR](../madr-4.pt-BR.md).

## Preserve o histórico

Não reescreva silenciosamente uma ADR aceita para fazer uma nova escolha parecer antiga. Crie uma nova ADR proposta, relacione os registros, revise-a e depois atualize o status e o link da decisão anterior. Correções pequenas que não alteram a decisão podem usar o histórico normal do Git; mudanças materiais merecem um novo registro.

Validação estrutural significa que o registro segue regras conhecidas. Não significa que as evidências sejam sólidas, que as pessoas certas concordaram, que a implementação terminou ou que a decisão foi aprovada.
