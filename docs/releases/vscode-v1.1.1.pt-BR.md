# ADR Guard para VS Code 1.1.1

[English](vscode-v1.1.1.md)

ADR Guard para VS Code `1.1.1` foi publicado no Visual Studio Marketplace em 05/11/2026. Este patch de manutenção antecede e não inclui o grafo de relacionamentos da Fase 3 preparado para a versão `1.2.0`.

## Mudanças

- Melhora o tratamento de caminhos do executável e workspace, mensagens limitadas para usuários, manutenibilidade dos adapters e cobertura de regressão.
- Preserva todos os comandos, configurações, VS Code `1.101.0+`, contratos visuais de criação/edição no schema `1.0` e provider MCP opcional e desabilitado por padrão.
- Continua executando uma CLI compatível instalada separadamente; o VSIX não inclui CLI nem MCP.

## Compatibilidade

- A CLI `1.11.0` publicada oferece o conjunto de comandos disponível quando esta extensão foi lançada.
- MCP `1.0.1` é opcional, instalado de forma independente e lançado separadamente.
- O grafo de relacionamentos exige a extensão `1.2.0` e uma CLI que exponha o schema `1.0` de `relationships`; ele não integra esta versão imutável do Marketplace.
