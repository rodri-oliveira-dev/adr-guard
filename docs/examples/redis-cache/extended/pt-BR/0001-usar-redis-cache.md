# Adotar Redis para Cache Distribuído de Produtos

## Status

Proposed

## Context

A API de produtos repete consultas de preços no banco, aumentando a latência p95 e a carga durante os picos do catálogo. Preços podem ficar obsoletos por no máximo 30 segundos. A API deve continuar consultando o banco quando o cache estiver indisponível, e as réplicas precisam compartilhar valores.

## Decision Drivers

- Reduzir latência p95 e consultas nos picos do catálogo
- Limitar obsolescência de preços a 30 segundos
- Compartilhar entradas entre réplicas da API
- Preservar o fallback correto se o cache falhar
- Controlar custos e exposição de dados sensíveis

## Options Considered

1. Sem cache: consultar diretamente o banco
2. Cache em processo por réplica
3. Redis gerenciado compartilhado entre réplicas

## Decision

Adotar Redis gerenciado com leituras cache-aside para preços e TTL de 30 segundos. Usar chaves versionadas, timeouts limitados e fallback para o banco em falhas de cache. Não armazenar dados de autorização ou dados pessoais sensíveis.

## Rationale

Consultas diretas são simples, mas mantêm a amplificação nos picos. Cache em processo ajuda cada réplica, mas dificulta a consistência entre elas. Redis permite reuso e janela explícita de obsolescência; cache-aside preserva o banco como fonte autoritativa.

## Consequences

Valores compartilhados devem reduzir leituras repetidas e melhorar a latência p95. A equipe passa a cuidar de custos, memória, capacidade, expiração, invalidação, monitoramento e testes de degradação. Falhas e misses ainda exigem consultas eficientes ao banco.

## Positive Consequences

- Entradas aquecidas reduzem consultas ao banco e latência.
- Valores compartilhados aumentam o reuso entre réplicas.

## Negative Consequences

- Redis adiciona custo de infraestrutura e novos modos de falha.
- Invalidação, serialização, capacidade e fallback exigem testes contínuos.

## Risks

- Chaves quentes e efeito manada exigem controle de concorrência e jitter de TTL.
- Falhas do Redis podem ampliar carga de origem; testar timeouts e backpressure.
- TTL de preços não é automaticamente seguro para estoque ou autorização.

## References

O benchmark, plano de rollout e runbook de degradação serão vinculados ao PR de entrega.
