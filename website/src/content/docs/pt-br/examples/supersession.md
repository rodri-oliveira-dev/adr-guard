---
title: Substituição de uma decisão arquitetural
description: Substitua uma regra de banco por serviço preservando justificativa e rastreabilidade.
sidebar:
  order: 6
---

## Decisão original — ADR-0012

**Contexto:** Equipes independentes precisavam de implantação autônoma e propriedade clara dos dados.

**Decisão:** Cada serviço possui banco PostgreSQL separado e expõe dados somente por APIs ou eventos.

**Status:** Accepted.

Isso funcionou para domínios centrais, mas impôs custo desproporcional a serviços internos pequenos: backups, migrações, credenciais, observabilidade e capacidade ociosa separados.

## Nova evidência

Após doze meses, três serviços de suporte de baixo volume compartilham equipe, cadência, fronteira de segurança e objetivo de recuperação. Incidentes mostram mais risco operacional na manutenção separada do que na co-localização lógica. Serviços centrais ainda precisam de isolamento.

## Decisão substituta — ADR-0048

Permitir que serviços internos aprovados e de baixo risco compartilhem cluster PostgreSQL gerenciado, mantendo bancos, credenciais, migrações e verificação de backup separados. Serviços centrais permanecem isolados. Uma revisão verifica carga, conformidade, raio de impacto e plano de saída.

**Status do ADR-0048:** Accepted.<br />
**Status do ADR-0012:** Superseded by ADR-0048.

## Consequências

Sobrecarga e custo ocioso caem para serviços qualificados. A plataforma precisa de quotas, monitoramento de noisy neighbors, isolamento de credenciais e caminho de volta à infraestrutura dedicada. Não significa “compartilhado por padrão”.

## Regras de rastreabilidade

1. Não reescreva ADR-0012 como se a exceção sempre existisse.
2. Adicione relações explícitas `Superseded` e `Superseded by` suportadas pelo contrato canônico.
3. Relacione ADR-0048 ao ADR-0012 e explique a evidência alterada.
4. Preserve ambos no versionamento e no índice gerado.

## Quando este modelo é adequado

Um par curto funciona quando o contexto original já está completo e o novo ADR foca evidência e escopo alterados. Use Extended quando a substituição introduzir várias alternativas ou risco amplo.
