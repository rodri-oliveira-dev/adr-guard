# O que é um Registro de Decisão de Arquitetura?

[English](what-is-an-adr.md) · [Início da documentação](../index.pt-BR.md) · [Próximo: Por que usar ADRs?](why-use-adrs.pt-BR.md)

Uma **decisão de arquitetura** é uma escolha que afeta de forma relevante a estrutura, os atributos de qualidade, as dependências, as interfaces, a operação ou a forma de construir um sistema. Uma **Architecture Decision Record (ADR)**, ou Registro de Decisão de Arquitetura, é um documento curto e durável que explica uma dessas escolhas.

Uma ADR responde a quatro perguntas:

1. **Contexto:** qual problema e quais restrições exigiram uma decisão?
2. **Decisão:** o que a equipe fará?
3. **Consequências:** quais benefícios, custos, riscos e trabalhos posteriores resultarão?
4. **Status:** a escolha está proposta, aceita, descontinuada ou substituída?

## O problema que ADRs resolvem

O código mostra o que um sistema faz, mas raramente preserva por que uma equipe escolheu um desenho em vez de outro. Issues e conversas podem conter partes dessa justificativa, porém costumam ficar dispersas, inacessíveis a novos integrantes ou separadas do código que influenciaram.

Sem um registro durável, futuros mantenedores podem repetir uma investigação, manter uma escolha obsoleta porque desconhecem suas restrições ou revertê-la sem enxergar os trade-offs que ela protegia. Uma ADR mantém a justificativa próxima do trabalho e torna visíveis decisões históricas.

## Um pequeno exemplo

Imagine que um serviço de pedidos precise de armazenamento relacional durável. “Usar PostgreSQL” registra apenas um resultado. Uma ADR útil explica que os pedidos exigem transações e auditabilidade, identifica alternativas como o banco de documentos atual, escolhe PostgreSQL por esses requisitos e registra consequências como esforço de migração e responsabilidade operacional.

Uma ADR não é:

- um desenho completo do sistema nem substituta para diagramas e runbooks;
- uma ata de reunião, plano de projeto ou lista de toda escolha de implementação;
- prova de que uma arquitetura está correta;
- uma aprovação produzida por uma ferramenta de validação ou IA.

O ADR Guard verifica regras estruturais como nomes de arquivo, seções obrigatórias, status, links e integridade de relacionamentos. Ele pode auxiliar escrita e revisão, mas a aprovação arquitetural continua sendo responsabilidade humana.

Em seguida, entenda [como ADRs ajudam equipes e quais são seus limites](why-use-adrs.pt-BR.md).
