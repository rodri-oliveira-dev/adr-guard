# Compatibilidade de formato e validação de ADR

O formato do documento e o rigor de validação são independentes. Os comandos existentes usam `canonical` com `legacy` por padrão, preservando comportamento publicado e códigos de saída.

| Perfil | Ciclo de vida | Texto de substituição | Placeholders | Metadados |
| --- | --- | --- | --- | --- |
| `legacy` | Quatro status históricos | Desligado | Desligado | Opcionais, sem validação |
| `advisory` | Status históricos | Desligado | Aviso | Opcionais, sem validação |
| `standard` | Adiciona `Rejected` | Ligado | Aviso | Validados |
| `strict` | Adiciona `Rejected` | Ligado | Erro | Validados |

Use `adr-format: canonical` ou `madr-4` independentemente de `validation-profile: legacy|advisory|standard|strict`. Configurações explícitas de ciclo de vida, nomes, placeholders e metadados refinam o perfil. Fingerprints de baseline mantêm a identidade de código, caminho e mensagem; avisos não alteram o código de saída. JSON adiciona `severity` apenas para avisos e SARIF usa o nível padrão `warning`.

A validação MADR 4.0 está publicada e permanece opcional. A geração MADR nativa por `new` e `new --form` está atribuída à candidata preparada da CLI `1.11.0`, mas ainda não possui pacote ou imagem publicados. A geração canônica continua sendo o padrão nos dois casos.

## Matriz de compatibilidade dos produtos

Os produtos usam linhas SemVer independentes. Números iguais não são obrigatórios, e disponibilidade no código-fonte não comprova publicação. Na tabela, **publicado** significa disponível no registro público indicado, **candidato** significa um baseline versionado de código/pacote preparado e validado para release, mas não promovido a um registro, e **somente código-fonte** significa implementação presente sem baseline de release para consumidores.

