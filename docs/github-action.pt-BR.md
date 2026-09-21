# Guia de consumo da GitHub Action

> **Status da publicação:** a Action reutilizável está publicada, e a tag de compatibilidade `v1` está publicada. Os exemplos abaixo com `rodri-oliveira-dev/adr-guard@v1` estão prontos para uso. A listagem no Marketplace é acompanhada separadamente e continua futura até ser publicada e verificada manualmente.

A composite Action do ADR Guard executa o container publicado do ADR Guard. O consumidor não precisa do .NET SDK, mas precisa de um runner Linux com Docker e deve fazer checkout do repositório antes.

## Inputs

| Input | Padrão | Valores aceitos |
| --- | --- | --- |
| `path` | `docs/adr` | Diretório de ADRs relativo ao repositório, dentro de `GITHUB_WORKSPACE`. Paths absolutos, diretórios inexistentes, traversal com `..` e escapes por symlink são rejeitados. |
| `command` | `check` | `check` ou `index`. |
| `version` | vazio | Versão exata opcional da imagem de runtime no formato `X.Y.Z` ou `vX.Y.Z`. Obrigatória quando o source da Action é fixado por SHA de commit ou branch. |

O contrato de exit codes do CLI é preservado: `0` sucesso, `1` falha de validação de ADR, `2` erro de uso/input e `3` erro operacional.

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

Exit code `3` indica falha operacional, como Docker indisponível ou imagem selecionada inexistente. O ambiente suportado é runner Linux com daemon Docker funcional.

No `index`, o runner precisa ser non-root porque a Action recusa deliberadamente executar o container gravável como UID 0.

Ao usar SHA ou branch, informe o input `version` exato. Ao usar `@v1`, o source da Action acompanha a tag de compatibilidade `v1` publicada e resolve a imagem de runtime `:1` correspondente.

## Releases e Marketplace

As release notes são publicadas na página de [Releases](https://github.com/rodri-oliveira-dev/adr-guard/releases).

**Listagem no Marketplace: futura.** A tag de compatibilidade `v1` está publicada. A verificação externa pré-release passou e está registrada nas [evidências de verificação externa](github-action-external-verification.pt-BR.md); a verificação de produção no Marketplace continua pendente até os workflows consumidores externos serem executados novamente contra o `@v1` publicado e a listagem ser publicada e verificada manualmente. Os pré-requisitos, a identidade proposta da listagem e os gates manuais do proprietário estão no [checklist de publicação no Marketplace](github-marketplace.pt-BR.md). Para suporte e relato de vulnerabilidades, consulte [../SUPPORT.md](../SUPPORT.md) e [../SECURITY.md](../SECURITY.md).
