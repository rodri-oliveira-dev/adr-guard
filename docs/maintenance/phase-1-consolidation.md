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
  a [matriz de compatibilidade](../compatibility.md), o source baseline contém
  CLI `1.9.0` e MCP `1.0.1` candidatos, enquanto o snapshot documental de 20 de
  outubro registra CLI `1.8.0` e MCP `1.0.0` como publicados e VS Code `1.1.0`
  como publicado. A consolidação não altera nenhum desses estados.
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

Nesta entrega de planejamento não há alteração executável, portanto não é
necessário rodar testes .NET. A validação proporcional é revisão do diff,
`git diff --check` e confirmação de que somente este documento foi adicionado.

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
