# ADR Guard para VS Code 1.1.1 — candidata a release

[English](vscode-v1.1.1.md)

ADR Guard para VS Code `1.1.1` está preparada, mas **não foi publicada**. A versão pública atual no Marketplace continua sendo `1.1.0`, verificada pela Gallery API do Marketplace em 05/11/2026.

Este patch de manutenção preserva todos os comandos, configurações, VS Code `1.101.0+`, contratos de criação/edição visual no schema `1.0` e o provider MCP opcional e desabilitado por padrão. Ele incorpora melhorias em caminhos, mensagens limitadas, manutenção e testes de regressão sem adicionar binários da CLI ou do MCP ao VSIX.

A extensão continua executando uma CLI compatível instalada separadamente. A CLI `1.10.0` é a candidata preparada para o conjunto completo de comandos documentados; verificações de capacidade e fallbacks seguros permanecem ativos. MCP `1.0.1` é opcional, instalado de forma independente e publicado separadamente.

Após o merge, execute **VS Code VSIX Package** na `main` para gerar e validar `adr-guard-1.1.1.vsix`. O workflow não publica no Marketplace. A publicação no Marketplace exige uma ação separada e explicitamente autorizada da pessoa proprietária, com autenticação do Marketplace, seguida da confirmação de que `rodrioliveira.adr-guard` informa `1.1.1`.
