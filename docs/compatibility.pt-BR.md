# Compatibilidade de formato e validação de ADR

O formato do documento e o rigor de validação são independentes. Os comandos existentes usam `canonical` com `legacy` por padrão, preservando comportamento publicado e códigos de saída.

| Perfil | Ciclo de vida | Texto de substituição | Placeholders | Metadados |
| --- | --- | --- | --- | --- |
| `legacy` | Quatro status históricos | Desligado | Desligado | Opcionais, sem validação |
| `advisory` | Status históricos | Desligado | Aviso | Opcionais, sem validação |
| `standard` | Adiciona `Rejected` | Ligado | Aviso | Validados |
| `strict` | Adiciona `Rejected` | Ligado | Erro | Validados |

Use `adr-format: canonical` ou `madr-4` independentemente de `validation-profile: legacy|advisory|standard|strict`. Configurações explícitas de ciclo de vida, nomes, placeholders e metadados refinam o perfil. Fingerprints de baseline mantêm a identidade de código, caminho e mensagem; avisos não alteram o código de saída. JSON adiciona `severity` apenas para avisos e SARIF usa o nível padrão `warning`.

## Versões das integrações

| Superfície | Limite de compatibilidade |
| --- | --- |
| CLI publicada | Consulte `adr-guard --version` instalado e o `--help` do comando; versões da extensão, MCP e CLI não são equivalentes. |
| Extensão VS Code | Exige VS Code `1.101.0` ou superior. Formulários visuais exigem contratos da CLI 1.7.0; fluxos de análise da branch consultam cada comando antes do uso. |
| Servidor MCP | Atualmente disponível apenas no código-fonte e compilado para .NET 10. Schemas das ferramentas são `1.0`; não existe pacote MCP versionado nem distribuição oficial publicada. |

Implementação no código-fonte, incorporação à branch padrão e publicação em um canal de distribuição são estados separados. Documentação de uma branch de desenvolvimento não estabelece disponibilidade em release.

