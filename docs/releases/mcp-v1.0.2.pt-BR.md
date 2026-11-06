# ADR Guard MCP v1.0.2

[English](mcp-v1.0.2.md)

<!-- release-candidate-only:start -->
ADR Guard MCP `1.0.2` está preparado no código-fonte, mas **não foi publicado**. MCP `1.0.1` continua disponível no NuGet.org, em GitHub Releases e no MCP Registry oficial. Este patch possui release independente da CLI `1.12.0` e do VS Code `1.2.0`.
<!-- release-candidate-only:end -->

## Mudanças

- Reutiliza a implementação determinística compartilhada do grafo em `AdrGuard.Core` na ferramenta existente `get_adr_relationships`.
- Mantém a descoberta de declarações em seções, status convencional e supersessão MADR alinhada à validação da CLI, preservando o comportamento específico de formato.
- Normaliza alvos declarados MADR de forma consistente e reutiliza o conjunto autoritativo de códigos de diagnóstico de relacionamentos.

## Compatibilidade e limites

- As mesmas sete ferramentas somente leitura, transporte `stdio` local, argumentos de inicialização, schema `1.0`, campos estruturados, limites de resposta, semântica de erros e anotações são preservados.
- Nenhuma ferramenta MCP foi adicionada ou removida. O servidor não cria, edita, aprova, substitui ou altera o status de ADRs e nunca infere relacionamentos não declarados.
- O pacote inclui `adr-guard-mcp` e `AdrGuard.Core.dll`, exclui o executável da CLI, exige .NET 10 e continua instalável de forma independente.

## Processo de release

<!-- release-candidate-only:start -->
Depois que a CLI `1.12.0` concluir sua release coordenada e verificação pública, dispare **MCP Release** na `main` com `version=1.0.2`. O workflow protegido valida, testa, empacota, instala, executa smoke tests das sete ferramentas, reserva a versão e então publica no NuGet.org, GitHub Packages, GitHub Releases e MCP Registry oficial por seus jobs protegidos.
<!-- release-candidate-only:end -->

O merge desta preparação não publica pacote MCP, manifesto, entrada no Registry, tag ou release.
