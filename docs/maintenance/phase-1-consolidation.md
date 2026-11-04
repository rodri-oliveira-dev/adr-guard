# Fase 1 de consolidação técnica

## Objetivo e limites

Este plano registra o diagnóstico do código em `main` no commit
`4cf208f3ddf9fb14944b68db18aa3548ff4553b6`, consultado em 3 de novembro de
2026. O objetivo da Fase 1 é reduzir o custo de mudança nos adaptadores da CLI,
na geração de ADRs, nos templates e no review sem alterar comportamento
observável.

Esta fase é um refactor de preservação de comportamento. Ela não autoriza novas
funcionalidades, mudanças de comandos ou argumentos, novos contratos, alteração
de versões, publicação de pacotes, mudança de defaults ou ampliação da API
pública. A separação de responsabilidades deve continuar obedecendo à
[ADR 0008](../adr/0008-extract-an-internal-shared-core.md): regras reutilizáveis
pertencem ao Core; roteamento, apresentação, providers, escrita e adaptação de
exit codes permanecem nos executáveis apropriados.

## Diagnóstico técnico resumido

### CLI

`src/AdrGuard/Cli/CliApplication.cs` tem 1.948 linhas e reúne quatro tipos de
responsabilidade:

1. texto de ajuda e contrato de uso de todos os comandos;
2. roteamento e composição de dependências;
3. parsing e validação sintática dos argumentos;
4. adaptação de exceções, mensagens, streams e exit codes.

Os parsers de `check`, `index`, `review`, `draft`, `impact` e `drift` repetem o
mesmo controle manual de posição, duplicidade, valor ausente e opção
desconhecida. Ao mesmo tempo, métodos como `RunBaseline` e `RunDraft` também
compõem configuração, filesystem, Git, providers e apresentação. O resultado é
acoplamento por fluxo: uma opção nova ou uma extração aparentemente local pode
alterar ordem de validação, mensagem, stream de saída ou exit code de outro
comando.

Este é o principal problema real da fase. Os oito findings de complexidade do
Sonar em `CliApplication` coincidem com métodos longos e com alto risco de
regressão contratual; não são apenas sugestões de formatação.

### Geração e templates

A área de geração já possui limites úteis entre provider, transporte HTTP,
contexto, renderização e persistência atômica. Entretanto, o contrato de
templates está distribuído entre `AdrCustomTemplateLoader`,
`AdrBuiltInTemplates`, `AdrTemplateSelection`, `AdrMarkdownRenderer`,
`GeneratedAdrStructureGuard`, `AdrCreationService` e os comandos `new`/`draft`.

Há três pontos de atenção:

- `AdrCustomTemplateLoader.Load` combina resolução segura de caminho, leitura
  limitada, validação de encoding, normalização e validação léxica; `Parse`
  combina tokenização de headings, montagem do modelo e regras canônicas.
- `AdrMarkdownRenderer.RenderTemplate` valida o request, monta substituições,
  aplica placeholders, impede injeção estrutural e produz o contrato exato de
  Markdown em uma única operação.
- `GeneratedAdrStructureGuard` implementa um scanner Markdown sensível a fences,
  headings ATX e Setext. A complexidade representa casos reais de segurança e
  compatibilidade, portanto uma extração deve preservar a máquina de estados em
  vez de apenas reduzir a métrica.

Os testes de snapshots, placeholders hostis, concorrência e persistência atômica
oferecem boa proteção. O risco está em quebrar whitespace, LF versus newline do
host, ordem de validação, mensagens ou o princípio de nunca sobrescrever um ADR.

### Review

O review separa provider, política, segurança e reporting, mas ainda concentra
fluxos extensos em `ReviewCommand.Run` e em renderizadores/builders. O comando
carrega e valida o ADR, avalia política, constrói e sanitiza contexto, cria o
provider, executa a chamada, constrói o relatório, serializa e opcionalmente
persiste o resultado. Essa concentração não apareceu como finding HIGH no
recorte do Sonar, mas é um acoplamento real e mais relevante do que alguns
findings próximos do limite.

No reporting, `AdrReviewReportBuilder.ResolveEvidence` mistura parsing de
referências, rejeição de paths, resolução de ambiguidade e ordenação.
`AdrReviewTextRenderer.Render` mistura todas as seções do documento. Já
`AdrReviewPolicyLoader.Validate`, com complexidade 16 para limite 15, é
predominantemente validação sequencial de um contrato pequeno; isoladamente é
uma sugestão de baixa prioridade, não evidência de desenho defeituoso.

Qualquer mudança nessa área precisa manter o review como material consultivo,
preservar `unknown` quando não há evidência, impedir vazamento de credenciais e
nunca converter texto do provider em aprovação ou policy compliance.

