# ADR Guard MCP v1.0.1

[English](mcp-v1.0.1.md)

ADR Guard MCP `1.0.1` foi publicado em 05/11/2026 no NuGet.org, GitHub Packages, GitHub Releases e MCP Registry oficial.

## Destaques

- Referencia diretamente o projeto interno `AdrGuard.Core` para parsing, validação, configuração, inventário Git, impacto e drift compartilhados.
- Remove qualquer dependência de build ou runtime no executável da CLI ADR Guard. O pacote inclui `adr-guard-mcp` e `AdrGuard.Core.dll` e exclui `adr-guard.dll`.
- Mantém as sete ferramentas somente leitura no schema `1.0` por `stdio` local.
- Resolve aliases físicos de diretórios Git antes da contenção para evitar falsas violações entre caminhos equivalentes `/var` e `/private/var` no macOS.

## Compatibilidade e requisitos

- Exige .NET 10 e um cliente MCP local compatível com `stdio`.
- Nomes das ferramentas, argumentos de inicialização, schema `1.0`, campos estruturados, limites, semântica de erros, transporte e anotações somente leitura permanecem iguais à versão `1.0.0`.
- Git continua opcional e é usado somente para análise de impacto e metadados arquiteturais associados a commit.
- O servidor permanece independente da CLI e da extensão VS Code e não pode criar, editar, aprovar ou alterar o status de ADRs.

Instale com:

```shell
dotnet tool install --global RodriOliveira.AdrGuard.Mcp --version 1.0.1
adr-guard-mcp --repository /caminho/absoluto/do/repo --adr-directory docs/adr
```
