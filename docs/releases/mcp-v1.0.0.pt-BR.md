# ADR Guard MCP v1.0.0

[English](mcp-v1.0.0.md)

Esta é a primeira release estável do servidor ADR Guard MCP, versionado de forma independente. A versão `1.0.0` está publicada no [NuGet.org](https://www.nuget.org/packages/RodriOliveira.AdrGuard.Mcp/1.0.0), em sua [GitHub Release dedicada](https://github.com/rodri-oliveira-dev/adr-guard/releases/tag/mcp/v1.0.0) e no MCP Registry. Os sete nomes públicos de ferramentas, argumentos de inicialização, resultados estruturados no schema `1.0`, identidade do pacote, comando e transporte `stdio` foram revisados e cobertos por testes de contrato; isso não promete compatibilidade com futuras mudanças incompatíveis do protocolo ou do Registry.

## Incluído

- Sete ferramentas limitadas e somente leitura: `list_adrs`, `get_adr`, `search_adrs`, `get_adr_relationships`, `validate_adrs`, `analyze_impact` e `verify_architecture`.
- Uma .NET Tool dependente do framework .NET 10 com Package ID `RodriOliveira.AdrGuard.Mcp` e comando `adr-guard-mcp`.
- Nome oficial no Registry `io.github.rodri-oliveira-dev/adr-guard` e README do pacote com o marcador de propriedade obrigatório.
- Transporte `stdio` local, um repositório fixo por processo, contenção canônica de caminhos, proteção contra escapes por symlink/reparse point, limites de arquivos/busca/evidências/respostas/tempo, cancelamento e erros sanitizados.
- Resultados estruturados de validação, impacto e drift que preservam estados inválidos, desconhecidos, inconclusivos e de falha de política sem afirmar aprovação arquitetural ou conformidade.
- Testes de instalação do pacote, handshake MCP real, inicialização, descoberta das sete ferramentas, invocação, entrada inválida, impacto, drift, encerramento, manifesto e contrato do arquivo `.nupkg`.

## Instalação

```shell
dotnet tool install --global RodriOliveira.AdrGuard.Mcp --version 1.0.0
adr-guard-mcp --repository /caminho/absoluto/do/repositorio --adr-directory docs/adr
```

Para um manifesto local de ferramentas:

```shell
dotnet new tool-manifest
dotnet tool install --local RodriOliveira.AdrGuard.Mcp --version 1.0.0
dotnet tool run adr-guard-mcp -- --repository /caminho/absoluto/do/repositorio --adr-directory docs/adr
```

O .NET 10 é obrigatório. Git é necessário somente para análise de impacto e metadados de drift associados a commit. O servidor não oferece transporte de rede, ferramentas de escrita, integração com providers, download automático, aprovação de ADRs nem produção de evidências.

## Verificação da publicação

A release concluiu o workflow protegido `mcp-release`, as tags imutáveis de reserva/release, a verificação pública do pacote e a validação no Registry. O NuGet.org é o canal recomendado de instalação. O GitHub Packages continua sendo um canal secundário autenticado cujo backfill da versão `1.0.0` é uma operação manual separada e não deve republicar a release imutável.

Consulte a [visão geral](../mcp.pt-BR.md), a [configuração](../mcp-configuration.pt-BR.md), os [contratos das ferramentas](../mcp-tools.pt-BR.md), o [modelo de segurança](../mcp-security.pt-BR.md) e os [pré-requisitos de release](../mcp-release.pt-BR.md).
