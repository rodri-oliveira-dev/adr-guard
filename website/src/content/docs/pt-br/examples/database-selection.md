---
title: Seleção de tecnologia de banco de dados
description: Escolha persistência para um livro-razão de cobrança com critérios objetivos e consequências operacionais explícitas.
sidebar:
  order: 4
---

## Contexto e problema

Um serviço de cobrança precisa de escritas transacionais, lançamentos financeiros imutáveis, isolamento entre tenants, consultas de reconciliação e retenção de cinco anos. A equipe já opera PostgreSQL; um banco de documentos e um serviço chave-valor também estão disponíveis.

## Critérios objetivos

| Critério | Peso | PostgreSQL | Banco de documentos | Chave-valor |
| --- | ---: | --- | --- | --- |
| Transações entre linhas | Alto | Nativas | Disponíveis com ressalvas | Depende do modelo |
| Consultas de reconciliação | Alto | SQL forte | Agregações específicas | Exige pipelines secundários |
| Familiaridade operacional | Médio | Alta | Média | Média |
| Escala horizontal de escrita | Médio | Exige planejamento | Forte | Forte |
| Evolução de esquema | Médio | Migrações explícitas | Documentos flexíveis | Responsabilidade da aplicação |

## Decisão

Usar PostgreSQL gerenciado com tabelas append-only, controles de acesso por tenant, migrações testadas, criptografia, recuperação point-in-time e revisão de particionamento conforme o volume crescer.

## Consequências

Transações e reconciliação ficam em um sistema conhecido. A equipe aceita disciplina de migrações, capacidade, pool de conexões, verificação de backups e possível particionamento. Payloads flexíveis aparecem apenas onde integridade e consultas permitirem.

## Riscos e limites

O isolamento depende de testes na aplicação e políticas no banco. Retenção longa pode encarecer índices e backups. A escolha deve ser revista se volume medido ou requisitos regionais excederem o limite do desenho.

## Quando Extended é adequado

Tecnologia de dados é cara de reverter e afeta segurança, operação, custo e analytics. A tabela impede que “preferimos esta ferramenta” finja ser justificativa.
