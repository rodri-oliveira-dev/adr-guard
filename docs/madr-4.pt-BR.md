# Compatibilidade com MADR 4.0

> Disponibilidade: a validação MADR 4.0 está publicada no [ADR Guard v1.3.0](https://github.com/rodri-oliveira-dev/adr-guard/releases/tag/v1.3.0) e permanece opcional.

O ADR Guard implementa um subconjunto explícito alinhado aos [templates oficiais MADR 4.0.0](https://github.com/adr/madr/tree/4.0.0/template). O formato nunca é inferido:

```bash
adr-guard check docs/decisions --adr-format madr-4
adr-guard index docs/decisions --adr-format madr-4
```

A configuração equivalente é `adr-format: madr-4`. A opção explícita da CLI tem precedência. Omitir ambos mantém inalterado o formato canônico do ADR Guard.

## Estrutura obrigatória

- título de nível um;
- `Context and Problem Statement` de nível dois e não vazio;
- `Considered Options` de nível dois e não vazio;
- `Decision Outcome` de nível dois e não vazio;
- contrato existente de filename `NNNN-lowercase-kebab-case.md`.

O front matter MADR 4.0 (`status`, `date`, `decision-makers`, `consulted`, `informed`) é opcional. Se `status` existir, não pode estar vazio. As seções opcionais `Decision Drivers`, `Consequences`, `Confirmation`, `Pros and Cons of the Options` e `More Information` são aceitas. O ADR Guard não exige instruções/placeholders do template, não regrava arquivos e não migra ADRs canônicos.

Registros canônicos e MADR não devem ser misturados na mesma execução porque o formato se aplica ao conjunto validado. Use comandos separados para diretórios distintos. Metadata MADR é lida como dados inertes; não é uma superfície genérica de execução YAML.

Para substituição em MADR, `status: "superseded by ADR-0123"` resolve o ID único de quatro dígitos. Outros status permanecem texto definido pelo MADR; o ADR Guard interpreta somente essa forma explícita para integridade do grafo.
