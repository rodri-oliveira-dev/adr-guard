# Guia de consumo da GitHub Action

> **Status da publicação:** a Action reutilizável está publicada, e a tag móvel de compatibilidade `v1` está publicada. A linha pública `v1` suporta `check`, `index` e `review` opt-in desde a `v1.1.6`. `new` e `draft` continuam fluxos exclusivos de CLI/container. Ainda não foi verificada uma listagem pública no GitHub Marketplace; a publicação no Marketplace permanece um gate manual do proprietário documentado abaixo.

A composite Action do ADR Guard executa o container publicado do ADR Guard. O consumidor não precisa do .NET SDK. É necessário um runner Linux com Docker e checkout prévio do repositório; o `review` opt-in também exige Python 3 no runner para renderização segura de summary/annotations.

## Inputs

| Input | Padrão | Valores aceitos |
| --- | --- | --- |
| `path` | `docs/adr` | Diretório de ADRs relativo ao repositório, dentro de `GITHUB_WORKSPACE`. Paths absolutos, diretórios inexistentes, traversal com `..` e escapes por symlink são rejeitados. |
| `command` | `check` | `check`, `index` ou `review` explícito. |
| `version` | vazio | Versão exata opcional da imagem de runtime no formato `X.Y.Z` ou `vX.Y.Z`. Obrigatória quando o source da Action é fixado por SHA de commit ou branch. |
| `review-target` | vazio | Arquivo Markdown do ADR relativo ao repositório; obrigatório para `review`. |
| `provider` | vazio | Provider de revisão; obrigatório para `review`. |
| `model` | vazio | Identificador do modelo; obrigatório para `review`. |
| `endpoint` | vazio | Endpoint OpenAI-compatible opcional. |
| `context-files` | vazio | Contexto de review opcional em arquivos `.md`/`.txt` relativos ao repositório, um por linha. |
| `include-existing-adrs` | `false` | Opt-in explícito para contexto limitado de ADRs existentes. |
| `policy` | `advisory` | `advisory` ou `enforce` determinístico. |
| `policy-file` | vazio | JSON de policy determinística; obrigatório com `policy: enforce`. |

O contrato de exit codes do CLI é preservado: `0` sucesso, `1` falha de validação de ADR, `2` erro de uso/input, `3` falha operacional/provider e `4` falha de policy determinística de review.

A revisão com provider é opcional e não altera o comportamento padrão do `check`. Consulte [Revisão por IA na GitHub Action](github-action-review.pt-BR.md) para credenciais, eventos confiáveis, summaries, annotations e restrições de fork/`pull_request_target`.

## Validação de pull request

**Exemplo publicado com `@v1`:**

```yaml
name: Validação de ADRs

on:
  pull_request:
    branches:
      - main

permissions:
  contents: read

jobs:
  adr-guard:
    name: ADR Guard
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v7
        with:
          persist-credentials: false

      - name: Validar ADRs
        uses: rodri-oliveira-dev/adr-guard@v1
        with:
          path: docs/adr
          command: check
```

Uma cópia desse workflow está em [examples/github-action-pr.yml](examples/github-action-pr.yml).

## Validação da branch principal

Use a mesma validação somente leitura depois dos merges em `main`:

```yaml
name: Validação de ADRs

on:
  push:
    branches:
      - main

permissions:
  contents: read

jobs:
  adr-guard:
    name: ADR Guard
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v7
        with:
          persist-credentials: false

      - name: Validar ADRs
        uses: rodri-oliveira-dev/adr-guard@v1
        with:
          path: docs/adr
          command: check
```

Uma cópia desse workflow está em [examples/github-action-main.yml](examples/github-action-main.yml).

## Saída da validação e annotations

Falhas de validação mantêm os diagnósticos originais do CLI no log bruto. Diagnósticos reconhecidos `ADR001`–`ADR009` viram annotations de arquivo escapadas quando o path pode ser verificado dentro do diretório de ADR selecionado. Como o CLI não fornece linhas confiáveis, a Action não inventa números de linha.

