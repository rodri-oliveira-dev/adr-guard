---
title: Exemplos de ADR
description: Decisões completas de engenharia com contexto, alternativas, critérios, consequências, riscos e evolução rastreável.
sidebar:
  order: 1
---

Estes exemplos são material de aprendizagem, não recomendações universais. Cada cenário torna problema e raciocínio concretos e mostra como a profundidade deve acompanhar o impacto.

| Cenário | O que ensina | Profundidade sugerida |
| --- | --- | --- |
| [Cache distribuído com Redis](./redis-cache/) | O mesmo raciocínio em Minimal, Extended e MADR 4.0 | Compare os três |
| [Comunicação síncrona ou assíncrona](./service-communication/) | Acoplamento, resiliência, latência, consistência e operação | Extended |
| [Seleção de banco de dados](./database-selection/) | Critérios ponderados e responsabilidade operacional | Extended |
| [Autenticação e autorização](./authentication/) | Requisitos de segurança, riscos e limites de responsabilidade | Extended |
| [Substituição de ADR](./supersession/) | Evoluir uma decisão sem apagar o histórico | Par Minimal com relações explícitas |

Exemplos declarados compatíveis com o ADR Guard também existem como fontes em `docs/examples/` e passam pelos testes de documentação do repositório. O portal adiciona explicação; o Markdown de origem continua autoritativo.
