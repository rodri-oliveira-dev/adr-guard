# Modelo de segurança da GitHub Action

A GitHub Action reutilizável do ADR Guard mantém a validação determinística de ADRs como padrão, permite geração explícita do índice e expõe `review` assistido por IA somente como operação opt-in. Ela executa o container publicado no GHCR e não expõe o comando de IA `draft`.

## Permissões mínimas

Um workflow consumidor precisa apenas de leitura do repositório para fazer checkout dos arquivos:

```yaml
permissions:
  contents: read

jobs:
  adr-guard:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@<commit-fixado>
        with:
          persist-credentials: false

      - uses: rodri-oliveira-dev/adr-guard@vX.Y.Z
        with:
          command: check
          path: docs/adr
```

O ADR Guard não chama a API do GitHub, não solicita escrita no repositório e não precisa de `GITHUB_TOKEN` dentro do container. A Action não encaminha o ambiente do runner para o Docker.

## Acesso ao registry

A imagem de release usada pela Action é publicada em:

```text
ghcr.io/rodri-oliveira-dev/adr-guard
```

A imagem pública de release deve aceitar pull anônimo. O CI verifica isso usando uma configuração Docker vazia e removendo tokens do GitHub e dos providers do processo de pull. Se uma organização redirecionar pulls por um mirror privado ou aplicar uma política de registry que exija autenticação, autentique o Docker explicitamente antes de executar o ADR Guard; a Action não faz login implícito nem recebe token como input.

## Isolamento do container

A Action inicia o container com as seguintes restrições:

- filesystem raiz do container somente leitura;
- todas as capabilities Linux removidas com `--cap-drop=ALL`;
- elevação de privilégios bloqueada com `no-new-privileges`;
- rede desabilitada com `--network=none` para `check` e `index` determinísticos; somente `review` com provider usa rede de saída;
- sem modo `--privileged`;
- sem encaminhamento implícito de variáveis de ambiente ou secrets; `review` pode encaminhar apenas a variável de credencial do provider selecionado.

A imagem publicada declara um usuário não-root. O CI verifica os metadados da imagem publicada e rejeita usuário vazio, root ou UID 0.

No `check` e no `review`, todo o workspace do consumidor é montado como somente leitura. No `index`, o workspace continua somente leitura e apenas o diretório de ADR validado é sobreposto com uma montagem gravável. Em Linux, `index` usa UID/GID não-root do host para manter o ownership dos arquivos gerados. Um runner executado como root é rejeitado no `index`, em vez de executar o container como root.

## Inputs e diagnósticos

`check`, `index` e `review` explícito são aceitos. Os paths são resolvidos dentro de `GITHUB_WORKSPACE`, traversal e escapes por symlink são rejeitados, e os inputs são enviados como argumentos separados de processo, sem avaliação como fragmentos de shell. O review aceita somente `push`, `workflow_dispatch`, `schedule` e `pull_request` do mesmo repositório; todos os demais tipos de evento, incluindo `pull_request_target` e pull requests de fork, são rejeitados antes da execução do Docker/provider.

A saída do CLI e do provider é tratada como não confiável. O log bruto é preservado para troubleshooting enquanto a interpretação de workflow commands do GitHub fica suspensa. Diagnósticos determinísticos `ADR001`–`ADR009`, com paths verificados, viram annotations de erro escapadas. Achados de IA viram annotations `warning` apenas quando a evidência pode ser associada a um arquivo local selecionado e verificado; a Action não inventa números de linha. O relatório não altera o exit code original do CLI.

## Secrets e providers de IA

O caminho padrão de `check`/`index` não encaminha credenciais de provider. O `review` opt-in expõe seleção de provider/model, mas deliberadamente não possui input de credencial ou token: o consumidor fornece a credencial do provider pelo ambiente do step da Action, e o wrapper encaminha ao Docker apenas o nome da variável do provider selecionado. `GITHUB_TOKEN` e `GH_TOKEN` nunca são encaminhados. Review em PR de fork e em `pull_request_target` é bloqueado antes da execução do provider.

A criação assistida por IA continua sendo um fluxo separado e explícito do CLI/container e não é exposta pela Action. Consulte [Revisão por IA na GitHub Action](github-action-review.pt-BR.md) para o contrato de workflow confiável.

## Confiança em versão e digest

Use uma tag exata da Action, como `@v1.2.3`, para manter source/runtime imutáveis, ou a tag móvel de compatibilidade `@v1` para receber releases bem-sucedidas mais novas da major 1. O ADR Guard associa `@v1.2.3` à tag `:1.2.3` do container e `@v1` à tag `:1`; nunca há fallback silencioso para `latest`. Ao fixar a Action por SHA de commit, informe também o input `version` exato. Consulte a [política de release da GitHub Action](github-action-release.pt-BR.md) para ordem de publicação e verificação.

Em ambientes que exigem identidade imutável do container, resolva o digest publicado e use o fluxo direto de container documentado em [container.pt-BR.md](container.pt-BR.md):

```text
ghcr.io/rodri-oliveira-dev/adr-guard@sha256:<digest>
```

O CI também verifica que a imagem SemVer publicada resolve para um repository digest `sha256` do GHCR. As imagens de release continuam protegidas pelos controles existentes de SBOM, provenance, verificação de manifests multi-plataforma, Hadolint, smoke tests, atualizações de imagem-base pelo Dependabot e análise do Trivy.

## Pré-requisitos de runtime

A composite Action exige runner Linux com daemon Docker funcional. O `review` opt-in também exige Python 3, porque a renderização segura de `GITHUB_STEP_SUMMARY` e annotations faz parte do contrato de sucesso e é validada antes da execução do provider. O contrato foi projetado para runners Ubuntu hospedados pelo GitHub e runners Linux compatíveis executados como usuário não-root. Windows, macOS, runners sem Docker, runners de review sem Python 3, exigência de containers privilegiados e execução como root para `index` não fazem parte do suporte.