### AdrGuard.Core

O Core está corretamente separado como biblioteca interna e não empacotável.
Os testes de arquitetura verificam o grafo `CLI -> Core` e `MCP -> Core`, a
ausência de dependência entre executáveis e a aciclicidade. A busca dirigida não
encontrou uso de namespaces da CLI ou do MCP no Core.

Há complexidade real em parsers e loaders de entrada não confiável, especialmente
`AdrMarkdownParser`, `AdrGuardConfigurationLoader`, `FitnessManifestLoader`,
`DotNetProjectReferenceChecker`, `DriftEngine` e
`ImpactCorrelationService`. Porém, uma reorganização ampla do Core logo após a
extração aumentaria o risco para CLI e MCP simultaneamente. A Fase 1 deve tratar
apenas seams coesos, um domínio por vez, depois de estabilizar os adaptadores da
CLI.

Os tipos de exceção internos indicados pelo Sonar não devem ser tornados
públicos. `AdrGuard.Core` não é uma API pública ou um produto versionado, e a
visibilidade interna é deliberada. Torná-los públicos apenas para satisfazer a
regra `S3871` violaria a ADR 0008 e criaria uma superfície de compatibilidade
sem consumidor.

## Componentes prioritários

| Prioridade | Componente | Motivo | Limite da mudança |
| --- | --- | --- | --- |
| P0 | `CliApplication` — parsing de argumentos | Oito métodos complexos, lógica repetida e contrato público sensível a ordem e mensagens | Extrair parsers internos tipados por comando sem mudar sintaxe, defaults ou diagnósticos |
| P0 | `CliApplication` — roteamento e orquestração | Composição de filesystem, Git, providers, configuração, streams e exit codes em uma classe | Delegar a handlers existentes ou focados, mantendo a CLI como adaptador |
| P1 | `AdrCustomTemplateLoader` e `AdrMarkdownRenderer` | Contrato de template espalhado e validações de segurança intercaladas com transformação | Separar leitura, parsing, validação e rendering sem unificar contratos que hoje diferem |
| P1 | `ReviewCommand`, report builder e renderer | Fluxo extenso em boundary de segurança e contrato JSON/texto versionado | Extrair passos puros e adapters sem alterar schema `1.0`, disclosure ou policy semantics |
| P2 | Core — configuration, parsing, drift e impact | Complexidade real em regras compartilhadas por CLI e MCP | Uma extração coesa por domínio; nenhum movimento motivado apenas por contagem de linhas |
| Não executar | Exceções internas do Core | Finding estilístico conflita com o limite arquitetural | Manter `internal`; avaliar marcação do finding como accepted/false positive com justificativa |

## Issues do SonarQube Cloud e validade atual

A consulta autenticada à branch `main` do projeto
`rodri-oliveira-dev_adr-guard` retornou 68 findings abertos com impacto HIGH ou
BLOCKER. O recorte desta fase contém 32 findings, todos de
`MAINTAINABILITY/HIGH`; não há finding BLOCKER nesse recorte. São 27 ocorrências
de `csharpsquid:S3776` e cinco de `csharpsquid:S3871`.

