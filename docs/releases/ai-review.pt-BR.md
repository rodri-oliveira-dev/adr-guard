# Disponibilidade de releases do review por IA

Esta nota registra a fronteira real de versões publicadas da revisão técnica de ADR assistida por IA sem reescrever retroativamente notas de releases anteriores.

| Capacidade | Primeira versão publicada | Observações |
| --- | --- | --- |
| `adr-guard review`, análise em oito dimensões, contexto explícito/limitado, evidência cross-ADR e relatórios text/JSON schema `1.0` | `v1.1.2` | CLI/.NET Tool e containers da release |
| Policy determinística de review schema `1.0`, advisory por padrão, `enforce` explícito e exit code `4` | `v1.1.3` | Findings do modelo continuam advisory |
| Hardening de segurança/trust boundary e matriz de regressão determinística com mock provider | `v1.1.4` | Inclui validação estrita de saída e controles de segurança do review |
| Release publicada atual | `v1.1.6` | Inclui todas as capacidades de CLI review acima |
| `command: review` na GitHub Action reutilizável | `v1.1.6` | Publicado na Action móvel `@v1` como comando opt-in explícito |

A Action `@v1` publicada suporta `check`, `index` e `review` opt-in. O review com provider continua sujeito às regras de trust boundary e credenciais documentadas no guia de review da Action.

A listagem no Marketplace é um marco de publicação separado e não é apresentada aqui como disponível.

Consulte o [guia de review por IA](../adr-review.pt-BR.md) e o [review por IA na GitHub Action](../github-action-review.pt-BR.md).
