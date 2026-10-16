# Candidata a release do ADR Guard MCP v0.1.0

[English](mcp-v0.1.0.md)

Esta é a nota preparada para a primeira distribuição do ADR Guard MCP com versionamento independente. Ela não comprova que o pacote, a GitHub Release ou a entrada no Registry foram publicados.

## Incluído

- Sete ferramentas limitadas e somente leitura: `list_adrs`, `get_adr`, `search_adrs`, `get_adr_relationships`, `validate_adrs`, `analyze_impact` e `verify_architecture`.
- Uma .NET Tool dependente do framework .NET 10 chamada `RodriOliveira.AdrGuard.Mcp`, executada como `adr-guard-mcp` por `stdio`.
- A mesma implementação autoritativa de parsing, validação, impacto, drift, configuração e Git do ADR Guard; sem mecanismo paralelo de regras.
- Testes de instalação isolada e handshake MCP nos runners de release Windows, Linux e macOS.

## Instalação após a publicação

```shell
dotnet tool install --global RodriOliveira.AdrGuard.Mcp --version 0.1.0
adr-guard-mcp --repository /caminho/absoluto/do/repositorio --adr-directory docs/adr
```

O .NET 10 é obrigatório. Git é necessário para análise de impacto e evidência de drift associada a commit. O servidor não oferece transporte de rede, ferramentas de escrita, integração com providers, downloads automáticos nem autoridade para aprovar ADRs. Consulte a [visão geral](../mcp.pt-BR.md), a [configuração](../mcp-configuration.pt-BR.md) e o [modelo de segurança](../mcp-security.pt-BR.md).
