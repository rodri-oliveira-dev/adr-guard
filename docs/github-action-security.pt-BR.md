# Modelo de segurança da GitHub Action

A GitHub Action reutilizável do ADR Guard é deliberadamente limitada à validação determinística de ADRs e à geração do índice. Ela executa o container publicado no GHCR e não expõe o comando de IA `draft`.

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
- rede desabilitada com `--network=none`;
- sem modo `--privileged`;
- sem encaminhamento implícito de variáveis de ambiente ou secrets.

A imagem publicada declara um usuário não-root. O CI verifica os metadados da imagem publicada e rejeita usuário vazio, root ou UID 0.

No `check`, todo o workspace do consumidor é montado como somente leitura. No `index`, o workspace continua somente leitura e apenas o diretório de ADR validado é sobreposto com uma montagem gravável. Em Linux, `index` usa UID/GID não-root do host para manter o ownership dos arquivos gerados. Um runner executado como root é rejeitado no `index`, em vez de executar o container como root.

## Inputs e diagnósticos

Somente `check` e `index` são aceitos. Os paths são resolvidos dentro de `GITHUB_WORKSPACE`, traversal e escapes por symlink são rejeitados, e os inputs são enviados como argumentos separados de processo, sem avaliação como fragmentos de shell.

A saída do CLI é tratada como não confiável. O log bruto é preservado para troubleshooting enquanto a interpretação de workflow commands do GitHub fica suspensa. Somente diagnósticos reconhecidos `ADR001`–`ADR009`, com paths verificados, viram anotações de arquivo escapadas. O relatório não pode alterar o exit code original do validador.

## Secrets e providers de IA

A Action padrão não expõe `draft`, seleção de provider, endpoints nem inputs de credenciais. Ela não envia `OPENAI_API_KEY`, `ANTHROPIC_API_KEY`, `GEMINI_API_KEY`, `ADR_GUARD_OPENAI_COMPATIBLE_API_KEY`, `GITHUB_TOKEN` ou `GH_TOKEN` ao container.

A criação assistida por IA continua sendo um fluxo separado e explícito do CLI/container. Consumidores que decidirem usar `draft` devem gerenciar as credenciais do provider e revisar separadamente as orientações de privacidade.

## Confiança em versão e digest

Use uma tag exata da Action, como `@v1.2.3`. O ADR Guard associa essa release à tag exata correspondente do container `:1.2.3` e nunca faz fallback silencioso para `latest`. Ao fixar a Action por SHA de commit, informe também o input `version` exato.

Em ambientes que exigem identidade imutável do container, resolva o digest publicado e use o fluxo direto de container documentado em [container.pt-BR.md](container.pt-BR.md):

```text
ghcr.io/rodri-oliveira-dev/adr-guard@sha256:<digest>
```

O CI também verifica que a imagem SemVer publicada resolve para um repository digest `sha256` do GHCR. As imagens de release continuam protegidas pelos controles existentes de SBOM, provenance, verificação de manifests multi-plataforma, Hadolint, smoke tests, atualizações de imagem-base pelo Dependabot e análise do Trivy.

## Pré-requisito Docker

A composite Action exige runner Linux com daemon Docker funcional. O contrato foi projetado para runners Ubuntu hospedados pelo GitHub e runners Linux compatíveis executados como usuário não-root. Windows, macOS, runners sem Docker, exigência de containers privilegiados e execução como root para `index` não fazem parte do suporte.
