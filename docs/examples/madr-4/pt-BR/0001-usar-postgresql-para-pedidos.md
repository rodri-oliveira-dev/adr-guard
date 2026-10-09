---
status: "accepted"
decision-makers: Responsáveis pelo serviço de pedidos e pela plataforma
consulted: Equipes de segurança, governança de dados e suporte
informed: Equipes de produto e analytics
---

# Usar PostgreSQL como Sistema de Registro de Pedidos

## Context and Problem Statement

O serviço precisa atualizar cabeçalhos, itens, referências de pagamento e transições de estado de forma atômica e fornecer histórico auditável antes da próxima alta temporada. O banco de documentos depende de compensações e não atende sem novos mecanismos. A migração tem duas janelas, e a plataforma já opera PostgreSQL gerenciado.

## Decision Drivers

- Consistência atômica e relacionamentos aplicáveis
- Alterações de dados auditáveis
- Migração e rollback ensaiados antes do prazo
- Capacidade existente de backup, monitoramento, recuperação e plantão
- API e eventos permanecem o limite de integração

## Considered Options

- Manter o banco de documentos e adicionar compensação, reconciliação e auditoria separada.
- Usar PostgreSQL gerenciado para dados autoritativos de pedidos.
- Usar um banco SQL distribuído operado pela equipe do serviço.

## Decision Outcome

Opção escolhida: usar PostgreSQL gerenciado, pois atende às transações e auditoria reutilizando um modelo operacional estabelecido. Os dados compartilharão schema relacional e limite transacional. Somente o serviço poderá escrever diretamente.

### Consequences

- Positivo, pois transações e restrições protegem a consistência dos pedidos.
- Positivo, pois procedimentos existentes cobrem backup, correções, monitoramento e recuperação.
- Negativo, pois a migração exige operação dupla, reconciliação e rollback ensaiado.
- Negativo, pois a equipe assume evolução de schema, desempenho, custos e plantão.

### Confirmation

Antes do corte, o ensaio com volume de produção deve caber na janela, a reconciliação não pode ter diferenças sem explicação e os testes de transação e recuperação precisam passar. A pessoa responsável revisará essas verificações após o lançamento.

## Pros and Cons of the Options

### Manter o banco de documentos

- Positivo, pois evita uma migração imediata.
- Negativo, pois a equipe precisa construir e comprovar compensação e auditoria sob prazo fixo.

### PostgreSQL gerenciado

- Positivo, pois atende consistência e auditoria com suporte existente da plataforma.
- Negativo, pois introduz migração e responsabilidade relacional para a equipe.

### SQL distribuído

- Positivo, pois oferece semântica relacional e opções adicionais de distribuição.
- Negativo, pois o novo modelo operacional e sua complexidade não se justificam pela meta regional atual.

## More Information

Planos de implementação, evidências de desempenho e o runbook serão anexados ao trabalho de entrega quando finalizados.
