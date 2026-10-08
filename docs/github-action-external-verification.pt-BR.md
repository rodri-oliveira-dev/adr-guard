# Verificação externa da GitHub Action

Este documento registra as evidências de consumidor independente da issue #49 do ADR Guard.

## Status

**Verificação pré-release em repositório independente: aprovada.**

**Verificação de compatibilidade do `@v1` publicado em fixtures isoladas de consumidor: testada continuamente no CI do ADR Guard.**

**URL do Marketplace:** [https://github.com/marketplace/actions/adr-guard-architecture-decision-validator](https://github.com/marketplace/actions/adr-guard-architecture-decision-validator) informada pelo proprietário em 07/10/2026; o acesso público independente em sessão deslogada ainda precisa ser confirmado.

**Verificação do `@v1` publicado em repositório independente: aprovada em 07/10/2026.**

A tag de compatibilidade `v1` está publicada e acompanha a linha móvel `v1`; o `review` entrou nessa linha na `v1.1.6`. As evidências históricas abaixo são anteriores à primeira release `v1`, enquanto a nova execução em repositório independente testa o `@v1` publicado sem sobrescrever a versão da imagem.

## Verificação externa do `@v1` publicado (07/10/2026)

Consumidor: [`rodri-oliveira-dev/poc-arquitetura`](https://github.com/rodri-oliveira-dev/poc-arquitetura), branch isolada `test/adr-guard-action-49`, commit [`84ac7f4`](https://github.com/rodri-oliveira-dev/poc-arquitetura/commit/84ac7f436091314755294c8df66fcf7097d34382).

- [Workflow válido](https://github.com/rodri-oliveira-dev/poc-arquitetura/actions/runs/37707510792): **sucesso**. Jobs do diretório padrão `docs/adr` e do diretório customizado `.adr-guard-consumer/custom` passaram, incluindo as duas verificações de somente leitura com `git diff --exit-code`.
- [Workflow intencionalmente inválido](https://github.com/rodri-oliveira-dev/poc-arquitetura/actions/runs/37707510728): **falha esperada** (exit code `1`) com `ADR005` e annotation de erro do GitHub para `.adr-guard-consumer/invalid/0003-missing-decision.md`.
- Ambos usam `rodri-oliveira-dev/adr-guard@v1`, sem input explícito `version`, com `permissions: contents: read`.
- A imagem publicada `ghcr.io/rodri-oliveira-dev/adr-guard:1` foi baixada com sucesso segundo os logs.

Essa é uma evidência de consumo externo após a release, não uma confirmação independente de acesso público à página do Marketplace.

## Consumidor independente (verificação histórica pré-release)

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

## Verificação restante para encerramento

O proprietário forneceu [a URL do Marketplace](https://github.com/marketplace/actions/adr-guard-architecture-decision-validator) e a validação independente com `@v1` foi concluída (execuções acima). A documentação já referencia o endereço fornecido. Antes de encerrar a issue #49 e depois o roadmap #50, confirme o acesso à página em sessão deslogada e confira repositório e versão; registre essa evidência na #49. O sucesso do workflow não comprova, isoladamente, a acessibilidade da página do Marketplace.
