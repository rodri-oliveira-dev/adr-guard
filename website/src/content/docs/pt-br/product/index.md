---
title: Documentação do produto
description: Escolha a interface e o fluxo de validação do ADR Guard adequado ao seu ambiente.
sidebar:
  order: 1
---

O ADR Guard mantém o julgamento arquitetural com pessoas e automatiza verificações documentais repetíveis. Comece pela CLI e adicione apenas as integrações necessárias.

## Interfaces

| Interface | Quando usar | Limite de confiança e execução |
| --- | --- | --- |
| [CLI](./cli/) | Você precisa da superfície local completa | Executa localmente com argumentos e configuração explícitos |
| [GitHub Action](./github-action/) | Pull requests devem validar ou revisar mudanças | Executa no GitHub Actions com permissões e entradas definidas |
| [Extensão VS Code](./vscode-extension/) · [Guia visual](./vscode-visual-guide/) | Colaboradores querem criação/edição visual, explorer, comandos e diagnósticos | Executa no host do workspace; aceita apenas workspaces locais confiáveis; invoca uma CLI compatível existente |
| [Contêiner](./containers/) | Você quer runtime isolado e reproduzível | Monte apenas arquivos necessários e passe configuração conscientemente |
| [Agent Skills](../skills/) | Você quer orientação de um agente para decisões e fluxos da CLI | Instruções portáteis; não incluem a CLI nem aprovam arquitetura |

## Fluxos principais

- [Crie ADRs canônicos](./creation/) Minimal, Extended ou com templates customizados restritos. Formulários compatíveis do VS Code usam os contratos estruturados de criação e edição da CLI 1.7.0 quando esses artefatos, versionados separadamente, forem publicados.
- [Valide relações](./relationships/), arquivos alterados e [baselines de diagnósticos](./incremental-validation/).
- Use a [Análise de Impacto Arquitetural](./architecture-impact/) para identificar decisões associadas a arquivos alterados por mapeamentos ADR-código explícitos. O resultado convida à revisão; não representa uma violação arquitetural.
- Use o [Drift Arquitetural](./architectural-drift/) para verificar regras de fitness explícitas e ligadas a ADRs com evidências limitadas. Os resultados diferenciam conformidade, violação e evidência insuficiente; o enforcement opcional atua somente dentro dos limites determinísticos implementados.
- Produza [relatórios legíveis e estruturados](./reports/).
- Ative [rascunho assistido por IA](./ai-drafting/) ou [revisão](./ai-review/) com provedores explícitos e controles de segurança.
- Valide MADR 4.0 em [modo opcional separado](./madr-4/).

## Impacto e drift são diferentes

| Funcionalidade | Pergunta respondida | O que ela não afirma |
| --- | --- | --- |
| [Análise de Impacto Arquitetural](./architecture-impact/) | Quais ADRs podem precisar de revisão porque arquivos mapeados mudaram? | Não interpreta a mudança como não conformidade nem realiza análise semântica irrestrita do código. |
| [Drift Arquitetural](./architectural-drift/) | A evidência disponível satisfaz uma regra de fitness explícita e ligada a um ADR? | Não infere regras ausentes, não transforma falta de evidência em sucesso e não aprova a decisão associada. |

Os dois fluxos mantêm evidências rastreáveis e o julgamento arquitetural sob responsabilidade humana. A validação documental verifica se os ADRs seguem os contratos declarados; a avaliação técnica cobre somente regras e escopos configurados explicitamente; pessoas ainda decidem se o ADR é aceito e se a regra representa a arquitetura pretendida.

## IA e confirmação humana

Rascunho e revisão enviam contexto selecionado ao provedor configurado. A saída é não confiável, limitada, analisada e apresentada para avaliação humana. Ela não aprova decisões, não edita histórico aceito automaticamente e não substitui revisões de segurança e arquitetura.

## Status do MCP

O repositório não publica atualmente um servidor MCP independente do ADR Guard. Não configure nem dependa de um com base neste portal. As superfícies públicas suportadas são CLI, GitHub Action, contêiner e extensão VS Code documentadas aqui.

Para detalhes de comandos, use a [referência da CLI](./cli/) e preserve a versão realmente instalada ao investigar problemas.
