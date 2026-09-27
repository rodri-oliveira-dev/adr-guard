# Verificação externa da GitHub Action

Este documento registra as evidências de consumidor independente da issue #49 do ADR Guard.

## Status

**Verificação pré-release em repositório independente: aprovada.**

**Verificação de compatibilidade do `@v1` publicado em fixtures isoladas de consumidor: testada continuamente no CI do ADR Guard.**

**Verificação de produção no Marketplace: pendente de publicação pelo proprietário.**

A tag de compatibilidade `v1` está publicada e acompanha a linha móvel `v1`; o `review` entrou nessa linha na `v1.1.6`. As evidências de consumidor independente abaixo são anteriores à primeira release `v1`, enquanto o CI atual do ADR Guard exercita a referência remota real `rodri-oliveira-dev/adr-guard@v1` para `check` e `index` em diretórios de fixture limpos. A issue #49 mantém deliberadamente como critérios finais o rerun mais estrito em repositório independente e a listagem real no Marketplace.

## Consumidor independente

Repositório:

- https://github.com/rodri-oliveira-dev/poc-arquitetura

Branch isolada:

- `test/adr-guard-action-49`

A branch não altera a `main` do repositório consumidor.

Os fixtures cobrem:

- path padrão `docs/adr`;
- path customizado `.adr-guard-consumer/custom`;
- path intencionalmente inválido `.adr-guard-consumer/invalid`;
- `permissions: contents: read`;
- checkout com `persist-credentials: false`;
- comportamento somente leitura do `check`.

## Referência pré-release da Action

O rerun pré-release aprovado fixa o source da Action no commit:

`d1d164b5b70014e8101f7843c7cde13d7a19ae9f`

e seleciona explicitamente a imagem de runtime:

`ghcr.io/rodri-oliveira-dev/adr-guard:0.1.12`

Essa execução histórica **não** é apresentada como verificação de produção do `@v1`. Ela usou um SHA de commit com versão de imagem explícita porque a tag `@v1` ainda não havia sido publicada naquela ocasião. A tag já está publicada; os workflows consumidores externos ainda precisam ser executados novamente contra ela.

## Evidência do consumidor válido

Workflow:

- https://github.com/rodri-oliveira-dev/poc-arquitetura/actions/runs/35645607020

Resultado: **success**

Jobs verificados:

- `ADR Guard default path` — valida `docs/adr` usando os defaults da Action.
- `ADR Guard custom path` — valida `.adr-guard-consumer/custom`.

Evidências observadas:

- os dois jobs executam com permissão do `GITHUB_TOKEN` `Contents: read`;
- os dois validam um ADR compatível com sucesso;
- o path customizado é respeitado;
- cada job executa `git diff --exit-code` depois do `check`, provando que a validação não modificou o conteúdo selecionado.

## Evidência do consumidor inválido

Workflow:

- https://github.com/rodri-oliveira-dev/poc-arquitetura/actions/runs/35645606990

Resultado: **failure**, como esperado.

Evidências observadas:

- a permissão do `GITHUB_TOKEN` é `Contents: read`;
- o ADR inválido não possui a seção `Decision`;
- o ADR Guard emite `ADR005 ADR must define a non-empty 'Decision' section.`;
- a validação termina com exit code `1`;
- o GitHub renderiza o diagnóstico ADR como error annotation.

Isso confirma que um consumidor independente recebe o mesmo contrato de validação e as mesmas semânticas de falha do repositório ADR Guard.

## Bug encontrado no teste externo e corrigido

A primeira execução inválida revelou um defeito em runner frio:

- https://github.com/rodri-oliveira-dev/poc-arquitetura/actions/runs/35645392420

O job falhava corretamente com `ADR005`, mas não emitia annotation de arquivo. Em um runner limpo, o Docker baixava automaticamente a imagem ausente e escrevia o progresso do pull no mesmo stderr capturado dos diagnósticos do ADR Guard. O parser de annotations rejeitava corretamente essa saída misturada inesperada.

A Action foi reforçada para:

1. executar `docker pull` explicitamente antes de capturar a saída do CLI;
2. retornar exit code operacional `3` se a imagem exata não puder ser baixada;
3. executar o container de validação com `--pull=never`;
4. capturar somente stdout/stderr do ADR Guard para o parsing de diagnósticos.

No rerun externo corrigido, a annotation `ADR005` foi emitida como esperado.

Isso também melhora o comportamento da major móvel em runners self-hosted, pois o `@v1` publicado atualiza ativamente a imagem `:1` em vez de usar silenciosamente uma imagem local antiga.

## Gate final de verificação de produção

A tag móvel `v1` da Action e os canais públicos de release/container agora são cobertos pela [auditoria da release pública](public-release-audit.pt-BR.md) e pelos smoke tests de distribuição pública do CI. Para concluir os critérios mais estritos de Marketplace nas issues #49/#50:

1. publicar a listagem no Marketplace pelo fluxo autorizado descrito em [github-marketplace.pt-BR.md](github-marketplace.pt-BR.md);
2. registrar na issue #49 a URL canônica real do Marketplace e a referência publicada da Action;
3. atualizar os workflows independentes do `poc-arquitetura` da referência histórica SHA + `version: 0.1.12` para `uses: rodri-oliveira-dev/adr-guard@v1`;
4. remover o input explícito `version`;
5. executar novamente os dois workflows independentes;
6. exigir sucesso do workflow válido e falha do inválido com annotation de arquivo;
7. substituir os avisos explícitos de "Marketplace ainda não verificado" pela URL real da listagem;
8. somente então encerrar #49 e o roadmap #50.

A URL final do Marketplace e a referência publicada devem ser copiadas do GitHub depois da publicação; nunca devem ser inferidas ou inventadas.
