# Verificação externa da GitHub Action

Este documento registra as evidências de consumidor independente da issue #49 do ADR Guard.

## Status

**Verificação externa pré-release: aprovada.**

**Verificação de produção no Marketplace: pendente.**

A Action já foi publicada como `v1.0.0`, e a tag de compatibilidade `v1` está publicada. As evidências de consumidor independente abaixo são anteriores a essa release; os critérios finais da #49 relativos ao Marketplace continuam pendentes até o proprietário publicar e verificar a listagem e os workflows consumidores externos serem executados novamente contra o `@v1` publicado.

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

A release `v1.0.0` e a tag de compatibilidade `v1` já estão publicadas. Para concluir a verificação de produção do consumidor e do Marketplace:

1. confirmar que as tags publicadas imutável `v1.0.0` e móvel `v1` da Action resolvem conforme esperado;
2. confirmar que as imagens exata (`:1.0.0`) e major (`:1`) correspondentes no GHCR podem ser baixadas publicamente;
3. publicar a listagem no Marketplace pelo fluxo autorizado descrito em [github-marketplace.pt-BR.md](github-marketplace.pt-BR.md);
4. registrar na issue #49 a URL real do Marketplace e a referência publicada da Action;
5. alterar os workflows externos da referência SHA + `version: 0.1.12` para:
   `uses: rodri-oliveira-dev/adr-guard@v1`;
6. remover o input explícito `version`;
7. executar novamente os dois workflows externos;
8. exigir sucesso do workflow válido e falha do inválido com annotation de arquivo;
9. verificar compatibilidade da referência/imagem major publicada;
10. substituir todos os avisos de "Marketplace futuro" pela URL verificada;
11. somente então encerrar #49 e o roadmap #50.

A URL final do Marketplace e a referência publicada devem ser copiadas do GitHub depois da publicação; nunca devem ser inferidas ou inventadas.
