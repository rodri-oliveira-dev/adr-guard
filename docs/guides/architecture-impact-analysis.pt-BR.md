# Análise de impacto arquitetural

`adr-guard impact` é uma análise opcional e somente leitura que correlaciona alterações Git com mapeamentos ADR-código explícitos. Ela é consultiva: uma decisão afetada é um convite à revisão, não uma violação de policy nem prova de não conformidade.

```bash
adr-guard impact . --base-ref origin/main --map .adrguard-impact.json
adr-guard impact . --base-ref origin/main --map .adrguard-impact.json --format json
```

A referência base e o arquivo de mapeamento são sempre explícitos. O comando não executa fetch ou checkout, não modifica arquivos nem altera o estado do Git. Ele inclui somente alterações observadas, commitadas desde o merge base, além de paths staged, unstaged e untracked. Uma análise completa retorna `0` mesmo com ADRs afetados ou alterações não mapeadas; argumentos inválidos retornam `2`, e falhas de repositório, Git, manifesto, limites ou cancelamento retornam `3`.

## Contratos e compatibilidade

O contrato de mapeamento é [`adr-impact-map-v1.schema.json`](../schemas/adr-impact-map-v1.schema.json). Paths usam barras normais relativas ao repositório, e padrões suportam literais, `*` dentro de um segmento e `**` como segmento completo. A identidade do ADR é resolvida pelo ID estável e path declarados; mapeamentos não resolvidos, inativos, renomeados ou ambíguos no ciclo de vida são `unknown`, sem serem tratados silenciosamente como governança vigente.

A saída JSON segue [`adr-impact-report-v1.schema.json`](../schemas/adr-impact-report-v1.schema.json); consulte o [exemplo reproduzível](../examples/adr-impact-report-v1.json). Nomes e significados dos campos são versionados por `schemaVersion: "1.0"`. Campos aditivos exigem evolução compatível; remoções ou mudanças semânticas exigem nova versão de schema. Esse contrato não altera os schemas JSON ou SARIF existentes de `check`.

Os relatórios são limitados a 10.000 alterações Git, 1.000 mapeamentos, 100 padrões por mapeamento, 1.000.000 de avaliações, 5.000 evidências totais e 500 evidências por decisão. A saída e duração dos comandos Git e os bytes do manifesto também têm limites.

## Estudo de caso reproduzível

O repositório de teste em `tests/AdrGuard.Tests/Fixtures/Impact/case-study` começa com ADRs canônicos Accepted e Proposed, mais um ADR Rejected com prefixo legado habilitado por configuração explícita da Fase 0. O conjunto de alterações modifica paths mapeados e não mapeados, renomeia um arquivo mapeado, exclui um path governado somente pela decisão inativa e modifica um Markdown comum que contém link para um ADR.

As saídas JSON e texto esperadas comprovam que:

- mapeamentos Accepted e Proposed são candidatos afetados, sem tratar Proposed como aprovação vigente;
- a decisão Rejected inativa produz `unknown` para o path excluído;
- os paths antigo e novo de rename permanecem evidências inspecionáveis;
- código não mapeado permanece visível como lacuna de cobertura;
- um link Markdown comum não cria identidade, mapeamento ou falso positivo de impacto.

## Modos de falha

Argumentos inválidos retornam `2`. Repositório ausente, histórico base ausente/desconectado/shallow, falha ou timeout do Git, manifestos inválidos/grandes demais, paths ou padrões inseguros, correlação excessiva e cancelamento retornam `3` sem relatório parcial de sucesso. Nesses casos, stdout JSON fica vazio e os diagnósticos vão para stderr. Uma análise completa retorna `0` independentemente dos achados.

## Trade-offs e evolução futura

Mapeamentos explícitos exigem manutenção, mas são revisáveis, determinísticos, independentes de linguagem e não inspecionam conteúdo-fonte. Reutilizar o CLI Git preserva semântica nativa de rename/copy ao custo de exigir Git no container de runtime. O subconjunto limitado de glob é menos expressivo que regex, reduzindo deliberadamente riscos de negação de serviço e interpretação.

A Fase 2 pode consumir esses contratos de domínio para fluxos de drift ou policy, mas deve preservar a distinção consultiva/unknown, o versionamento de schema, a ativação explícita, evidências limitadas e a separação dos contratos de validação existentes. Extensões possíveis incluem tendências de cobertura, metadados de ownership e campos de relatório versionados independentemente; vereditos automáticos de conformidade, inferência implícita, escrita oculta no repositório e descoberta por rede continuam fora desse contrato.

## Segurança e exposição de dados

A saída pode revelar paths relativos ao repositório, razões dos mapeamentos e títulos de ADRs. Trate todo valor derivado do repositório como não confiável ao encaminhar JSON ou texto para logs, workflow commands, HTML ou outras ferramentas. O comando não lê conteúdo de código como evidência nem envia dados pela rede. JSON é gravado somente em stdout; erros operacionais, somente em stderr.
