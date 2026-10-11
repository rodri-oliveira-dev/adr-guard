# Estudo de caso reproduzível de drift arquitetural

O ADR aceito proíbe Domain → Infrastructure. O `Domain.csproj` versionado está conforme.

1. Execute `adr-guard drift . --rules .adrguard-fitness.json --format json`; o resultado é `pass`.
2. Substitua `src/Domain/Domain.csproj` por `Domain.violation.csproj.fixture`; repita em modo advisory para ver a referência e a linha exatas como `violation`.
3. Execute com `--policy enforce`; a nova violação determinística retorna exit 4.
4. Crie histórico somente por ação explícita do responsável: `adr-guard drift . --rules .adrguard-fitness.json --write-baseline drift-baseline.json`. Revise o novo arquivo antes de commitá-lo.
5. Restaure o projeto original (remova a referência proibida) e execute com `--baseline drift-baseline.json`; a violação anterior fica `resolved` e a regra atual fica `pass`.

Substituir o projeto por `Domain.unknown.csproj.fixture` produz `unknown` e exit 3 porque condições MSBuild não são avaliadas. Executar `.adrguard-fitness-with-external.json` sem `--evidence` também produz `unknown`, nunca pass. Esses resultados mostram o limite da análise, não uma arquitetura certificada: pessoas ainda decidem se a regra representa o ADR e se mudanças de baseline são aceitáveis.
