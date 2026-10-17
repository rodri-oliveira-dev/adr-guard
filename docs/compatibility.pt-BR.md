# Compatibilidade de formato e validação de ADR

O formato do documento e o rigor de validação são independentes. Os comandos existentes usam `canonical` com `legacy` por padrão, preservando comportamento publicado e códigos de saída.

| Perfil | Ciclo de vida | Texto de substituição | Placeholders | Metadados |
| --- | --- | --- | --- | --- |
| `legacy` | Quatro status históricos | Desligado | Desligado | Opcionais, sem validação |
| `advisory` | Status históricos | Desligado | Aviso | Opcionais, sem validação |
| `standard` | Adiciona `Rejected` | Ligado | Aviso | Validados |
| `strict` | Adiciona `Rejected` | Ligado | Erro | Validados |

Use `adr-format: canonical` ou `madr-4` independentemente de `validation-profile: legacy|advisory|standard|strict`. Configurações explícitas de ciclo de vida, nomes, placeholders e metadados refinam o perfil. Fingerprints de baseline mantêm a identidade de código, caminho e mensagem; avisos não alteram o código de saída. JSON adiciona `severity` apenas para avisos e SARIF usa o nível padrão `warning`.

## Matriz de compatibilidade dos produtos

Os produtos usam linhas SemVer independentes. Números iguais não são obrigatórios, e disponibilidade no código-fonte não comprova publicação.

| Produto / capacidade | Versão no repositório | Dependência mínima | Runtime / host | Dependências opcionais | Funciona sem MCP? | Publicação verificada em 2026-10-17 |
| --- | --- | --- | --- | --- | --- | --- |
| CLI / .NET Tool | `1.8.0` | Nenhuma | .NET 10 | Git somente para validação incremental, review comparativo, impacto e metadados de drift associados a commit; provider de IA configurado somente para `draft`/`review` | Sim | GitHub Release `v1.8.0` publicada com arquivo `.nupkg`; verificar separadamente a disponibilidade no NuGet.org. |
| Comandos básicos, Problems e Explorer no VS Code | `1.1.0` | CLI `1.3.0+` compatível para o conjunto básico documentado | VS Code `1.101.0+`; host Node de workspace; workspace local `file:` confiável | Requisitos de Git/provider vêm do comando da CLI escolhido | Sim | Candidata `1.1.0` no código-fonte; última baseline documentada do Marketplace `1.0.0`. Verificar a listagem do Marketplace separadamente. |
| Criação/edição visual no VS Code | `1.1.0` | CLI `1.7.0+`, schema de formulário `1.0` | Mesmo limite do VS Code | Nenhuma além da CLI | Sim | Preparada como parte da extensão 1.1.0; a extensão consulta o contrato. |
| Impacto, drift, baseline, catálogo enriquecido, draft e review/comparação no VS Code | `1.1.0` | Build da CLI cujo `<command> --help` exponha o contrato pedido; CLI `1.8.0` é a recomendação coordenada | Mesmo limite do VS Code | Git/mapeamentos/evidências locais ou provider de IA somente quando o fluxo escolhido exigir | Sim | Preparada como parte da extensão 1.1.0. |
| Provider MCP do VS Code | `1.1.0` | MCP `1.0.0` instalado separadamente | VS Code `1.101.0+`, host de workspace local/remoto confiável | `dotnet` no `PATH` do host da extensão quando configurado por DLL | Não aplicável; esta linha é a integração opcional | Preparado, desabilitado por padrão e ainda ausente da release publicada no Marketplace. |
| Servidor MCP / .NET Tool | `1.0.0` | Nenhuma; a extensão não é obrigatória | .NET 10, cliente `stdio` local | Git somente para `analyze_impact` e metadados de commit | Não aplicável; executa de forma independente | O MCP 1.0.0 está publicado no NuGet.org, GitHub Releases e MCP Registry. O backfill para GitHub Packages é uma etapa manual após o merge do workflow de distribuição. |

A CLI executa independentemente da extensão e do MCP. A extensão preserva todo o comportamento apoiado pela CLI quando MCP está desabilitado, ausente ou incompatível. O servidor MCP funciona com qualquer cliente `stdio` compatível e não exige a extensão.

Confirme uma CLI instalada com `adr-guard --version` e o `--help` do comando relevante. Confirme versões da extensão no Visual Studio Marketplace e versões MCP tanto no NuGet.org quanto no MCP Registry. Branch, pacote local, VSIX, manifesto ou nota de release isolados não comprovam disponibilidade pública.

