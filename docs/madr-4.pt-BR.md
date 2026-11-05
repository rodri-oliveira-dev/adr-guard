# Compatibilidade com MADR 4.0

> Disponibilidade: a validação MADR 4.0 está publicada no [ADR Guard v1.3.0](https://github.com/rodri-oliveira-dev/adr-guard/releases/tag/v1.3.0) e permanece opcional.

> Estado de desenvolvimento: a geração MADR nativa está implementada nesta branch, mas não estará disponível em pacotes ou imagens publicados até que uma release posterior conclua seu fluxo de publicação.

O ADR Guard implementa um subconjunto explícito alinhado aos [templates oficiais MADR 4.0.0](https://github.com/adr/madr/tree/4.0.0/template). O formato nunca é inferido:

```bash
adr-guard check docs/decisions --adr-format madr-4
adr-guard index docs/decisions --adr-format madr-4
```

A configuração equivalente é `adr-format: madr-4`. A opção explícita da CLI tem precedência. Omitir ambos mantém inalterado o formato canônico do ADR Guard.

## Geração nativa

Gere um rascunho MADR mínimo ou estendido explicitamente e valide o mesmo
diretório com o formato correspondente:

```bash
adr-guard new docs/decisions --title "Adotar PostgreSQL" --adr-format madr-4 --culture pt-BR
adr-guard new docs/decisions --title "Adotar uma fila" --adr-format madr-4 --template extended --culture pt-BR --preview
adr-guard check docs/decisions --adr-format madr-4
```

O front matter gerado contém somente `status: "proposed"`; ele não registra
aprovação nem deriva uma data do relógio do sistema. `minimal` emite as três
seções obrigatórias. `extended` acrescenta orientação editável para
direcionadores, consequências, confirmação, análise de opções e informações
adicionais. `--culture pt-BR` localiza a orientação, mas os headings
estruturais permanecem invariantes.

`--preview` e `--dry-run` retornam o caminho provável e o Markdown exato sem
criar arquivo, índice, artefato temporário, lock ou reserva de ID. A configuração
`adr-format: madr-4` vale para `new`; `--adr-format canonical` explícito a
sobrescreve. O conteúdo de `--template-file` continua exclusivamente canônico
e é rejeitado na geração MADR.

O envelope schema `1.0` de `new --form` não muda. A seleção de formato fica
fora do JSON. Para preservar clientes existentes, MADR reutiliza IDs
publicados: `context`, `optionsConsidered` e `decision` mapeiam para as três
seções MADR obrigatórias; extended também expõe `decisionDrivers`,
`consequences` e `references` (`More Information`). Campos distintos para
Confirmation e análise de opções exigem uma futura versão do schema, não uma
mudança silenciosa em `1.0`.

## Estrutura obrigatória

- título de nível um;
- `Context and Problem Statement` de nível dois e não vazio;
- `Considered Options` de nível dois e não vazio;
- `Decision Outcome` de nível dois e não vazio;
- contrato existente de filename `NNNN-lowercase-kebab-case.md`.

O front matter MADR 4.0 (`status`, `date`, `decision-makers`, `consulted`, `informed`) é opcional. Se `status` existir, não pode estar vazio. As seções opcionais `Decision Drivers`, `Consequences`, `Confirmation`, `Pros and Cons of the Options` e `More Information` são aceitas. O ADR Guard não exige instruções/placeholders do template, não regrava arquivos e não migra ADRs canônicos.

Registros canônicos e MADR não devem ser misturados na mesma execução porque o formato se aplica ao conjunto validado. Use comandos separados para diretórios distintos. Metadata MADR é lida como dados inertes; não é uma superfície genérica de execução YAML.

Para substituição em MADR, `status: "superseded by ADR-0123"` resolve o ID único de quatro dígitos. Outros status permanecem texto definido pelo MADR; o ADR Guard interpreta somente essa forma explícita para integridade do grafo.
