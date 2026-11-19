# ADR Guard para VS Code 1.1.1 — publicada

[English](vscode-v1.1.1.md)

A extensão ADR Guard para VS Code `1.1.1` está publicada no [Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=rodrioliveira.adr-guard), conforme verificação de 19/11/2026. A publicação é independente da CLI e do servidor MCP.

Este patch de manutenção preserva todos os comandos, configurações, VS Code `1.101.0+`, contratos de criação/edição visual no schema `1.0` e o provider MCP opcional e desabilitado por padrão. Ele incorpora melhorias em caminhos, mensagens limitadas, manutenção e testes de regressão sem adicionar binários da CLI ou do MCP ao VSIX.

A extensão continua executando uma CLI compatível instalada separadamente. A CLI `1.12.0` publicada fornece os contratos de criação/edição visual e geração MADR, além da exportação de relacionamentos, que a extensão não expõe diretamente. Verificações de capacidade e fallbacks seguros permanecem ativos. MCP `1.0.1` é opcional, instalado e publicado de forma independente.

O workflow **VS Code VSIX Package** apenas gera e valida o arquivo `adr-guard-1.1.1.vsix`; a publicação já realizada no Marketplace dependeu de uma etapa separada e autorizada pela pessoa mantenedora. Um artifact de CI, isoladamente, não comprova disponibilidade pública.
