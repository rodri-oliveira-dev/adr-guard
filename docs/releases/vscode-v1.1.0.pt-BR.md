# ADR Guard para VS Code 1.1.0 — Publicada

[English](vscode-v1.1.0.md)

A versão **ADR Guard para VS Code 1.1.0**, versionada de forma independente, está publicada no [Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=rodrioliveira.adr-guard). A disponibilidade foi verificada novamente em 20/10/2026. Esse estado no Marketplace independe das releases da CLI e do MCP e não significa que o workflow VSIX do repositório a publicou nem que exista tag Git ou GitHub Release.

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
- CLI compatível instalada separadamente. CLI `1.7.0+` fornece criação/edição visual no schema `1.0`; a CLI pública `1.8.0` suporta os fluxos documentados de arquitetura, governança e IA.
- CLI `1.9.0` continua sendo uma candidata não publicada que adiciona localizações precisas de diagnóstico; a extensão usa fallback seguro com CLIs anteriores.
- MCP é opcional. O MCP `1.0.0` publicado é compatível; MCP `1.0.1` é uma candidata independente não publicada, com os contratos das ferramentas no schema `1.0` inalterados.

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

Confirme que o VS Code informa a versão `1.1.0`. Instale separadamente a CLI e o servidor MCP opcional no ambiente do host do workspace.

## Caminho do VSIX validado

O [workflow VS Code VSIX Package](https://github.com/rodri-oliveira-dev/adr-guard/actions/workflows/vscode-extension-release.yml) manual continua validando e gerando `adr-guard-1.1.0.vsix`, com retenção do artifact `adr-guard-vscode-1.1.0` por 21 dias (sujeita aos limites do repositório). Ele **não** contém credenciais do Marketplace nem executa `vsce publish`; um artifact de workflow bem-sucedido não é evidência de publicação.

## Checklist de acompanhamento da publicação

- Marketplace `rodrioliveira.adr-guard` informa `1.1.0`: verificado em 20/10/2026.
- Mantenha `extensions/vscode/package.json` em `1.1.0`; não incremente a versão como parte da preparação das releases da CLI/MCP.
- Mantenha os workflows de CI/empacotamento VSIX somente como artifacts e documente separadamente qualquer caminho futuro de publicação no Marketplace.
- Não deduza tag Git, GitHub Release ou disponibilidade da CLI/MCP a partir da listagem do Marketplace; verifique cada canal de forma independente.

Consulte o [guia da extensão](../../extensions/vscode/README.pt-BR.md), a [matriz de compatibilidade](../compatibility.pt-BR.md) e o [guia de empacotamento VSIX](../../extensions/vscode/PUBLISHING.md).
