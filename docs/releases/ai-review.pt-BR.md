# Disponibilidade de releases do review por IA

Esta nota registra a fronteira real de versões publicadas da revisão técnica de ADR assistida por IA sem reescrever retroativamente notas de releases anteriores.

| Capacidade | Primeira versão publicada | Observações |
| --- | --- | --- |
| `adr-guard review`, análise em oito dimensões, contexto explícito/limitado, evidência cross-ADR e relatórios text/JSON schema `1.0` | `v1.1.2` | CLI/.NET Tool e containers da release |
| Policy determinística de review schema `1.0`, advisory por padrão, `enforce` explícito e exit code `4` | `v1.1.3` | Findings do modelo continuam advisory |
| Hardening de segurança/trust boundary e matriz de regressão determinística com mock provider | `v1.1.4` | Inclui validação estrita de saída e controles de segurança do review |
| Release publicada atual | `v1.1.5` | Inclui todas as capacidades de CLI review acima |
| `command: review` na GitHub Action reutilizável | Ainda não publicado no momento desta nota | Implementado e validado no CI do PR #85; passa a fazer parte da Action móvel `@v1` somente após a release pós-merge |

Portanto, a Action `@v1` já publicada deve ser tratada como `check`/`index` apenas até a publicação da release que contém o PR #85. Não copie um workflow pré-release com `command: review` supondo que o `@v1` remoto atual já o suporte.

A listagem no Marketplace é um marco de publicação separado e não é apresentada aqui como disponível.

Consulte o [guia de review por IA](../adr-review.pt-BR.md) e o [review por IA na GitHub Action](../github-action-review.pt-BR.md).