A Action também grava um `GITHUB_STEP_SUMMARY` compacto com resultado, exit code, total de diagnósticos e contagem por regra. São emitidas no máximo 50 annotations por execução; o log bruto preserva todos os diagnósticos.

## `check` versus `index`

`check` é somente leitura: todo o checkout é montado como read-only.

`index` grava intencionalmente o `README.md` gerado dentro do diretório de ADR selecionado. O restante do checkout continua read-only. Um padrão comum no CI é:

```yaml
- name: Gerar índice de ADRs
  uses: rodri-oliveira-dev/adr-guard@v1
  with:
    path: docs/adr
    command: index

- name: Garantir que o índice gerado foi commitado
  run: git diff --exit-code -- docs/adr/README.md
```

A referência `@v1` acima usa a tag major móvel de compatibilidade já publicada.

## Pinning de versão

Escolha o modo de pinning conforme sua política de atualização:

- `@v1.2.3`: source da Action imutável daquela release exata e imagem de runtime exata `:1.2.3`.
- `@v1`: referência móvel de compatibilidade que acompanha releases `v1.x.y` bem-sucedidas mais novas e a imagem `:1`.
- `@<commit-sha>`: source da Action imutável; informe `version: 1.2.3` explicitamente porque o SHA não codifica a versão da imagem de runtime.

Nunca há fallback implícito para `latest`.

Consulte a [política de release da GitHub Action](github-action-release.pt-BR.md) e o [modelo de segurança](github-action-security.pt-BR.md).

## Permissões e checks obrigatórios

A própria Action só precisa que o conteúdo do repositório já tenha sido obtido pelo checkout. O workflow de validação pode usar:

```yaml
permissions:
  contents: read
```

Para tornar a validação de ADR obrigatória antes do merge, execute o workflow pelo menos uma vez para o GitHub conhecer o nome do check. Depois configure as regras da branch ou um ruleset para `main` e exija o status check produzido pelo job `ADR Guard`. Mantenha o nome do job estável para a regra continuar encontrando o check.

## Solução de problemas

Se a Action retornar exit code `2`, confira `path`, `command` e a combinação de versão/ref. Os paths precisam permanecer dentro do checkout.

Exit code `3` indica falha operacional, como Docker indisponível, imagem selecionada inexistente ou ausência de Python 3 no `review` opt-in. O ambiente suportado é runner Linux com daemon Docker funcional; `review` também exige Python 3.

No `index`, o runner precisa ser non-root porque a Action recusa deliberadamente executar o container gravável como UID 0.

Ao usar SHA ou branch, informe o input `version` exato. Ao usar `@v1`, o source da Action acompanha a tag de compatibilidade `v1` publicada e resolve a imagem de runtime `:1` correspondente.

## Releases e Marketplace

As release notes são publicadas na página de [Releases](https://github.com/rodri-oliveira-dev/adr-guard/releases).

**Listagem no Marketplace: ainda não verificada como pública.** A tag de compatibilidade `v1` está publicada e este repositório exercita continuamente o `@v1` público real em fixtures de consumidor isoladas. A verificação anterior em repositório independente continua registrada nas [evidências de verificação externa](github-action-external-verification.pt-BR.md). A listagem no Marketplace ainda exige que o proprietário autorizado conclua o fluxo de publicação na interface web do GitHub e registre a URL canônica; até isso acontecer, o repositório não afirma disponibilidade no Marketplace. Consulte a [auditoria da release pública](public-release-audit.pt-BR.md) e o [checklist de publicação no Marketplace](github-marketplace.pt-BR.md). Para suporte e relato de vulnerabilidades, consulte [../SUPPORT.md](../SUPPORT.md) e [../SECURITY.md](../SECURITY.md).
