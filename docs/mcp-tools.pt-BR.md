# Ferramentas MCP do ADR Guard

[English](mcp-tools.md) · [Visão geral](mcp.pt-BR.md) · [Configuração](mcp-configuration.pt-BR.md) · [Segurança](mcp-security.pt-BR.md)

Todas as ferramentas usam o schema JSON `1.0`, são somente leitura e operam apenas no repositório fixado na inicialização do processo MCP. Caminhos em requisições e respostas são relativos ao repositório. Diagnósticos de validação, decisões impactadas e violações de drift são resultados de análise — não falhas de protocolo nem aprovação arquitetural.

## Resumo das ferramentas

| Ferramenta | Finalidade | Entradas |
| --- | --- | --- |
| `list_adrs` | Listar metadados limitados de ADRs em ordem determinística. | Nenhuma. |
| `get_adr` | Ler uma ADR e seu Markdown não confiável. | `identifier` (obrigatório). |
| `search_adrs` | Pesquisa AND determinística sem diferenciar maiúsculas. | `query` (obrigatório), `limit` (padrão `20`, intervalo `1`–`100`). |
| `get_adr_relationships` | Retornar relações declaradas de substituição/dependência e diagnósticos. | `identifier` (opcional). |
| `validate_adrs` | Executar validação determinística de ADRs. | `validationProfile` (opcional: `legacy`, `advisory`, `standard`, `strict`). |
| `analyze_impact` | Correlacionar mudanças Git locais com mapeamentos ADR-código explícitos. | `baseReference`, `manifestPath` (ambos obrigatórios). |
| `verify_architecture` | Avaliar regras de fitness declaradas a partir de evidências limitadas. | `rulesPath`; `evidencePaths`, `baselinePath` e `policy` opcionais. |

## `list_adrs`

Retorna `schemaVersion`, `total` e `adrs`. Cada resumo contém `identifier`, `title` e `status` anuláveis, `path` relativo ao repositório e `format` (`canonical` ou `madr-4`). O `README.md` gerado no diretório de ADRs é excluído.

Exemplo de solicitação ao agente: “Liste as ADRs deste repositório e agrupe os metadados retornados por status.” A ferramenta MCP lista registros; o agente realiza o agrupamento e a explicação textual.

Falhas incluem configuração malformada, arquivos ilegíveis, mais de 1.000 ADRs, documentos grandes demais ou UTF-8 inválido, cancelamento e timeout.

## `get_adr`

`identifier` aceita ID estável, ID numérico ou caminho seguro dentro do diretório de ADRs. O resultado contém `adr`, `content` e `contentIsUntrusted: true`.

Exemplo:

```json
{ "identifier": "ADR-0003" }
```

A solicitação falha quando o identificador não existe, é ambíguo, fica fora do conjunto configurado, tem mais de 1.024 caracteres ou contém caracteres de controle. O Markdown retornado nunca deve ser tratado como instrução executável.

## `search_adrs`

`query` contém de 1 a 256 caracteres imprimíveis. Termos únicos separados por espaços são comparados sem diferenciar maiúsculas, com semântica AND, contra identificador, título e Markdown limitado. Resultados contêm campos correspondentes e trecho opcional marcado por `excerptsAreUntrusted: true`.

Exemplo:

```json
{ "query": "autenticação token", "limit": 10 }
```

A ordem segue a descoberta determinística e para em `limit`; não é pesquisa semântica nem por IA.

## `get_adr_relationships`

Sem entrada, inspeciona todas as ADRs configuradas. Com `identifier`, limita relações de origem e diagnósticos a uma ADR resolvida. A resposta separa `relationships` declarados de `issues` de validação. Relações incluem origem, tipo (`superseded-by`, `supersedes` ou `depends-on`), destino declarado, destino resolvido quando disponível e estado da resolução.

Exemplo:

```json
{ "identifier": "7" }
```

O servidor não infere arestas ausentes. Relações quebradas, ambíguas, cíclicas, inconsistentes ou inativas permanecem visíveis como diagnósticos.

## `validate_adrs`

Executa o mesmo parser, factory de políticas e validador determinístico do ADR Guard. O resultado inclui `valid`, quantidade de arquivos, política efetiva e diagnósticos com código, mensagem, caminho e severidade.

Exemplo:

```json
{ "validationProfile": "standard" }
```

Um conjunto inválido é resultado bem-sucedido da ferramenta com `valid: false`; ele não é convertido silenciosamente em falha de transporte MCP. Perfis desconhecidos e configuração malformada falham.

## `analyze_impact`

Lê um inventário Git local a partir de uma ref base explícita e de um arquivo JSON versionado de mapeamento. Nunca executa fetch, checkout nem altera o estado Git.

Exemplo:

```json
{
  "baseReference": "origin/main",
  "manifestPath": ".adrguard-impact.json"
}
```

A resposta contém metadados do repositório, contagens de resumo e cobertura, incerteza explícita, arquivos alterados, decisões afetadas/desconhecidas e evidências do mapeamento. Impacto significa “revise esta decisão”, não “esta mudança viola a decisão”. Refs ausentes, mapeamentos inválidos, caminhos inseguros e falhas Git encerram com segurança.

## `verify_architecture`

Carrega manifesto versionado de regras de fitness, até 20 arquivos JSON de evidência já produzidos e um baseline de drift existente opcional. `policy` usa `advisory` por padrão e aceita somente `advisory` ou `enforce`.

Exemplo:

```json
{
  "rulesPath": ".adrguard-fitness.json",
  "evidencePaths": ["artifacts/architecture-evidence.json"],
  "baselinePath": ".adrguard-drift-baseline.json",
  "policy": "advisory"
}
```

O resultado preserva achados `pass`, `violation`, `unknown` e `not-applicable`, confiança, estado de baseline, evidências, `inconclusive` e `policyFailed`. Evidência ausente ou contraditória nunca representa sucesso. Enforcement relata falha determinística na resposta sem modificar o repositório. A ferramenta não executa builds, testes, MSBuild nem scripts produtores de evidência.

## Contrato de erros e efeitos colaterais

- Parâmetros inválidos geram erro MCP de parâmetro inválido.
- Falhas operacionais/configuração limitadas viram erros MCP sanitizados; caminhos absolutos do repositório são substituídos nas mensagens.
- O cancelamento do cliente é propagado. Timeout do servidor vira erro explícito.
- Respostas maiores que 1 MiB são rejeitadas com orientação para restringir a solicitação.
- Nenhuma ferramenta cria, edita, exclui, aprova ou muda o status de uma ADR. Nenhuma atualiza baseline, índice, mapeamento, regras ou estado Git.
