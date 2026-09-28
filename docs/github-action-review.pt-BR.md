# Revisão por IA na GitHub Action

A revisão assistida por IA de ADRs é uma extensão explicitamente opt-in da Action reutilizável do ADR Guard. O padrão continua sendo o `check` determinístico; habilitar `review` não altera workflows de validação existentes.

> **Disponibilidade:** `command: review` está publicado em `rodri-oliveira-dev/adr-guard@v1` desde a **v1.1.6**. Os exemplos com `@v1` abaixo são exemplos atuais de consumo. O `review` continua sendo opt-in explícito e não altera o caminho determinístico padrão de `check`.

## Limite de confiança

Use revisão com provider apenas em workflows confiáveis. O review exige Linux, Docker e Python 3 no runner porque a renderização segura de summary/annotations faz parte do contrato de sucesso. A Action aplica estes limites:

- `pull_request_target` é rejeitado para `review`;
- eventos `pull_request` vindos de forks são rejeitados antes da execução do Docker/provider;
- somente eventos `push`, `workflow_dispatch`, `schedule` e `pull_request` do mesmo repositório podem optar pela revisão; todos os outros tipos de evento são rejeitados antes da execução do Docker/provider;
- o checkout é montado como somente leitura em `review`;
- `GITHUB_TOKEN` e `GH_TOKEN` nunca são encaminhados ao container de revisão;
- somente a variável de ambiente de credencial associada ao provider selecionado é encaminhada, apenas pelo nome da variável;
- não há comentários automáticos em PR nem operações com token de escrita;
- o workflow precisa apenas de `permissions: contents: read`.

Para contribuições não confiáveis, execute o `check` determinístico no pull request e faça a revisão com provider depois do merge ou em um workflow manual/confiável separado.

## Inputs

Use `command: review` e informe:

| Input | Obrigatório | Padrão | Finalidade |
| --- | --- | --- | --- |
| `review-target` | sim | vazio | Arquivo Markdown do ADR relativo ao repositório. |
| `provider` | sim | vazio | `openai`, `anthropic`, `gemini` ou `openai-compatible`. |
| `model` | sim | vazio | Identificador do modelo do provider. |
| `endpoint` | quando exigido pelo provider compatível | vazio | Endpoint explícito OpenAI-compatible. |
| `context-files` | não | vazio | Arquivos `.md`/`.txt` relativos ao repositório, um por linha. |
| `include-existing-adrs` | não | `false` | Opt-in para contexto limitado de ADRs existentes. |
| `policy` | não | `advisory` | `advisory` ou `enforce` determinístico. |
| `policy-file` | com `policy: enforce` | vazio | JSON de policy determinística relativo ao repositório. |

Credenciais de provider não são inputs da Action. Passe a credencial selecionada como variável de ambiente no step da Action, por exemplo `OPENAI_API_KEY: ${{ secrets.OPENAI_API_KEY }}`.

## Exemplo confiável

```yaml
name: Revisão confiável de ADR por IA

on:
  workflow_dispatch:

permissions:
  contents: read

jobs:
  review:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v7
        with:
          persist-credentials: false

      - name: Revisar ADR selecionado
        uses: rodri-oliveira-dev/adr-guard@v1
        env:
          OPENAI_API_KEY: ${{ secrets.OPENAI_API_KEY }}
        with:
          command: review
          review-target: docs/adr/0007-cache-strategy.md
          provider: openai
          model: gpt-5
          context-files: |
            docs/architecture/security.md
            docs/architecture/availability.txt
          policy: advisory
```

Um `pull_request` do mesmo repositório também pode executar review quando o workflow fornece intencionalmente uma credencial de provider. PRs de fork são bloqueados mesmo se o workflow estiver configurado incorretamente para expor os inputs de review.

## Rede e filesystem

`check` e `index` preservam o comportamento existente com `--network=none`. Como `review` precisa acessar o provider, somente esse comando roda sem a restrição Docker `--network=none`.

O filesystem raiz do container e todo o checkout continuam somente leitura. A revisão não altera ADRs, status, índices nem grava arquivos de relatório.

## Relatórios e exit codes

Uma revisão bem-sucedida solicita JSON versionado ao CLI e converte o resultado em `GITHUB_STEP_SUMMARY`.

Achados de follow-up viram annotations GitHub do tipo `warning` somente quando a evidência pode ser associada a um arquivo local selecionado e verificado. A Action não inventa números de linha. Achados sem path local verificado, contexto ausente e incertezas permanecem apenas como texto advisory no summary.

A Action preserva o contrato de exit codes do CLI:

- `0`: revisão concluída; achados de IA continuam advisory;
- `1`: ADR selecionado falhou na validação estrutural determinística;
- `2`: configuração inválida da Action/CLI;
- `3`: falha de provider, transporte, cancelamento ou operação;
- `4`: enforcement explícito de policy determinística falhou.

O exit code `4` é distinto de achados do modelo. Falha do provider nunca é tratada como revisão limpa.

## Relato em pull request

Esta primeira versão usa apenas step summaries e annotations de arquivo. Ela não cria nem atualiza comentários no PR e, portanto, não exige `pull-requests: write` nem outra permissão de escrita.
