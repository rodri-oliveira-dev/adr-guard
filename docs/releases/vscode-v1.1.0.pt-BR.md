# ADR Guard para VS Code 1.1.0 — Publicada

[English](vscode-v1.1.0.md)

A versão **ADR Guard para VS Code 1.1.0**, versionada de forma independente, está publicada no [Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=rodrioliveira.adr-guard). A disponibilidade foi verificada novamente em 03/11/2026. Esse estado no Marketplace independe das releases da CLI e do MCP e não significa que o workflow VSIX do repositório a publicou nem que exista tag Git ou GitHub Release.

## Principais mudanças

- Consulta, criação, edição, configuração e validação visual de ADRs, impacto e drift arquitetural, baselines de diagnóstico, catálogos enriquecidos e draft/review opcionais com IA.
- Provider opcional de registro MCP, desabilitado por padrão, para um servidor compatível instalado separadamente no host da extensão de workspace.
- Interface bilíngue, limites explícitos de execução e confirmação, exigência de workspace confiável, cancelamento, proteção contra respostas obsoletas e adapters JSON versionados estritos.
- Diagnósticos no Problems navegam para linha e coluna opcionais fornecidas pela CLI e preservam o fallback para o início do documento em CLIs anteriores ou achados no nível de arquivo.

## Correções e melhorias internas

- Preserva operandos relativos ao repositório esperados pela CLI nos fluxos de impacto e drift.
- Evita exigir o diretório de ADRs em fluxos independentes de análise/review.
- Mantém decisões de parsing, validação, política, impacto e drift na CLI/Core .NET; a extensão continua sendo um adaptador do host de workspace.
- Empacota uma entrada Node em bundle e exclui do VSIX arquivos de desenvolvimento, source maps, fixtures, credenciais e binários .NET.

## Compatibilidade e requisitos

- VS Code `1.101.0+`, workspace local `file:` confiável e host Node de extensão de workspace.
- CLI compatível instalada separadamente. CLI `1.7.0+` fornece criação/edição visual no schema `1.0`; a CLI pública `1.9.0` suporta os fluxos documentados de arquitetura, governança e IA e adiciona localizações precisas de diagnóstico. A extensão usa fallback seguro com CLIs compatíveis anteriores.
- MCP é opcional. O MCP `1.0.0` permanece compatível; `1.0.1` foi publicado independentemente em 05/11/2026, preservando os contratos no schema `1.0`.

## Mudanças de comportamento

As novas superfícies de arquitetura, governança, IA e MCP executam somente após ações explícitas da pessoa usuária. Habilitar MCP registra definições, mas não inicia processo durante a ativação da extensão. IA continua opcional e consultiva; ela não pode aprovar ADRs nem estabelecer conformidade.

## Limitações conhecidas

- A extensão não inclui, baixa nem atualiza a CLI ou o servidor MCP.
- Workspaces virtuais e não confiáveis não são suportados. Workspaces remotos exigem executáveis e caminhos no host remoto da extensão.
- As capturas existentes cobrem a interface visual compartilhada, mas não servem como evidência de todas as telas de arquitetura, IA ou MCP da versão 1.1.0.

## Instalar ou atualizar

Instale a extensão publicada pela aba Extensões, pesquisando **ADR Guard**, ou execute:

```shell
code --install-extension rodrioliveira.adr-guard
```

Esta página registra a release histórica `1.1.0`; o comando acima instala a versão atualmente oferecida pelo Marketplace (`1.1.1`, verificada em 19/11/2026). Instale a CLI e o MCP opcional separadamente no host do workspace.

## Caminho do VSIX validado

Na época da versão 1.1.0, o [workflow VS Code VSIX Package](https://github.com/rodri-oliveira-dev/adr-guard/actions/workflows/vscode-extension-release.yml) validava e gerava `adr-guard-1.1.0.vsix`, retido por 21 dias (sujeito aos limites do repositório). O workflow permanece restrito a artifacts e não publica no Marketplace.

## Histórico da publicação 1.1.0

- Marketplace `rodrioliveira.adr-guard` informa `1.1.0`: verificado em 03/11/2026.
- O manifesto da extensão foi atualizado independentemente para `1.1.1`; versões da CLI/MCP não determinam seu versionamento.
- Mantenha os workflows de CI/empacotamento VSIX somente como artifacts e documente separadamente qualquer caminho futuro de publicação no Marketplace.
- Não deduza tag Git, GitHub Release ou disponibilidade da CLI/MCP a partir da listagem do Marketplace; verifique cada canal de forma independente.

Consulte o [guia da extensão](../../extensions/vscode/README.pt-BR.md), a [matriz de compatibilidade](../compatibility.pt-BR.md) e o [guia de empacotamento VSIX](../../extensions/vscode/PUBLISHING.md).
