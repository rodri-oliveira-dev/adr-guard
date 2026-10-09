# Escolha um template ou formato de ADR

[English](choosing-a-template.md) · [Início da documentação](../index.pt-BR.md) · [Anterior](formats-and-templates.pt-BR.md) · [Próximo](writing-effective-adrs.pt-BR.md)

Escolha a opção mais leve que torne a decisão e seus trade-offs compreensíveis. A matriz diferencia suporte de escrita e suporte de validação.

| Opção | Melhor uso | Detalhe e trade-off | Suporte do ADR Guard |
| --- | --- | --- | --- |
| **Minimal** | Escolha focada, de complexidade baixa ou moderada, com justificativa clara | Menor cerimônia; o autor precisa incluir custos e justificativa nas seções centrais | Gerado por `new`/`draft` com `--template minimal`; validado pelo modo canônico padrão |
| **Extended** | Escolha de maior impacto, controversa, entre equipes ou com risco operacional | Direcionadores, opções, justificativa, consequências positivas/negativas, riscos e referências explícitos | Gerado por `new`/`draft` com `--template extended`; validado pelo modo canônico; não é MADR |
| **Custom** | A equipe precisa de seções canônicas extras ou perguntas próprias | Orientação flexível, mas com restrições estruturais e de placeholders; manutenção da equipe | Um arquivo local via `--template-file`; o resultado continua canônico e começa `Proposed` |
| **MADR 4.0** | Equipes que já usam MADR ou querem sua estrutura de opções e resultado | Formato externo com direcionadores e análise de opções; metadata e seções diferem do canônico | Validação e indexação opcionais com `--adr-format madr-4`; não há gerador MADR interno no `new` |

## Heurísticas de seleção

- Comece com **Minimal** quando contexto, escolha, justificativa e consequências equilibradas couberem nas seções centrais.
- Passe para **Extended** quando opções, atributos de qualidade, riscos, migração ou vários interessados precisarem de tratamento explícito.
- Use **Custom** para tornar recorrentes perguntas próprias da equipe sem abandonar o contrato canônico. Não o use para inventar um formato de validação.
- Use **MADR 4.0** quando interoperabilidade com MADR ou sua estrutura de resultado for uma escolha deliberada. Mantenha-o em conjunto separado.

Não escolha um template maior apenas para fazer uma decisão parecer importante. Uma ADR concisa e completa é melhor do que um documento longo cheio de placeholders.

## Limites exatos dos comandos

```bash
# Geração e validação canônicas
adr-guard new docs/adr --title "Escolher o sistema de registro de pedidos" --template minimal
adr-guard new docs/adr --title "Escolher o sistema de registro de pedidos" --template extended
adr-guard new docs/adr --title "Escolher o sistema de registro de pedidos" --template-file docs/templates/team.md
adr-guard check docs/adr

# Registros MADR escritos separadamente e validados em outro diretório
adr-guard check docs/decisions --adr-format madr-4
```

`minimal` e `en-US` são os padrões de `new`. Consulte [criação offline](../creation.pt-BR.md), [restrições de templates personalizados](../custom-templates.pt-BR.md) e [compatibilidade MADR](../madr-4.pt-BR.md) antes de adotar uma opção diferente.
