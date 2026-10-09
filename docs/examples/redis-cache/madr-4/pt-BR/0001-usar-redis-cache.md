---
status: "proposed"
decision-makers: Responsáveis pela API de produtos e pela plataforma
consulted: Equipes de segurança e governança de dados
informed: Equipes de produto e suporte
---

# Adotar Redis para Cache Distribuído de Produtos

## Context and Problem Statement

A API de produtos repete consultas de preços no banco, aumentando a latência p95 e a carga durante os picos do catálogo. Preços podem ficar obsoletos por no máximo 30 segundos. A API deve continuar consultando o banco quando o cache estiver indisponível, e as réplicas precisam compartilhar valores.

## Decision Drivers

- Reduzir latência p95 e consultas nos picos do catálogo
- Limitar obsolescência de preços a 30 segundos
- Compartilhar entradas entre réplicas da API
- Preservar o fallback correto se o cache falhar
- Controlar custos e exposição de dados sensíveis

## Considered Options

- Sem cache: consultar diretamente o banco
- Cache em processo por réplica
- Redis gerenciado compartilhado entre réplicas

## Decision Outcome

Opção escolhida: Adotar Redis gerenciado com leituras cache-aside para preços e TTL de 30 segundos. Usar chaves versionadas, timeouts limitados e fallback para o banco em falhas de cache. Não armazenar dados de autorização ou dados pessoais sensíveis. Consultas diretas são simples, mas mantêm a amplificação nos picos. Cache em processo ajuda cada réplica, mas dificulta a consistência entre elas. Redis permite reuso e janela explícita de obsolescência; cache-aside preserva o banco como fonte autoritativa.

### Consequences

- Positivo, pois entradas aquecidas reduzem consultas ao banco e latência.
- Positivo, pois valores compartilhados aumentam o reuso entre réplicas.
- Negativo, pois redis adiciona custo de infraestrutura e novos modos de falha.
- Negativo, pois invalidação, serialização, capacidade e fallback exigem testes contínuos.

### Confirmation

Antes de ampliar o rollout, testes de carga devem confirmar melhora da latência p95 e taxa de acerto, métricas de frescor devem respeitar 30 segundos e testes de falha devem comprovar o fallback. A equipe da API revisará os resultados.

## Pros and Cons of the Options

### Sem cache

- Positivo, pois preserva a simplicidade operacional.
- Negativo, pois mantém a pressão sobre o banco nos picos.

### Cache em processo

- Positivo, pois acelera leituras locais.
- Negativo, pois os valores divergem entre réplicas.

### Redis gerenciado

- Positivo, pois compartilha valores com TTL explícito.
- Negativo, pois adiciona custo operacional e risco de efeito manada.

## More Information

O benchmark, plano de rollout e runbook de degradação serão vinculados ao PR de entrega.
