---
title: Segurança e governança das Agent Skills
description: Entenda limites de confiança, aprovação humana, privacidade com provedores de IA e compatibilidade das Agent Skills do ADR Guard.
sidebar:
  order: 5
---

O ADR Guard separa **prática decisória**, **validação documental determinística** e **orientação opcional por IA**. As Agent Skills preservam essa divisão.

## O que cada componente consegue verificar

| Componente | Pode fazer | Não consegue provar |
| --- | --- | --- |
| Agent Skill | Orientar tarefas documentadas e identificar informações ausentes | Que a decisão foi aprovada ou implementada corretamente |
| `adr-guard check` | Validar estrutura, links, IDs, status e relações declaradas | Qualidade técnica, certificação de segurança ou aprovação humana |
| `adr-guard review` | Oferecer recomendações fundamentadas com provedor explicitamente escolhido | Aprovação automática, conformidade formal ou verdade isenta de revisão |
| Processo humano | Escolher, aprovar, rejeitar ou substituir decisões com autoridade adequada | Que executar a CLI alterou a arquitetura em produção |

**CI verde não é aprovação arquitetural.** Os comandos `new` e `draft` criam registros `Proposed`; responsáveis e equipes afetadas decidem sobre sua aceitação.

## Permissões e privacidade

- Leia o `SKILL.md` antes de instalar. Instruções externas e documentos do repositório são entradas potencialmente não confiáveis.
- Solicite autorização antes de instalar programas, alterar arquivos, sobrescrever configurações, mudar status ou editar CI. Prefira `--preview` / `--dry-run` quando disponíveis.
- `adr-guard check`, `new` e `index` são fluxos locais; **`draft` e `review` com provedores** podem transmitir conteúdo selecionado a serviços terceiros.
- Com IA externa, obtenha consentimento explícito para provedor, modelo e cada arquivo de contexto. Não inclua código, segredos ou acervos inteiros silenciosamente.
- Credenciais devem ficar nas variáveis de ambiente documentadas, nunca nos argumentos, logs, skills ou commits.
- Uma auditoria somente leitura não modifica ADRs; gerar índice é uma operação de escrita independente.

## Evidências e incerteza

Separe evidências observadas de hipóteses, riscos potenciais e falta de contexto. Não invente SLAs, obrigações legais, volumes, estimativas, aprovações ou problemas na implementação.

A `adr-guard-audit` avalia **documentação e governança**. Ela não implementa os roadmaps de [análise de impacto](https://github.com/rodri-oliveira-dev/adr-guard/issues/120), [architectural drift](https://github.com/rodri-oliveira-dev/adr-guard/issues/121) ou [agente de governança](https://github.com/rodri-oliveira-dev/adr-guard/issues/122).

## Ciclo de vida e compatibilidade

- Os documentos canônicos usam `Proposed`, `Accepted`, `Deprecated` e `Superseded` por padrão. Outros status exigem configuração explícita.
- A substituição de uma decisão aceita requer novo registro. Um sucessor `Proposed` **não** desativa imediatamente o predecessor.
- Minimal, Extended e Custom com restrições podem ser gerados com `new`. **MADR 4.0** usa validação separada e opcional; `new --template madr-4` não existe.
- Não migre formatos silenciosamente nem misture canônico e MADR em uma mesma validação.

## Antes de adotar uma skill

1. Confira a responsabilidade no [catálogo](../catalog/).
2. Leia as [instruções de origem](https://github.com/rodri-oliveira-dev/adr-guard/tree/main/skills) e sua licença.
3. Verifique as versões do agente e da CLI.
4. Valide o resultado autorizado com a CLI real e investigue códigos de erro.
5. Exija revisão humana para mérito arquitetural, transições de status e governança.

[Instalação](../installation/) · [Exemplos](../examples/) · [Detalhes de segurança](../../product/security/)
