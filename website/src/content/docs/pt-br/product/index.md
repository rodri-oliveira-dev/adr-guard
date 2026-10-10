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
| [Extensão VS Code](./vscode-extension/) | Colaboradores querem explorer, comandos e diagnósticos | Executa no host do workspace; aceita apenas workspaces locais confiáveis; invoca CLI existente |
| [Contêiner](./containers/) | Você quer runtime isolado e reproduzível | Monte apenas arquivos necessários e passe configuração conscientemente |
| [Agent Skills](../skills/) | Você quer orientação de um agente para decisões e fluxos da CLI | Instruções portáteis; não incluem a CLI nem aprovam arquitetura |

## Fluxos principais

- [Crie ADRs canônicos](./creation/) Minimal, Extended ou com templates customizados restritos.
- [Valide relações](./relationships/), arquivos alterados e [baselines de diagnósticos](./incremental-validation/).
- Produza [relatórios legíveis e estruturados](./reports/).
- Ative [rascunho assistido por IA](./ai-drafting/) ou [revisão](./ai-review/) com provedores explícitos e controles de segurança.
- Valide MADR 4.0 em [modo opcional separado](./madr-4/).

## IA e confirmação humana

Rascunho e revisão enviam contexto selecionado ao provedor configurado. A saída é não confiável, limitada, analisada e apresentada para avaliação humana. Ela não aprova decisões, não edita histórico aceito automaticamente e não substitui revisões de segurança e arquitetura.

## Status do MCP

O repositório não publica atualmente um servidor MCP independente do ADR Guard. Não configure nem dependa de um com base neste portal. As superfícies públicas suportadas são CLI, GitHub Action, contêiner e extensão VS Code documentadas aqui.

Para detalhes de comandos, use a [referência da CLI](./cli/) e preserve a versão realmente instalada ao investigar problemas.
