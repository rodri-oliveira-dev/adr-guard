# Adotar Redis para Cache Distribuído de Produtos

## Status

Proposed

## Context

A API de produtos repete consultas de preços no banco, aumentando a latência p95 e a carga durante os picos do catálogo. Preços podem ficar obsoletos por no máximo 30 segundos. A API deve continuar consultando o banco quando o cache estiver indisponível, e as réplicas precisam compartilhar valores.

## Decision

Adotar Redis gerenciado com leituras cache-aside para preços e TTL de 30 segundos. Usar chaves versionadas, timeouts limitados e fallback para o banco em falhas de cache. Não armazenar dados de autorização ou dados pessoais sensíveis.

## Consequences

Valores compartilhados devem reduzir leituras repetidas e melhorar a latência p95. A equipe passa a cuidar de custos, memória, capacidade, expiração, invalidação, monitoramento e testes de degradação. Falhas e misses ainda exigem consultas eficientes ao banco.
