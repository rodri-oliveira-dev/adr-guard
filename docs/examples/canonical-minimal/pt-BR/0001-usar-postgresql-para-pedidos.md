# Usar PostgreSQL como Sistema de Registro de Pedidos

## Status

Accepted

## Context

O serviço de pedidos armazena cabeçalhos, itens, referências de pagamento e transições de estado. O banco de documentos atual exige atualizações compensatórias quando vários registros mudam juntos, e o suporte não consegue reconstruir com confiança quem alterou um pedido e quando. Novos requisitos de auditoria entram em vigor antes da próxima alta temporada. A equipe pode migrar em duas janelas programadas, e a plataforma já opera PostgreSQL gerenciado para outros serviços.

Consideramos manter o banco atual com lógica adicional de compensação e auditoria ou mover os dados autoritativos para um banco relacional. A decisão prioriza atualizações atômicas, relacionamentos aplicáveis, auditabilidade e um modelo operacional já suportado pela organização.

## Decision

Usaremos PostgreSQL gerenciado como sistema de registro dos dados novos e migrados de pedidos. Cabeçalhos, itens, referências de pagamento e transições compartilharão um schema relacional e um limite transacional. O serviço continuará como único escritor; outros sistemas usarão sua API e eventos em vez de acesso direto ao banco.

## Consequences

Transações atômicas e restrições relacionais protegerão a consistência, e a capacidade existente de plataforma fornecerá backup, correções, monitoramento e recuperação. O schema e seu histórico tornarão alterações mais auditáveis.

A equipe precisará projetar e ensaiar a migração, executar reconciliação temporária e manter critérios de rollback. Desenvolvedores precisarão administrar evolução do schema e desempenho de consultas. PostgreSQL se torna dependência crítica com custo e plantão, e consumidores não poderão usar o banco como superfície compartilhada de integração.
