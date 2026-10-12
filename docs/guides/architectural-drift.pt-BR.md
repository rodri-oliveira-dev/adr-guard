# Drift arquitetural e evidências de fitness

`adr-guard drift` verifica regras arquiteturais estreitas ligadas a ADRs efetivos. A análise é local, determinística quando possível e separada da revisão consultiva por IA e da análise de impacto de mudanças.

```text
adr-guard drift . --rules .adrguard-fitness.json --format json
adr-guard drift . --rules .adrguard-fitness.json --policy enforce
```

A política padrão é `advisory`. O enforcement bloqueia somente uma violação nova, com `gate: true`, verificada localmente por `forbid-project-reference` (exit 4). Uma violação existente no baseline continua visível, mas não bloqueia. Falhas reportadas externamente nunca bloqueiam porque o produtor não é atestado. Qualquer resultado `unknown` retorna 3 nas duas políticas, impedindo que evidência ausente ou ambígua pareça um check verde. Uso inválido retorna 2.

Repita `--evidence caminho.json` para artefatos externos explícitos. Use `--write-baseline drift-baseline.json` para criar um snapshot e acrescente `--update-baseline` somente para uma substituição deliberada. `--baseline drift-baseline.json` apenas compara. Revise mudanças de baseline como código-fonte.

## Receita de CI

O contrato atual da Action composta permanece intencionalmente inalterado. Instale a .NET Tool em um passo comum de privilégio mínimo e execute drift diretamente; nenhum secret ou permissão de escrita é necessário.

```yaml
permissions:
  contents: read
steps:
  - uses: actions/checkout@v6
  - uses: actions/setup-dotnet@v5
    with:
      dotnet-version: 10.0.x
  - run: dotnet tool install --global RodriOliveira.AdrGuard --version 1.6.0
  - run: adr-guard drift . --rules .adrguard-fitness.json --policy enforce --format json
```

Este exemplo requer o ADR Guard v1.6.0 no NuGet.org. Confirme que a release coordenada foi publicada antes de executar o workflow; o merge do PR de implementação não publica o pacote por si só. O comando `drift` não faz chamadas de rede nem lê credenciais de provedores.

## Limites

Somente relações estáticas e exatas de `ProjectReference` em `.csproj` são provadas localmente. Condições, propriedades MSBuild, curingas, imports, arquivos ausentes e XML inseguro viram `unknown`; o ADR Guard nunca avalia MSBuild. JSON externo é uma observação reportada com proveniência preservada, não uma atestação. Revisão humana continua necessária para decidir se a regra representa fielmente o ADR e se uma atualização de baseline é justificável.

Contratos: [regras de fitness v1](../fitness-rules-v1.md), [schema de regras](../schemas/adr-fitness-rules-v1.schema.json), [schema de evidência](../schemas/adr-fitness-evidence-v1.schema.json), [schema de snapshot](../schemas/adr-drift-snapshot-v1.schema.json) e [schema de relatório](../schemas/adr-drift-report-v1.schema.json).
