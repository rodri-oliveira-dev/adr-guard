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

Os produtos usam linhas SemVer independentes. Números iguais não são obrigatórios, e disponibilidade no código-fonte não comprova publicação. Na tabela, **publicado** significa disponível no registro público indicado, **candidato** significa um artifact validado para download que não foi promovido a um registro e **somente código-fonte** significa implementação presente sem release para consumidores.

| Produto / capacidade | Versão no repositório | Dependência mínima | Runtime / host | Dependências opcionais | Funciona sem MCP? | Estado de distribuição verificado em 18/10/2026 |
| --- | --- | --- | --- | --- | --- | --- |
| CLI / .NET Tool | `1.8.0` | Nenhuma | .NET 10 | Git somente para validação incremental, review comparativo, impacto e metadados de drift associados a commit; provider de IA configurado somente para `draft`/`review` | Sim | **Publicado:** NuGet.org e GitHub Release `v1.8.0`; distribuições coordenadas de container e Action seguem a linha da CLI. |
| Comandos básicos, Problems e Explorer no VS Code | `1.1.0` | CLI `1.3.0+` compatível para o conjunto básico documentado | VS Code `1.101.0+`; host Node de workspace; workspace local `file:` confiável | Requisitos de Git/provider vêm do comando da CLI escolhido | Sim | **Publicado:** Marketplace `1.0.1`. **Candidato:** VSIX `1.1.0` dos workflows concluídos com sucesso, retido por 21 dias e nunca publicado automaticamente. |
| Criação/edição visual no VS Code | `1.1.0` | CLI `1.7.0+`, schema de formulário `1.0` | Mesmo limite do VS Code | Nenhuma além da CLI | Sim | **Publicado:** disponível no Marketplace `1.0.1`; preservado pela candidata `1.1.0`. |
| Impacto, drift, baseline, catálogo enriquecido, draft e review/comparação no VS Code | `1.1.0` | Build da CLI cujo `<command> --help` exponha o contrato pedido; CLI `1.8.0` é a recomendação coordenada | Mesmo limite do VS Code | Git/mapeamentos/evidências locais ou provider de IA somente quando o fluxo escolhido exigir | Sim | **Candidato:** disponível nos artifacts VSIX `1.1.0` validados pelo workflow; ausente do Marketplace `1.0.1`. |
| Provider MCP do VS Code | `1.1.0` | MCP `1.0.0` instalado separadamente | VS Code `1.101.0+`, host de workspace local/remoto confiável | `dotnet` no `PATH` do host da extensão quando configurado por DLL | Não aplicável; esta linha é a integração opcional | **Candidato:** incluído nos artifacts VSIX `1.1.0` validados pelo workflow, desabilitado por padrão; ausente do Marketplace `1.0.1`. |
| Servidor MCP / .NET Tool | `1.0.0` | Nenhuma; a extensão não é obrigatória | .NET 10, cliente `stdio` local | Git somente para `analyze_impact` e metadados de commit | Não aplicável; executa de forma independente | **Publicado:** NuGet.org, GitHub Release `mcp/v1.0.0` e MCP Registry. **Canal secundário pendente:** o backfill autenticado para GitHub Packages ainda não foi executado. |

A CLI executa independentemente da extensão e do MCP. A extensão preserva todo o comportamento apoiado pela CLI quando MCP está desabilitado, ausente ou incompatível. O servidor MCP funciona com qualquer cliente `stdio` compatível e não exige a extensão.

Confirme uma CLI instalada com `adr-guard --version` e o `--help` do comando relevante. Confirme versões da extensão no Visual Studio Marketplace e versões MCP tanto no NuGet.org quanto no MCP Registry. Branch, pacote local, VSIX de workflow, manifesto ou nota de release isolados não comprovam publicação em registro.