| Área | Issues sincronizadas | Avaliação |
| --- | --- | --- |
| `CliApplication` | [#218](https://github.com/rodri-oliveira-dev/adr-guard/issues/218), [#258](https://github.com/rodri-oliveira-dev/adr-guard/issues/258), [#263](https://github.com/rodri-oliveira-dev/adr-guard/issues/263), [#267](https://github.com/rodri-oliveira-dev/adr-guard/issues/267), [#268](https://github.com/rodri-oliveira-dev/adr-guard/issues/268), [#271](https://github.com/rodri-oliveira-dev/adr-guard/issues/271), [#279](https://github.com/rodri-oliveira-dev/adr-guard/issues/279), [#280](https://github.com/rodri-oliveira-dev/adr-guard/issues/280) | Válidas e prioritárias. Complexidades reportadas entre 26 e 100 correspondem a parsing/orquestração ainda presentes. |
| Geração/templates | [#252](https://github.com/rodri-oliveira-dev/adr-guard/issues/252), [#282](https://github.com/rodri-oliveira-dev/adr-guard/issues/282), [#283](https://github.com/rodri-oliveira-dev/adr-guard/issues/283), [#284](https://github.com/rodri-oliveira-dev/adr-guard/issues/284) | Válidas. `#283` é a prioridade estrutural; as demais devem ser resolvidas apenas junto a seams coerentes, pois protegem contratos e casos hostis. |
| Review | [#276](https://github.com/rodri-oliveira-dev/adr-guard/issues/276), [#277](https://github.com/rodri-oliveira-dev/adr-guard/issues/277), [#278](https://github.com/rodri-oliveira-dev/adr-guard/issues/278) | `#277` e `#278` representam responsabilidades misturadas. `#276` excede o limite por um ponto e, isoladamente, é estilística/baixa prioridade. |
| Core — complexidade | [#219](https://github.com/rodri-oliveira-dev/adr-guard/issues/219), [#235](https://github.com/rodri-oliveira-dev/adr-guard/issues/235), [#236](https://github.com/rodri-oliveira-dev/adr-guard/issues/236), [#253](https://github.com/rodri-oliveira-dev/adr-guard/issues/253), [#254](https://github.com/rodri-oliveira-dev/adr-guard/issues/254), [#255](https://github.com/rodri-oliveira-dev/adr-guard/issues/255), [#257](https://github.com/rodri-oliveira-dev/adr-guard/issues/257), [#259](https://github.com/rodri-oliveira-dev/adr-guard/issues/259), [#261](https://github.com/rodri-oliveira-dev/adr-guard/issues/261), [#264](https://github.com/rodri-oliveira-dev/adr-guard/issues/264), [#265](https://github.com/rodri-oliveira-dev/adr-guard/issues/265), [#273](https://github.com/rodri-oliveira-dev/adr-guard/issues/273) | Válidas como sinal de manutenção, mas não autorizam refactor transversal. Priorizar loaders/parsers acima de 25; tratar ocorrências entre 18 e 21 somente quando a extração melhorar um boundary verificável. |
| Core — exceções públicas | [#256](https://github.com/rodri-oliveira-dev/adr-guard/issues/256), [#260](https://github.com/rodri-oliveira-dev/adr-guard/issues/260), [#262](https://github.com/rodri-oliveira-dev/adr-guard/issues/262), [#270](https://github.com/rodri-oliveira-dev/adr-guard/issues/270), [#274](https://github.com/rodri-oliveira-dev/adr-guard/issues/274) | Detecção tecnicamente compatível com a regra, mas recomendação inválida para esta arquitetura. Não ampliar visibilidade; documentar a decisão no Sonar/GitHub ao encerrar ou aceitar. |

O último timestamp dos findings sincronizados é 20 de outubro de 2026, enquanto
`main` está em 3 de novembro. Desde a análise, somente cinco arquivos do recorte
foram alterados. Em `AdrCustomTemplateLoader` e `FitnessManifestLoader` foram
adicionados timeouts a regexes, sem remover os fluxos indicados; as demais
mudanças trataram resolução segura do executável Git e visibilidade para testes.
Portanto, nenhum finding do recorte pôde ser classificado como resolvido ou
obsoleto. Os valores numéricos exatos de complexidade devem ser considerados uma
baseline potencialmente desatualizada até uma nova análise do Sonar, mas a
inspeção do código atual confirma os problemas estruturais descritos acima.

## Evidências de publicação verificadas

As superfícies de distribuição foram verificadas de forma independente em 3 de
novembro de 2026. Versão no código-fonte, GitHub Release e publicação em um
registry não são tratadas como evidências equivalentes.

| Componente | Código-fonte | Evidência pública verificada | Estado documentado |
| --- | --- | --- | --- |
| CLI | `src/AdrGuard/AdrGuard.csproj` em `1.9.0` | NuGet.org e GitHub Release exata `v1.9.0` | `1.9.0` publicada |
| MCP | `src/AdrGuard.Mcp/AdrGuard.Mcp.csproj` em `1.0.1` | NuGet.org, MCP Registry e GitHub Release `mcp/v1.0.0` | `1.0.0` publicada; `1.0.1` candidata no source |
| VS Code | `extensions/vscode/package.json` em `1.1.0` | API do Visual Studio Marketplace | `1.1.0` publicada |
| GitHub Action | `action.yml` no repositório | tags remotas `v1` e `v1.9.0` no commit `8c6858a5bf2e2fc7dae36568ae06c60320cd71ac` | `v1.9.0` exata e alias móvel `v1` publicados |

A listagem da Action no Marketplace exibe metadados derivados das releases do
repositório e, isoladamente, não comprova a versão da Action. Da mesma forma, a
release do GitHub não comprova a presença do pacote no NuGet ou no MCP Registry.
A matriz de compatibilidade é a referência central para esse snapshot; READMEs
e páginas específicas apontam para ela em vez de reproduzir toda a matriz.

## Riscos e dependências

- **Contrato da CLI:** parsing manual codifica precedência, duplicidade,
  mensagens, stdout/stderr e exit codes. Uma biblioteca nova de parsing ou uma
  normalização genérica de erros está fora do escopo desta fase.
- **Determinismo:** findings, ordenação, fingerprints, JSON/SARIF e Markdown
  gerado devem permanecer byte a byte estáveis quando o contrato existente os
  define assim.
- **Segurança de entrada:** templates, Markdown, JSON, XML/MSBuild, paths,
  contexto explícito, Git e respostas de provider continuam não confiáveis e
  limitados. Extrações não podem mover validação para depois de I/O ou provider.
- **Compartilhamento do Core:** qualquer mudança no Core afeta CLI e MCP. Os
  testes de arquitetura, workspace MCP e smoke da CLI são dependências de
  conclusão, não validações opcionais.
- **Persistência:** criação offline e por IA compartilham alocação, mutex e
  promoção atômica. Não criar um segundo caminho de escrita durante a
  consolidação.
- **Review:** policy determinística deve ocorrer antes do provider quando
  aplicável; conteúdo de IA permanece consultivo e nunca estabelece aprovação.
- **Compatibilidade publicada:** as linhas de versão são independentes. Conforme
  a [matriz de compatibilidade](../compatibility.md), CLI `1.9.0`, MCP `1.0.0` e
  VS Code `1.1.0` estão publicados; MCP `1.0.1` existe apenas como candidata no
  código-fonte. A Action expõe a tag exata `v1.9.0` e o alias móvel `v1`. A
  consolidação não altera nenhum desses estados.
- **Documentação:** mudanças futuras de comportamento exigiriam atualização
  equivalente EN/pt-BR e portal. Refactors internos sem mudança observável devem
  apenas manter referências arquiteturais e notas de manutenção coerentes.

## Estratégia de testes

1. Antes de cada extração, adicionar apenas os testes de caracterização que
   faltarem para ordem de validação, texto exato, stream e exit code do método
   afetado.
2. Para parsing da CLI, usar testes tabulares por comando cobrindo opção única,
   duplicada, ausente, desconhecida, positional duplicado, aliases e combinação
   inválida. Comparar o resultado antes/depois, sem testar implementação privada.
3. Para geração/templates, preservar fixtures e snapshots EN/pt-BR, newline,
   placeholders, headings hostis, limites, concorrência, cancelamento e
   persistência sem overwrite.
4. Para review, executar os testes focados de policy, security, report,
   regression fixtures e integração do comando. Confirmar schema `1.0`, stdout
   JSON puro, disclosure em stderr, redaction, evidência e resultados
   inconclusivos.
5. Para mudanças no Core, executar primeiro a área unitária correspondente e,
   depois, testes de arquitetura, smoke CLI e testes MCP que consomem a regra.
6. Executar build Release e a suíte completa somente quando uma etapa tocar o
   Core compartilhado, contratos transversais ou no fechamento da fase. Todos os
   comandos de teste devem usar Microsoft.Testing.Platform/xUnit v3 conforme a
   skill `run-tests`; não usar filtros VSTest.

Na criação inicial deste plano não havia alteração executável, portanto a
validação daquele commit foi somente documental. As etapas de implementação
seguem a baseline .NET proporcional definida acima.

### Cobertura de caracterização adicionada na etapa 1

Os testes adicionados antes das extrações estruturais protegem os seguintes
comportamentos observáveis sem alterar a produção:

- parsing de `check`, `baseline`, `index`, `impact`, `drift`, `review` e `draft`
  para valor ausente, opção duplicada, positional duplicado, valor inválido e
  combinação dependente inválida, incluindo stderr exato, stdout vazio e exit
  code `2`;
- ajuda específica de `baseline`, `impact` e `drift` antes da validação de
  argumentos obrigatórios, com stdout e exit code `0`;
- precedência da validação de template de `draft` sobre a seleção obrigatória
  de provider e da exigência de policy file de `review` sobre qualquer acesso ao
  ADR alvo;
- aceitação de headings ATX e Setext como dados quando contidos em fences
  Markdown fechadas durante o rendering de templates, mantendo o ADR resultante
  validável.

A cobertura preexistente continua sendo a evidência para os contratos de
JSON/SARIF do `check`, JSON do `review`, templates Minimal/Extended/Custom,
persistência atômica, falhas de provider e os exit codes `1`, `3` e `4`. Não
foram adicionados testes MCP porque esta etapa não altera o Core compartilhado
nem revelou lacuna no contrato read-only do servidor.

### Implementação da etapa 2 — primeiro recorte

O primeiro recorte de implementação foi limitado aos dois métodos de maior
risco que podiam ser separados sem tocar geração, templates ou o fluxo interno
de review:

- `TryParseCheckArguments`, com complexidade cognitiva 100 no finding
  `AaFbXpZOnKzhk_hT4iLe`, foi removido de `CliApplication`. A interpretação foi
  decomposta em `CheckCommandArguments`, mantendo defaults, opções repetíveis e
  únicas, validações do Core, dependência entre `--changed` e `--base-ref` e a
  passagem tipada para `CheckCommand`.
- o parsing embutido em `RunBaseline`, com complexidade cognitiva 71 no finding
  `AaFbXpZOnKzhk_hT4iLi`, foi extraído para `BaselineCommandArguments`.
  `RunBaseline` preserva apenas ajuda, adaptação do erro de uso e dispatch para
  `BaselineCommand`; inclusive a aceitação histórica da última ocorrência de
  `--lifecycle-statuses`, `--filename-policy` e `--validation-profile` foi
  mantida.

Esses dois findings deixam de existir em `CliApplication`; a confirmação de
fechamento no Sonar depende de uma nova análise da branch. A branch ainda não
possui análise própria no SonarQube Cloud, e a consulta de referência em
`main` continua mostrando como abertos os outros seis findings do arquivo:
`TryParseDriftArguments` (46), `TryParseImpactArguments` (44),
`TryParseIndexArguments` (64), `TryParseReviewArguments` (77),
`TryParseDraftArguments` (61) e `RunDraft` (26). Eles permanecem para recortes
incrementais posteriores; este recorte não moveu complexidade para Core nem
alterou os command handlers existentes.

### Implementação da etapa 2 — geração, templates e reporting

O segundo recorte foi limitado a três componentes internos com cobertura de
regressão já estabelecida:

- `AdrCustomTemplateLoader.Load` foi reduzido a um pipeline explícito de
  resolução de path, leitura limitada, decode UTF-8, validação de placeholders,
  parsing e validação do preview. O parsing com estado foi isolado em
  `CustomTemplateParser`, que mantém a ordem histórica de título, Status,
  headings, conteúdo e fechamento de seção.
- `GeneratedAdrStructureGuard.ValidateField` passou a delegar o estado de
  fences a `MarkdownFenceTracker` e a reconhecer headings ATX/Setext por uma
  operação coesa. As regras CommonMark já implementadas, a distinção entre
  conteúdo de template e provider e as mensagens de rejeição foram mantidas.
- `AdrReviewReportBuilder.ResolveEvidence` foi separado nas etapas de rejeição
  de paths, validação de tokens, resolução por source ID/filename, ambiguidade e
  ordenação. A extração não amplia fontes aceitas nem infere path, ADR ID ou
  número de linha.

Com isso, os métodos apontados pelos findings sincronizados
`AaFbXpaBnKzhk_hT4iMw` ([#282](https://github.com/rodri-oliveira-dev/adr-guard/issues/282)),
`AaFbXpaBnKzhk_hT4iMx` ([#283](https://github.com/rodri-oliveira-dev/adr-guard/issues/283)),
`AaFbXpZ1nKzhk_hT4iMt` ([#284](https://github.com/rodri-oliveira-dev/adr-guard/issues/284)) e
`AaFbXpZnnKzhk_hT4iMl` ([#277](https://github.com/rodri-oliveira-dev/adr-guard/issues/277))
deixam de concentrar as condicionais reportadas. O GitHub ainda os apresenta
como abertos porque reflete a última sincronização de `main`; o fechamento no
Sonar depende de uma nova análise desta branch.

Permanecem fora deste recorte `AdrMarkdownRenderer.RenderTemplate`
([#252](https://github.com/rodri-oliveira-dev/adr-guard/issues/252)),
`AdrReviewTextRenderer.Render`
([#278](https://github.com/rodri-oliveira-dev/adr-guard/issues/278)) e a
decomposição de `ReviewCommand`. `AdrReviewPolicyLoader.Validate` (#276) continua
de baixa prioridade por representar validação sequencial apenas um ponto acima
do limite. Esses itens devem ser tratados incrementalmente, sem ampliar esta
rodada além dos três componentes selecionados.

A comparação antes/depois executou as mesmas classes e preservou exatamente as
contagens: `AdrCustomTemplateLoaderTests` 24/24,
`AdrMarkdownRendererTests` 29/29, `AdrReviewReportTests` 11/11 e
`AdrReviewRegressionFixtureTests` 14/14, sem falhas ou testes ignorados. O build
Release concluiu com zero avisos e zero erros.

### Implementação da etapa 2 — parsing de index, review e draft

O terceiro recorte permaneceu no adaptador da CLI e tratou quatro findings de
manutenibilidade HIGH do snapshot de `main`, sem tocar contratos públicos, Core
ou MCP:

- o parsing de `index` (finding `AaFbXpZPnKzhk_hT4iL5`, complexidade 64) foi
  movido para `IndexCommandArguments`, preservando defaults, validação das
  policies, opções únicas e o catálogo enriquecido;
- o parsing de `review` (finding `AaFbXpZPnKzhk_hT4iL6`, complexidade 77) foi
  movido para `ReviewCommandArguments`, mantendo arquivos de contexto
  repetíveis, dependência entre `--overwrite` e `--output`, policy, formato e
  target obrigatório;
- o parsing de `draft` (finding `AaFbXpZPnKzhk_hT4iMD`, complexidade 61) foi
  movido para `DraftCommandArguments`, mantendo a equivalência entre
  `--dry-run` e `--preview`, valores default e obrigatoriedade de title/context;
- a seleção e carga de template antes contida em `RunDraft` (finding
  `AaFbXpZPnKzhk_hT4iLx`, complexidade 26) foi separada em validação e resolução
  focadas. Templates continuam validados antes da construção ou invocação do
  provider.

A consulta autenticada ao SonarQube Cloud em 3 de novembro de 2026 retornou
zero findings para `refactor/phase-1-consolidation`, porque a branch ainda não
possui análise publicada, e 68 HIGH/BLOCKER abertos no snapshot antigo de
`main`. Portanto, os quatro métodos acima deixam de existir ou de concentrar a
complexidade reportada no código local, mas nenhum finding é declarado fechado
no Sonar sem uma nova análise.

A revisão de segurança não encontrou vulnerabilidade reproduzível neste
recorte. A validação continua anterior a I/O e providers; a carga de template
mantém resolução segura de path, limites, cancelamento e os erros existentes;
review continua sanitizando diagnósticos com sua security boundary; e nenhum
fluxo de processo externo, persistência atômica, credencial ou recurso MCP foi
alterado. As exceções do Core apontadas por `S3871` permanecem `internal`: abrir
uma API apenas para satisfazer o analisador continuaria incompatível com a ADR
0008.

Permanecem como próximos recortes da CLI os parsers de `drift` (46) e `impact`
(44). `AdrMarkdownRenderer.RenderTemplate`, `AdrReviewTextRenderer.Render` e
os seams selecionados do Core continuam adiados para etapas coesas; findings
próximos do limite e exceções internas não serão tratados apenas para reduzir
contadores.

A validação final deste recorte executou as classes de contrato da CLI, index,
draft, review, security boundary, template customizado, criação e persistência
atômica. Depois do build Release da solução com zero avisos e zero erros, a
suíte completa concluiu 744/744 testes, incluindo os testes de arquitetura e do
MCP. Uma regressão intermediária no estado default de `--culture` foi detectada
pelos testes de draft, corrigida e revalidada antes da baseline final.

## Escopo das próximas seis etapas

### 1. Congelar comportamento e medir seams

- Mapear cada método alvo para testes existentes e registrar lacunas.
- Adicionar caracterização somente para contratos ainda implícitos.
- Registrar tamanho, dependências e complexidade inicial por método; a métrica é
  evidência de apoio, não Definition of Done.

### 2. Separar o parsing da CLI por comando

- Extrair parsers internos tipados para `check`, `baseline`, `index`, `impact`,
  `drift`, `review` e `draft`.
- Compartilhar apenas primitivas comprovadamente idênticas, como leitura de
  opção com valor e detecção de duplicidade.
- Preservar argumentos, aliases, defaults, mensagens de ajuda e ordem de falha.

### 3. Reduzir `CliApplication` a roteamento e adaptação

- Delegar composição e execução aos command handlers já existentes ou a novos
  handlers internos de responsabilidade única.
- Manter na CLI a tradução para exit codes, stdout/stderr e apresentação.
- Não mover write workflows, providers ou detalhes de console para o Core.

### 4. Consolidar geração e templates

- Separar leitura limitada, parsing e validação de templates customizados.
- Tornar explícito o pipeline seleção -> validação -> rendering -> validação do
  ADR -> persistência atômica.
- Preservar diferenças intencionais entre draft legado e templates, snapshots,
  culturas e limites de segurança.

### 5. Decompor review e reporting

- Separar no `ReviewCommand` carregamento/validação, policy, preparação segura de
  contexto, execução do provider e emissão/persistência do relatório.
- Extrair resolução de evidência e seções do renderer como operações puras.
- Manter security boundary, schema, disclosure, redaction, human ownership e
  ordem de falha inalterados.

### 6. Tratar seams selecionados do Core e reconciliar Sonar

- Refatorar um domínio por vez, começando por loaders/parsers com complexidade
  acima de 25 e testes fortes.
- Preservar o grafo de projetos, tipos internos, determinismo e consumo comum por
  CLI/MCP.
- Executar nova análise Sonar, fechar apenas findings comprovadamente resolvidos
  e justificar os `S3871` que conflitam com a arquitetura.
- Rodar a validação final proporcional e atualizar este plano com resultados,
  desvios e trabalho explicitamente adiado.

## Critérios objetivos de conclusão

A Fase 1 estará concluída somente quando todos os itens abaixo tiverem evidência:

- `CliApplication` contiver apenas ajuda, roteamento de alto nível e adaptação de
  boundary; nenhum parser de comando permanecer na classe.
- Cada comando afetado preservar argumentos, defaults, ordem de validação,
  mensagens estáveis, stdout/stderr e exit codes `0` a `4`.
- Não houver mudança nos schemas JSON/SARIF/review, form contracts, fingerprints,
  templates aprovados, versões ou APIs públicas.
- Geração offline e por IA continuarem usando a mesma alocação e persistência
  segura, sem overwrite e com cancelamento coberto.
- Review continuar read-only e consultivo, com policy determinística, evidência,
  redaction e `unknown` cobertos por regressão.
- O grafo `CLI -> Core` e `MCP -> Core` permanecer acíclico e sem referências
  entre executáveis; Core continuar interno e não empacotável.
- Cada finding Sonar deste recorte estiver em uma de três situações evidenciadas:
  resolvido após nova análise, aceito/false positive com justificativa
  arquitetural, ou adiado com issue e dependência explícitas.
- Testes focados de cada etapa, build Release e baseline final aplicável tiverem
  sido executados com sucesso e registrados com os comandos exatos.
- Documentação pública EN/pt-BR permanecer inalterada quando não houver mudança
  observável; qualquer exceção tiver atualização equivalente e validação do
  portal.
- O diff final não contiver funcionalidade, mudança de contrato ou versão fora
  do escopo aprovado.

## Fechamento da branch

O fechamento iniciado em 3 e concluído em 4 de novembro de 2026 revisou os
seis commits da branch
`refactor/phase-1-consolidation` sobre `main` em
`4cf208f3ddf9fb14944b68db18aa3548ff4553b6`. A referência remota de `main` foi
atualizada por HTTPS antes da comparação e permaneceu nesse commit. O recorte
está apto a revisão em Pull Request, mas não satisfaz literalmente todos os
critérios ambiciosos do plano original; os desvios abaixo permanecem
explicitamente fora do ajuste final para evitar uma refatoração tardia e sem a
mesma caracterização dos recortes concluídos.

### Resultado por componente

- CLI: os parsers tipados de `check`, `baseline`, `index`, `review` e `draft`
  foram extraídos, e a seleção/carga de template de `draft` foi separada da
  execução. Ajuda, defaults, precedência de validação, streams e exit codes
  permaneceram no adaptador.
- Geração e templates: leitura limitada, decode, parsing e validação de
  template customizado formam etapas explícitas; o scanner estrutural mantém o
  estado de fences e os mesmos limites de headings.
- Review: a resolução de evidência foi decomposta em operações puras sem
  aceitar novas fontes, paths ou inferências. O schema `1.0`, o caráter
  consultivo e a security boundary não foram alterados.
- Documentação e distribuição: o estado realmente publicado de CLI, MCP,
  extensão e Action foi reconciliado em inglês e pt-BR. Nenhuma versão,
  release, tag ou pacote publicado foi alterado.
- Core, MCP, extensão e Action: não houve mudança de regra compartilhada nem
  de código de runtime nessas superfícies. Elas foram validadas como
  consumidoras dos contratos preservados.

### Compatibilidade confirmada

- Os comandos e argumentos existentes, os exit codes `0` a `4`, mensagens de
  uso e ordem de falha permaneceram cobertos pela suíte completa e pelos testes
  de caracterização adicionados.
- JSON, SARIF, fingerprints, schema de review, sete ferramentas MCP e contratos
  estritos consumidos pela extensão permaneceram sem alteração.
- Os templates Minimal, Extended e Custom em en-US/pt-BR foram exercitados a
  partir do pacote instalado; renderização, `check`, `index`, ausência de
  overwrite e determinismo permaneceram compatíveis.
- O pacote da CLI continuou contendo Core e excluindo MCP; o pacote MCP
  continuou contendo Core e excluindo a CLI. As versões permaneceram CLI
  `1.9.0`, MCP source `1.0.1` e extensão `1.1.0`.
- Os testes novos usam somente argumentos e strings determinísticos em memória,
  `StringWriter` e parsers locais. Eles não dependem de rede, credenciais,
  relógio, arquivos do usuário ou ordem de execução.

### Evidências de validação final

Todos os comandos abaixo foram executados da raiz do repositório, salvo quando
o diretório é indicado:

- `dotnet restore AdrGuard.slnx`: concluído com sucesso.
- `dotnet build AdrGuard.slnx --configuration Release --no-restore`: concluído
  com zero avisos e zero erros.
- `dotnet test --solution AdrGuard.slnx --configuration Release --no-build`:
  747 aprovados, zero falhos e zero ignorados, incluindo a suíte MCP.
- CLI e MCP foram empacotados com
  `dotnet pack --configuration Release --no-build`, instalados de fontes NuGet
  locais isoladas e inspecionados. As
  regressões do pacote instalado para templates/CLI e documentação de review
  passaram; o pacote MCP instalado concluiu 22 testes, todos aprovados.
- Em `extensions/vscode`, `npm ci --ignore-scripts`, `npm run lint`,
  `npm run typecheck`, `npm run build`, `npm run test:readme-images`,
  `npm run test:unit` e `npm run test:integration` passaram. A suíte unitária
  teve 115 testes
  aprovados, zero falhos e um ignorado por condição de plataforma; as suítes
  extension-host tiveram 17 testes aprovados.
- Em `website`, `npm ci --ignore-scripts`, `npm run sync:docs`,
  `npm run validate` e `npm run test:spelling` passaram. Foram sincronizados 78
  documentos, executados seis testes, construídas 118 páginas, verificadas 46
  rotas bilíngues, links em 118 arquivos HTML e SEO em 116 páginas; Astro e
  spelling terminaram sem findings.
- `scripts/github-action-docs-test.sh`,
  `scripts/github-action-impact-test.sh` e
  `scripts/github-action-report-test.sh` passaram usando Git Bash explícito.
- O ADR Guard empacotado validou os oito ADRs de `docs/adr` sem findings.
- A consulta Sonar anterior ao PR retornou zero issues, mas o quality gate
  respondeu `NOT_COMPUTED` sem condições; isso confirmava ausência de análise,
  não ausência comprovada de findings. A primeira análise do PR executou de
  fato e falhou com 76% de cobertura no código novo, 6% de duplicação e quatro
  findings diretamente relacionados às extrações desta fase. Os ajustes finais
  consolidaram a leitura comum de argumentos, removeram o sombreamento de
  `Parse`, aproximaram os helpers de fence do estado que os consome e
  centralizaram a mensagem de uso repetida. Três regressões determinísticas de
  contrato foram acrescentadas; a reanálise do PR é o gate final desses
  ajustes.
- `git diff --check` foi executado com sucesso antes do commit de fechamento.

Os testes Linux-específicos `github-action-smoke-test.sh` e
`github-action-review-test.sh` não puderam ser concluídos integralmente no host
Windows: o primeiro requer mounts Docker com paths POSIX reais, e o segundo
isola `PATH` pressupondo utilitários do runner Linux. O teste de review chegou
ao caso negativo de Python, mas o ambiente Git Bash falhou antes da asserção.
Eles não foram registrados como aprovados localmente. O workflow
`GitHub Action contract` concluiu com sucesso no CI Linux do PR, cobrindo a
execução no ambiente para o qual esses scripts foram escritos.

### Findings tratados e pendências conscientes

Os métodos associados aos findings de complexidade de parsing de `check`,
`baseline`, `index`, `review` e `draft`, ao fluxo de template de `draft`, ao
loader/parser de template customizado, ao scanner estrutural e à resolução de
evidência deixaram de concentrar as condicionais apontadas. O estado definitivo
dos findings originais depende da reconciliação histórica no SonarQube;
ausência de análise não foi interpretada como fechamento. Os quatro findings e
as duas métricas impeditivas produzidos pela primeira análise do PR foram
tratados diretamente e permanecem sujeitos à reanálise obrigatória do mesmo
PR.

Permanecem pendentes, sem regressão conhecida e sem ampliação deste PR:

- extrair os parsers de `impact` e `drift`, que ainda estão em
  `CliApplication`; por isso a classe ainda não atende ao critério literal de
  conter somente roteamento e adaptação;
- decompor `ReviewCommand` e `AdrReviewTextRenderer`, e avaliar
  `AdrReviewPolicyLoader.Validate` somente por um seam coeso;
- tratar `AdrMarkdownRenderer.RenderTemplate` e os loaders/parsers selecionados
  do Core um domínio por vez;
- reconciliar os findings históricos comprovadamente resolvidos e registrar a
  justificativa arquitetural dos `S3871` sem tornar exceções internas públicas.

Esses itens são dívida de consolidação remanescente, não funcionalidades da
Fase 2, e devem conservar o mesmo requisito de caracterização antes de qualquer
extração. A primeira análise Sonar do PR encontrou regressões de qualidade
diretamente causadas pelas extrações da branch. Elas foram corrigidas sem
alterar contratos públicos; nenhuma funcionalidade da Fase 2 foi incorporada.
