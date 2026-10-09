---
title: Adoção de Redis para cache distribuído
description: Compare uma decisão coerente de cache nos formatos Minimal, Extended e MADR 4.0.
sidebar:
  order: 2
---

## Cenário

Uma API de produtos repete consultas intensivas de leitura. A latência p95 cresce nos picos do catálogo, enquanto preços podem ficar obsoletos por no máximo 30 segundos. A equipe consegue operar um serviço gerenciado, mas não quer que o cache seja pré-requisito para leituras corretas.

## Alternativas e critérios

| Opção | Latência | Controle de consistência | Reuso entre instâncias | Custo operacional |
| --- | --- | --- | --- | --- |
| Sem cache | Fraca no pico | Forte | N/A | Baixo |
| Cache em processo | Boa por instância | Mais difícil de coordenar | Não | Baixo–médio |
| Redis gerenciado | Boa | TTL e invalidação explícitos | Sim | Médio |

## Decisão

Adotar Redis gerenciado com leituras cache-aside, TTL de 30 segundos para preços, timeouts limitados e fallback para o banco. Chaves serão versionadas. Taxa de acerto, latência, erros e indicadores de obsolescência serão observados antes da expansão.

## Consequências

**Positivas:** leituras repetidas deixam o banco, a latência responde melhor aos picos e réplicas compartilham valores aquecidos.

**Negativas:** a equipe assume invalidação, custo, capacidade, conexões e testes de degradação. Cache não corrige consultas lentas ou incorretas na origem.

## Riscos e limites

Efeito manada, chaves quentes, incompatibilidade de serialização e cache acidental de dados sensíveis exigem controles. A tolerância de 30 segundos é específica do domínio e não deve ser copiada para estoque ou autorização.

## Três representações completas

- [Fonte Minimal canônica](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/docs/examples/redis-cache/minimal/pt-BR/0001-usar-redis-cache.md) — suficiente para piloto delimitado.
- [Fonte Extended canônica](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/docs/examples/redis-cache/extended/pt-BR/0001-usar-redis-cache.md) — melhor quando revisores precisam de motivadores e opções explícitos.
- [Fonte MADR 4.0](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/docs/examples/redis-cache/madr-4/pt-BR/0001-usar-redis-cache.md) — adequada quando o repositório padroniza MADR e valida separadamente com `--adr-format madr-4`.

Use a [comparação interativa](/adr-guard/pt-br/templates/#compare-a-mesma-decisao) para alternar entre as estruturas.
