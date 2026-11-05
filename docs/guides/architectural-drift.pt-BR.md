# Drift arquitetural e evidências de fitness

`adr-guard drift` verifica regras arquiteturais estreitas ligadas a ADRs efetivos. A análise é local, determinística quando possível e separada da revisão consultiva por IA e da análise de impacto de mudanças.

```text
adr-guard drift . --rules .adrguard-fitness.json --format json
adr-guard drift . --rules .adrguard-fitness.json --policy enforce
```

A política padrão é `advisory`. O enforcement bloqueia somente uma violação nova, com `gate: true`, verificada localmente por `forbid-project-reference` (exit 4). Uma violação existente no baseline continua visível, mas não bloqueia. Falhas reportadas externamente nunca bloqueiam porque o produtor não é atestado. Qualquer resultado `unknown` retorna 3 nas duas políticas, impedindo que evidência ausente ou ambígua pareça um check verde. Uso inválido retorna 2.

Repita `--evidence caminho.json` para artefatos externos explícitos. Use `--write-baseline drift-baseline.json` para criar um snapshot e acrescente `--update-baseline` somente para uma substituição deliberada. `--baseline drift-baseline.json` apenas compara. Revise mudanças de baseline como código-fonte.

## Receita de CI

A Action composta publica o modo `drift` somente leitura em `@v1` e na versão exata `v1.10.0`. Nenhum secret ou permissão de escrita é necessário.

```yaml
permissions:
  contents: read
steps:
  - uses: actions/checkout@v7
    with:
      persist-credentials: false
  - uses: rodri-oliveira-dev/adr-guard@<commit-sha>
    with:
      version: 1.10.0
      command: drift
      drift-rules: .adrguard-fitness.json
      drift-policy: enforce
```

A Action solicita o relatório JSON da CLI, preserva os exits `0`, `2`, `3` e `4` e grava um summary limitado sem tomar uma nova decisão de policy. `drift-baseline` opcional lê um snapshot existente; `drift-evidence`, separado por linhas, aceita no máximo 20 arquivos JSON explícitos. Criação/atualização de baseline continua sendo uma operação deliberada pela CLI direta. Confirme uma release coordenada da Action antes de substituir `<commit-sha>` por uma tag de release. Consulte o [guia da Action](../github-action.pt-BR.md#verificação-de-drift-arquitetural-somente-no-source-de-desenvolvimento) e o [exemplo completo disponível no source](../examples/github-action-drift.yml).

## Limites

Somente relações estáticas e exatas de `ProjectReference` em `.csproj` são provadas localmente. Condições, propriedades MSBuild, curingas, imports, arquivos ausentes e XML inseguro viram `unknown`; o ADR Guard nunca avalia MSBuild. JSON externo é uma observação reportada com proveniência preservada, não uma atestação. Revisão humana continua necessária para decidir se a regra representa fielmente o ADR e se uma atualização de baseline é justificável.

Contratos: [regras de fitness v1](../fitness-rules-v1.md), [schema de regras](../schemas/adr-fitness-rules-v1.schema.json), [schema de evidência](../schemas/adr-fitness-evidence-v1.schema.json), [schema de snapshot](../schemas/adr-drift-snapshot-v1.schema.json) e [schema de relatório](../schemas/adr-drift-report-v1.schema.json).