| Produto / capacidade | Versão no repositório | Dependência mínima | Runtime / host | Dependências opcionais | Funciona sem MCP? | Estado de distribuição verificado em 05/11/2026 |
| --- | --- | --- | --- | --- | --- | --- |
| CLI / .NET Tool | candidata `1.11.0` | Nenhuma | .NET 10 | Git somente para validação incremental, review comparativo, impacto e metadados de drift associados a commit; provider de IA configurado somente para `draft`/`review` | Sim | **Publicado:** `1.10.0` no NuGet.org e na GitHub Release exata `v1.10.0`. **Não publicado:** `1.11.0`, incluindo a geração MADR nativa. |
| Geração MADR 4.0 nativa (`new`, `new --form`) | candidata `1.11.0` | CLI candidata `1.11.0`; clientes estruturados continuam usando o schema de formulário `1.0` | .NET 10 | Nenhuma | Sim | **Publicado:** somente validação MADR, desde a CLI `1.3.0`. **Não publicado:** geração MADR nativa em qualquer pacote, imagem, tag da Action ou release. A geração canônica continua sendo o padrão. |
| Grafo de relacionamentos somente leitura (`relationships`) | somente código-fonte após o baseline `1.11.0` | Build do código cujo help exponha o schema `1.0` | .NET 10 | Nenhuma | Sim | **Não publicado:** o contrato aditivo da CLI e o grafo compatível do VS Code estão disponíveis juntos no código-fonte; não alteram MCP `1.0` nem JSON/SARIF de check. |
| Comandos básicos, Problems e Explorer no VS Code | candidata `1.1.1` | CLI `1.3.0+` compatível para o conjunto básico documentado | VS Code `1.101.0+`; host Node de workspace; workspace local `file:` confiável | Requisitos de Git/provider vêm do comando da CLI escolhido | Sim | **Publicado:** Marketplace `1.1.0`. **Não publicado:** patch candidato `1.1.1`. Artifacts VSIX dos workflows são um canal separado de instalação por 21 dias e nunca são publicados automaticamente. |
| Criação/edição visual no VS Code | candidata `1.1.1` | CLI `1.7.0+`, schema de formulário `1.0` | Mesmo limite do VS Code | Nenhuma além da CLI | Sim | **Publicado:** Marketplace `1.1.0`. A candidata `1.1.1` preserva o schema `1.0`. |
| Impacto, drift, baseline, catálogo enriquecido, draft e review/comparação no VS Code | candidata `1.1.1` | Build da CLI cujo `<command> --help` exponha o contrato pedido; a CLI publicada `1.10.0` suporta os fluxos documentados | Mesmo limite do VS Code | Git/mapeamentos/evidências locais ou provider de IA somente quando o fluxo escolhido exigir | Sim | **Publicado:** Marketplace `1.1.0`; a candidata `1.1.1` ainda exige uma CLI instalada separadamente. |
| Provider MCP do VS Code | candidata `1.1.1` | MCP `1.0.0+` instalado separadamente com contratos schema `1.0` inalterados | VS Code `1.101.0+`, host de workspace local/remoto confiável | `dotnet` no `PATH` do host da extensão quando configurado por DLL | Não aplicável; esta linha é a integração opcional | **Publicado:** incluído no Marketplace `1.1.0`, desabilitado por padrão. O MCP `1.0.1` está publicado independentemente no NuGet.org e em GitHub Releases; o VS Code `1.1.1` permanece não publicado. |
| Servidor MCP / .NET Tool | versão lançada `1.0.1` | Nenhuma; a extensão não é obrigatória | .NET 10, cliente `stdio` local | Git somente para `analyze_impact` e metadados de commit | Não aplicável; executa de forma independente | **Publicado:** `1.0.1` no NuGet.org e na GitHub Release exata `mcp/v1.0.1`. O manifesto versionado do MCP Registry permanece em `1.0.0`; verifique o Registry separadamente. |
| GitHub Action | `v1.10.0` exata / `v1` móvel publicadas; `v1.11.0` candidata | Container da CLI na mesma release coordenada | Runner Linux com Docker; Python 3 para renderização limitada de review/impact/drift | Credenciais de provider somente no `review` explícito; histórico Git/mapa somente para `impact`; regras/baseline/evidências locais para `drift` | Sim | **Publicado:** `v1` e `v1.10.0` atuais expõem `check`, `index`, `review` opt-in, `impact` opt-in e `drift` somente leitura. **Não publicado:** `v1.11.0`; a Action não expõe o comando `new` nem a geração MADR nativa. |

A CLI executa independentemente da extensão e do MCP. A extensão preserva todo o comportamento apoiado pela CLI quando MCP está desabilitado, ausente ou incompatível. O servidor MCP funciona com qualquer cliente `stdio` compatível e não exige a extensão.

## Limites internos de assemblies e pacotes

As builds atuais do código-fonte usam este grafo de dependências:

```text
VS Code / GitHub Action ──processo ou container──> CLI AdrGuard ──> AdrGuard.Core
cliente MCP compatível ───────────────stdio──────> AdrGuard.Mcp ──> AdrGuard.Core
```

`AdrGuard.Core` é uma biblioteca de classes interna, sem pacote próprio. Ela contém parsing, validação, configuração, inventário Git limitado, impacto e drift compartilhados, mas não expõe um produto para consumidores com versionamento independente. Os pacotes da CLI `1.10.0` publicada e da candidata `1.11.0` incluem `adr-guard` e `AdrGuard.Core.dll` e excluem MCP. Os pacotes publicados do MCP `1.0.1` incluem `adr-guard-mcp` e `AdrGuard.Core.dll` e excluem `adr-guard.dll`. CLI e MCP preservam executáveis, IDs de pacote, versões, tags de release e workflows de publicação separados.

Confirme uma CLI instalada com `adr-guard --version` e o `--help` do comando relevante. Confirme versões da extensão no Visual Studio Marketplace e versões MCP tanto no NuGet.org quanto no MCP Registry. Branch, pacote local, VSIX de workflow, manifesto ou nota de release isolados não comprovam publicação em registro.

