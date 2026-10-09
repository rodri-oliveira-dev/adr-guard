# Usar PostgreSQL como Sistema de Registro de Pedidos

## Status

Accepted

## Context

O serviço de pedidos armazena cabeçalhos, itens, referências de pagamento e transições de estado. O banco de documentos atual exige compensações quando registros mudam juntos e não fornece a trilha de auditoria exigida antes da próxima alta temporada. A migração está limitada a duas janelas. A plataforma já opera PostgreSQL gerenciado; outra tecnologia exigiria um novo modelo de suporte.

## Decision Drivers

- Preservar consistência atômica no agregado de pedido.
- Aplicar relacionamentos e manter histórico auditável.
- Concluir migração com rollback ensaiado antes do prazo de conformidade.
- Reusar backup, monitoramento, recuperação e plantão existentes.
- Manter API e eventos do serviço como limite de integração.

## Options Considered

1. Manter o banco de documentos e adicionar compensação, reconciliação e log de auditoria separado.
2. Adotar PostgreSQL gerenciado para dados autoritativos de pedidos.
3. Adotar um banco SQL distribuído operado pela equipe do serviço.

## Decision

Adotaremos PostgreSQL gerenciado para dados novos e migrados. Cabeçalhos, itens, referências de pagamento e transições compartilharão schema relacional e limite transacional. Somente o serviço poderá escrever diretamente; consumidores continuarão usando API e eventos.

## Rationale

PostgreSQL atende aos direcionadores de transação e integridade sem introduzir plataforma operacional desconhecida. Manter o banco atual exigiria construir e comprovar compensação e auditoria sob prazo fixo. SQL distribuído atenderia aos dados, mas adicionaria complexidade que a meta regional de disponibilidade não justifica.

## Consequences

A decisão melhora consistência e auditoria, mas exige migração, governança de schema, planejamento de capacidade e responsabilidade operacional explícita.

## Positive Consequences

- Transações e restrições protegem relações dentro de um pedido.
- Procedimentos do serviço gerenciado cobrem backup, monitoramento, correções e recuperação.
- Migrações de schema criam histórico revisável de mudanças estruturais.
- A organização não adiciona outro modelo de suporte de banco.

## Negative Consequences

- Dois bancos precisarão operar durante migração e reconciliação.
- A equipe precisará aprender otimização de consultas e evolução segura de schema.
- Armazenamento, réplicas e migração adicionam custo.
- Outros serviços e analytics continuarão sem acesso direto; precisarão de eventos ou exportações suportadas.

## Risks

- O corte pode exceder a janela. A pessoa responsável pelo serviço ensaiará com volume de produção e definirá limites de rollback.
- Escritas duplas podem divergir. A liderança da migração executará reconciliação e bloqueará o corte diante de diferenças sem explicação.
- Índices ruins podem elevar latência. A equipe testará consultas representativas e observará consultas lentas antes e depois do lançamento.

## References

O plano de implementação, as evidências de desempenho e o runbook serão relacionados no pull request de entrega quando seus caminhos estiverem definidos.
