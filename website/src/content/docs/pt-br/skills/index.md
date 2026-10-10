---
title: Agent Skills do ADR Guard
description: Conheça as 11 Agent Skills para criar, validar, revisar e manter decisões arquiteturais e apoiar a adoção de ADRs em equipes.
sidebar:
  order: 1
---

**Agent Skills** são instruções reutilizáveis e específicas para tarefas. Elas orientam um agente de programação compatível sobre *quando* e *como* trabalhar com Architecture Decision Records (ADRs). O ADR Guard oferece **11 skills** que cobrem desde a identificação de uma decisão importante até a manutenção de seu histórico.

As skills facilitam a descoberta e o uso consistente do produto. Elas **não** substituem a equipe de arquitetura, não instalam a CLI do ADR Guard e não dão ao agente autoridade para aceitar decisões.

## O que podemos fazer com elas?

| Objetivo | Comece por | Resultado |
| --- | --- | --- |
| Descobrir se a decisão precisa ser documentada | [Quando registrar](./catalog/#adr-guard-when-to-record) | Recomendação fundamentada: ADR, documento simples ou mais evidências |
| Comparar caminhos possíveis | [Análise de trade-offs](./catalog/#adr-guard-tradeoff-analysis) | Alternativas, critérios, consequências e incertezas explícitos |
| Documentar uma nova decisão | [Criar um ADR](./catalog/#adr-guard-create) | Um ADR **Proposed** para revisão humana |
| Melhorar um registro existente | [Validação](./catalog/#adr-guard-validate) e [Revisão técnica](./catalog/#adr-guard-technical-review) | Diagnósticos estruturais e recomendações fundamentadas, separadamente |
| Mudar uma decisão aceita | [Substituição](./catalog/#adr-guard-supersede) | Nova proposta e relação rastreável sem apagar o histórico |
| Cuidar do acervo de ADRs | [Auditoria](./catalog/#adr-guard-audit) e [Adoção](./catalog/#adr-guard-team-adoption) | Avaliação somente leitura e processo leve com responsáveis humanos |

## Como funcionam?

1. Você instala a skill específica no ambiente do agente compatível.
2. O agente consulta o `SKILL.md` quando a tarefa é relevante.
3. Ele verifica o formato dos ADRs, os arquivos existentes e a disponibilidade da CLI.
4. Ele ajuda a estruturar a análise ou, **com sua autorização**, executa comandos existentes do ADR Guard.
5. Você e as pessoas responsáveis avaliam as evidências e tomam a decisão.

Por exemplo: **“Ajude a decidir se nossa mudança para mensageria assíncrona precisa de um ADR; compare as alternativas sem presumir o resultado.”**

O agente pode usar `adr-guard-when-to-record` e `adr-guard-tradeoff-analysis`. Depois, `adr-guard-create` pode documentar a proposta. Um `adr-guard check` bem-sucedido valida a estrutura, mas não o mérito arquitetural.

## Por onde começar

- [Instalação e pré-requisitos](./installation/) — descoberta, instalação e verificação da CLI independente.
- [Catálogo com as 11 skills](./catalog/) — escolha a responsabilidade correta.
- [Exemplos completos](./examples/) — da nova decisão à auditoria e substituição.
- [Segurança e governança](./security-and-governance/) — aprovações, privacidade, fontes não confiáveis, formatos e limites.

A [especificação Agent Skills](https://agentskills.io/specification) define o formato portátil `SKILL.md`. O [Skills CLI](https://github.com/vercel-labs/skills) facilita a instalação em agentes compatíveis.
