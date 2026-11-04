# Fase 2 de Developer Experience e integrações

## Objetivo e limites

Este plano registra o estado da `main` no commit
`d59b958830daecd24f49e1610945e74f69cf3888`, consultado em 4 de novembro de
2026. Esse commit incorporou a PR #292 e concluiu a Fase 1 sem entregar
funcionalidades da Fase 2.

O objetivo desta fase é validar a primeira experiência de uso em projetos
externos, corrigir apenas fricções comprovadas no onboarding e completar a
integração de `drift` com a GitHub Action sem criar um segundo motor de regras.
O trabalho deve permanecer dividido em entregas pequenas e verificáveis:

1. evidência de adoção e onboarding da issue #209;
2. ajustes mínimos de ajuda ou tratamento de comandos da CLI, condicionados a
   defeitos reproduzíveis;
3. modo `drift` explícito na Action, adaptando o contrato já implementado na
   CLI/Core;
4. testes de contrato e documentação pública bilíngue das superfícies alteradas.

Não fazem parte desta fase uma reformulação geral da CLI ou do portal, novos
assistentes, telemetria, publicação de releases, MCP com escrita, aprovação
automática de ADRs ou reabertura do roadmap #169. As extrações pendentes dos
parsers de `impact` e `drift` em `CliApplication` são dívida de consolidação da
Fase 1, não requisito funcional desta fase; qualquer refactor correspondente
deve ser isolado e preservar comportamento antes de uma mudança de produto.

## Estado atual verificado

### Onboarding

- `README.md` e `README.pt-BR.md` já oferecem um quick start offline com
  `init`, `new`, edição humana, `check` e `index`.
- `docs/guides/getting-started*.md` e `docs/guides/team-adoption*.md` já cobrem
  criação de um ADR `Proposed`, recuperação básica e separação entre validação
  estrutural e aprovação arquitetural.
- Há jornadas próprias para VS Code, MCP e compatibilidade publicada; a issue
  #209 exige melhorar essas páginas existentes em vez de criar tutoriais ou
  wizards paralelos.
- Ainda não há evidência externa suficiente para afirmar que a jornada é
  autônoma. A lacuna real é executar e registrar os cenários da issue #209 em
  projetos não relacionados ao ADR Guard, com versões publicadas e limitações
  identificadas separadamente do estado da `main`.

### CLI e tratamento de comandos

- `CliApplication` roteia `init`, `check`, `baseline`, `index`, `new`, `edit`,
  `draft`, `review`, `impact` e `drift`, oferece ajuda global/específica e
  preserva os exit codes públicos de `0` a `4`.
- Argumentos inválidos já retornam exit code `2`, escrevem em `stderr` e têm
  mensagens exatas caracterizadas em `CliArgumentContractTests`;
  `CliApplicationTests` cobre ajuda, versão, uso offline e comando desconhecido.
- Os comandos extraídos na Fase 1 usam modelos de argumentos e
  `CliCommandArgumentReader`. `impact` e `drift` ainda fazem parsing dentro de
  `CliApplication`, mas isso, isoladamente, não demonstra uma falha de DX.
- A issue #209 autoriza mudanças pequenas de CLI somente depois de um defeito
  reproduzível. Sugestões de comando, novas mensagens, alteração da ajuda ou
  defaults não devem ser implementados apenas por preferência, pois ordem de
  validação, texto, stream e exit code são contratos observáveis.

### GitHub Action e `drift`

- A Action composta suporta `check`, `index`, `review` opt-in e `impact`
  opt-in. O wrapper valida paths dentro de `GITHUB_WORKSPACE`, rejeita escapes
  por symlink, fixa a imagem, usa argumentos discretos e executa containers com
  filesystem somente leitura, capabilities removidas e rede desabilitada nos
  fluxos determinísticos.
- A CLI já oferece `drift` com regras e evidências versionadas, saída text/JSON,
  baseline explícito, política `advisory` por padrão e `enforce` opt-in. Resultado
  `unknown`/inconclusivo retorna `3`; somente uma violação nova, determinística,
  com gate habilitado retorna `4` em enforcement.
- Guias EN/pt-BR já documentam a execução direta da CLI no CI e afirmam
  corretamente que `drift` não é um modo da Action composta atual.
- A lacuna de integração é real: `action.yml` e `scripts/github-action.sh`
  rejeitam `drift`, não expõem paths de regras/evidências/baseline e não possuem
  summary seguro específico para o relatório JSON de drift.

### Testes e documentação disponíveis

- `DriftCommandIntegrationTests` protege determinismo, advisory/enforce,
  baseline, resultado inconclusivo, erros controlados e opções inválidas.
- `CliArgumentContractTests` protege ajuda e diagnósticos de uso da CLI.
- Os scripts `github-action-{contract,smoke,security,impact,report,docs}-test.sh`
  fornecem padrões para compatibilidade, hardening, exit codes, paths hostis,
  summaries e paridade documental.
- A documentação de Action, drift, onboarding, compatibilidade, exemplos e
  portal já tem pares EN/pt-BR. As páginas existentes devem ser atualizadas; não
  deve ser criada uma segunda referência pública para o mesmo contrato.

## Contratos a preservar

### Produto e decisão humana

- Um ADR novo permanece `Proposed`; nenhuma validação, Action, IA ou evidência
  de fitness aprova uma decisão ou muda seu lifecycle automaticamente.
- `check` valida regras documentais determinísticas. `drift` verifica somente
  regras de fitness explícitas e evidência limitada; ausência ou ambiguidade
  permanece `unknown`, nunca conformidade inferida.
- O caminho principal de onboarding continua offline e sem provider. `draft`,
  `review`, MCP e integrações com agentes permanecem opcionais e claramente
  separados.

### CLI e Core

- Preservar nomes, argumentos, aliases, defaults, precedência de validação,
  mensagens estáveis, uso de `stdout`/`stderr` e exit codes: `0` sucesso, `1`
  findings de validação, `2` uso inválido, `3` falha operacional ou evidência
  inconclusiva e `4` falha de policy determinística.
- Preservar schemas, versões, nomes e ordenação dos relatórios, fingerprints,
  semântica de baseline e política de drift.
- Manter parsing, policy e análise de drift em `AdrGuard.Core`; CLI e Action são
  adapters. Nenhuma regra pode ser reimplementada em Bash, Python ou YAML.

### GitHub Action

- Inputs novos devem ser aditivos. Defaults e comportamento de `check`,
  `index`, `review` e `impact` permanecem inalterados.
- O modo `drift` deve exigir um manifest de regras relativo ao workspace e pode
  aceitar evidências e baseline explícitos, todos canonicalizados e confinados
  ao checkout. Paths absolutos, traversal e escapes por symlink devem falhar
  antes da execução do container.
- A Action deve comparar um baseline existente, mas não criar nem atualizar
  baseline. Escritas deliberadas com `--write-baseline`/`--update-baseline`
  permanecem um fluxo direto da CLI e sujeito a revisão humana.
- `drift` deve executar sem rede, secrets ou credenciais e com o workspace
  somente leitura. A imagem selecionada deve ser sondada por suporte a
  `drift --help`, sem fallback silencioso para `latest`.
- Saída não confiável deve continuar incapaz de emitir workflow commands ou
  HTML ativo. Reporting é best effort e nunca substitui o exit code original;
  falhas Docker `125` a `127` continuam normalizadas para `3`.
- Não reutilizar silenciosamente inputs cujo significado público seja exclusivo
  de `review`. A decisão entre tornar `policy` comum aos dois comandos ou
  introduzir um input específico de drift deve ser registrada antes da
  implementação e refletida igualmente no manifest, wrapper, testes e guias.

### Publicação e documentação

- Distinguir versão existente no source, artefato publicado e alias móvel. Um
  merge não comprova disponibilidade no NuGet, GHCR, Marketplace ou MCP
  Registry.
- Mudanças públicas exigem paridade EN/pt-BR no README e nas páginas de
  onboarding, Action, drift, compatibilidade e portal que forem afetadas.
  Páginas geradas do portal não devem ser editadas manualmente.

## Atividades

### 1. Validar onboarding com evidência

1. Revalidar o inventário de CLI, VS Code, MCP e Action publicados, registrando
   versão, canal, data, SHA e ambiente sem inferir publicação a partir da
   `main`.
2. Preparar o roteiro seguro da issue #209 e executá-lo em dois projetos
   autorizados que não sejam o ADR Guard: instalação, `init`, `new`, edição
   significativa, `check`, falha e recuperação, `index` e localização de uma
   decisão existente.
3. Testar VS Code contra o artefato realmente instalado e MCP somente quando
   houver cliente compatível. Registrar bloqueio ou não aplicabilidade em vez
   de substituir evidência pública por funcionalidade source-only.
4. Observar três participantes iniciantes com consentimento e anonimização. Se
   isso não for possível, registrar um walkthrough técnico reproduzível e a
   limitação, sem chamá-lo de validação independente.
5. Classificar findings por severidade, repetição, impacto e força da evidência;
   separar defeito, documentação, instalação/ambiente e solicitação de feature.
6. Atualizar o material existente somente para blockers comprovados e repetir
   os cenários, registrando antes/depois e incertezas remanescentes.

### 2. Tratar lacunas comprovadas da CLI

1. Converter cada fricção de CLI selecionada em reprodução mínima que capture
   comando, ambiente, streams e exit code esperado/atual.
2. Adicionar caracterização focada antes de alterar ajuda, diagnóstico ou
   dispatch. Preservar os casos não relacionados e o caminho offline.
3. Fazer a menor mudança que resolva a reprodução. Mudanças mais amplas de
   parser/roteamento ou novas funcionalidades devem virar issue própria.
4. Atualizar a referência da CLI e onboarding nos dois idiomas apenas quando o
   comportamento observável mudar.

#### Contrato planejado de `adr-guard doctor`

Esta etapa define o contrato antes da implementação. O comando será um
diagnóstico local, determinístico, somente leitura e independente de providers:

```text
adr-guard doctor [repository]
adr-guard doctor --help
```

`repository` será um único diretório opcional e usará o diretório de invocação
por padrão. O argumento é necessário para que pessoas, a extensão VS Code e
clientes de automação possam diagnosticar explicitamente o mesmo workspace. A
primeira versão não terá `--format`, JSON, correção automática, descoberta de
rede, instalação de ferramentas nem outras opções. `-h` e `--help` serão os
únicos argumentos alternativos ao diretório.

##### Encaixe na arquitetura existente

- `CliApplication` deverá incluir `doctor` no help e no dispatch, delegando a
  execução a um handler `DoctorCommand`, como os demais comandos da pasta
  `Cli`. O handler adaptará resultados para texto e exit code; regras de path,
  configuração e identificação reutilizáveis permanecerão no Core.
- `doctor` deverá ser roteado, como `init`, antes de
  `AdrGuardConfigurationLoader.Load` e `ConfigurationArguments.Apply`. O
  handler carregará a configuração explicitamente para poder relatar uma
  `.adrguard.yml` inválida em vez de ser interrompido pelo preflight global.
- O carregamento manterá o parser estrito, o limite de 64 KiB, a rejeição de
  symlink/reparse point e a normalização confinada de `RepositoryPath` já
  existentes. O comando não criará um segundo parser de configuração.
- A versão virá da mesma identidade usada por `adr-guard --version`. A
  identificação do repositório reconhecerá o marcador local `.git` como
  diretório ou arquivo de worktree e poderá reutilizar a resolução de raiz
  existente, sem consultar remotes ou alterar o Git.

##### Checks, severidade e recuperação

Os checks aparecerão sempre nesta ordem. Cada linha começará com `OK`,
`WARNING` ou `ERROR`, seguida do nome estável do check, uma mensagem curta e,
quando houver ação segura, `Fix: <correção sugerida>`.

| Check | `OK` | `WARNING` | `ERROR` e correção |
| --- | --- | --- | --- |
| CLI | A versão informacional atual foi obtida. | Não aplicável. | Falha inesperada ao obter a própria identidade impede concluir o diagnóstico. |
| Repository | A raiz selecionada existe, pode ser lida e possui marcador `.git`. | A raiz possui `.adrguard.yml`, mas não marcador Git; fluxos básicos continuam disponíveis, enquanto recursos baseados em Git não. | Diretório inexistente/inacessível ou nenhuma raiz ADR Guard/Git identificável. `Fix`: executar em um repositório existente ou informar seu diretório. |
| Configuration | `.adrguard.yml` schema 1 foi carregada e seus paths são seguros. | Arquivo ausente; os defaults legados continuam válidos. `Fix`: revisar `adr-guard init --dry-run` quando configuração compartilhada for desejada. | Arquivo ilegível, grande demais, malformado, com schema/propriedade/valor incompatível ou path inseguro. `Fix`: corrigir a causa limitada informada na `.adrguard.yml`; o comando nunca a regrava. |
| ADR directory | O diretório efetivo existe, é legível, está contido na raiz e não atravessa symlink/reparse point. | Sem configuração, o escopo legado é a raiz selecionada; informar que nenhum diretório dedicado foi configurado. | Diretório configurado ausente, inacessível ou inseguro. `Fix`: criar/corrigir o diretório dentro da raiz ou ajustar `adr-directory`. |
| .NET runtime | O runtime que já hospeda a CLI é identificado localmente. | Não aplicável na primeira versão. | Falha inesperada de introspecção impede concluir o diagnóstico. |
| Git | Um executável Git local responde a uma consulta limitada de versão. | Git ausente, incompatível, excedeu o tempo/saída ou não pôde ser iniciado. `Fix`: instalar/corrigir Git somente se forem usados `--changed`, `impact`, `drift` ou comparações Git. | Não aplicável: Git é opcional para `init`, `new`, `check` completo e `index`. |

Um check dependente que não puder ser executado usará `WARNING`, dirá qual
pré-requisito o bloqueou e não inferirá sucesso. Por exemplo, configuração
inválida produz `ERROR Configuration` e `WARNING ADR directory: not checked
because configuration is invalid`. A ausência de providers, chaves, extensão
VS Code, servidor MCP ou acesso à rede não será inspecionada: nenhum deles é
pré-requisito do fluxo offline básico.

##### Streams e exit codes

- Um relatório concluído é gravado no `stdout`, termina com contagens de
  `OK`, `WARNING` e `ERROR` e retorna `0` quando não há `ERROR`, inclusive com
  um ou mais warnings.
- Um relatório concluído com pelo menos um `ERROR` retorna
  `ExitCodes.OperationalError` (`3`). Estado local inválido não será convertido
  em sucesso, validação ADR (`1`) nem policy (`4`).
- Argumento extra, opção desconhecida, help combinado com outro argumento ou
  mais de um diretório retorna `ExitCodes.UsageError` (`2`) no `stderr`, com a
  mesma forma `Invalid arguments` e hint de help dos comandos atuais.
- Falha que impeça produzir um relatório confiável também retorna `3` no
  `stderr`. Resultados parciais não serão apresentados como conclusivos.

##### Exemplos textuais

Repositório pronto, com Git opcional indisponível:

```text
ADR Guard doctor
OK CLI: 1.9.0
OK Repository: detected at the selected root (Git worktree).
OK Configuration: .adrguard.yml schema 1 loaded.
OK ADR directory: docs/adr exists and is readable.
OK .NET runtime: 10.0.x
WARNING Git: executable not available. Fix: install Git only for Git-based commands.
Summary: 5 OK, 1 WARNING, 0 ERROR
```

Execução fora de um repositório retorna `3`:

```text
ADR Guard doctor
OK CLI: 1.9.0
ERROR Repository: no ADR Guard or Git repository was detected. Fix: run from an existing repository or pass its directory.
WARNING Configuration: not checked because no repository root was identified.
WARNING ADR directory: not checked because no repository root was identified.
OK .NET runtime: 10.0.x
OK Git: 2.x available.
Summary: 3 OK, 2 WARNING, 1 ERROR
```

Configuração inválida também retorna `3`, sem imprimir o path absoluto:

```text
ERROR Configuration: .adrguard.yml line 2 has unknown property 'adrs'. Fix: use supported schema 1 top-level scalar properties.
WARNING ADR directory: not checked because configuration is invalid.
```

Os exemplos fixam a forma e a semântica, não os valores de versão. A
implementação deverá preservar a ordem e pontuação verificadas pelos testes,
sem transformar o texto em novo contrato JSON implícito.

##### Limites de segurança

- Não acessar rede, remotes, providers, package feeds, variáveis de credencial,
  conteúdo de ADRs ou arquivos fora da raiz selecionada; não executar hooks,
  instalar ferramentas, criar diretórios nem gravar arquivos.
- Canonicalizar a raiz antes das leituras, confinar paths configurados e rejeitar
  escapes e symlinks/reparse points conforme as regras existentes.
- Se Git for consultado, usar apenas argumentos literais com shell desabilitado,
  stdin fechado, timeout máximo de 5 segundos e no máximo 4 KiB combinados de
  stdout/stderr. Não encaminhar mensagens arbitrárias do Git sem sanitização.
- Nunca imprimir paths absolutos, diretórios de perfil, remotes, nomes de usuário,
  ambiente ou credenciais. Exibir somente `.`, `.adrguard.yml` e paths relativos
  já validados dentro da raiz. Remover controles, normalizar quebras de linha e
  limitar cada mensagem antes de renderizar dados locais não confiáveis.
- Não testar permissão de escrita criando arquivos temporários. O diagnóstico
  afirma apenas existência e leitura que realmente conseguiu comprovar.

##### Casos de teste para a etapa de implementação

1. Help global lista `doctor`; `doctor --help` retorna `0` sem carregar uma
   configuração inválida; argumentos desconhecidos/duplicados retornam `2` com
   diagnóstico exato no `stderr`.
2. Repositório Git saudável, worktree com `.git` em arquivo e raiz ADR Guard sem
   Git produzem estados determinísticos, sem revelar path absoluto.
3. Execução fora de repositório, raiz inexistente e raiz inacessível cobrem a
   diferença entre `ERROR` relatável e falha que impede concluir o relatório.
4. Configuração ausente, válida, malformada, grande demais, ilegível, em
   symlink, com schema incompatível e com `adr-directory` inseguro preserva os
   limites e diagnósticos do loader atual.
5. Diretório ADR existente, ausente, inacessível e atravessando reparse point
   cobre `OK`/`ERROR` sem abrir ou interpretar seus arquivos Markdown.
6. Git disponível, ausente, com exit não zero, timeout e saída excessiva gera
   apenas `OK`/`WARNING`; fakes locais comprovam que nenhum comando de rede,
   hook ou escrita foi executado.
7. Relatórios somente com `OK`, com warnings e com errors validam ordem,
   resumo, separação stdout/stderr e exits `0`/`3` sem usar `1` ou `4`.
8. Paths e mensagens hostis com controle, quebra de linha, delimitador de
   workflow e texto semelhante a segredo são limitados/sanitizados e nunca
   ecoam valores de ambiente.

Esses testes serão adicionados somente junto da implementação. Esta etapa não
adiciona stubs, testes pendentes nem altera o build.

### 3. Integrar `drift` à GitHub Action

1. Definir o contrato aditivo de inputs para manifest de regras, lista limitada
   de evidências, baseline opcional e policy, incluindo limites, exclusividades
   e nomes sem ambiguidade com `review`.
2. Estender `action.yml` e o wrapper para validar todos os inputs antes do
   Docker, sondar a versão selecionada, montar o workspace somente leitura e
   invocar a CLI com uma lista de argumentos literal.
3. Solicitar JSON da CLI e criar reporting específico e limitado para summary,
   sem interpretar o resultado em Bash/Python como uma nova decisão de policy.
4. Preservar exatamente os exits `0`, `2`, `3` e `4`, inclusive quando o
   relatório for indisponível ou o renderer auxiliar falhar.
5. Adicionar fixture e teste focado para sucesso advisory, violação enforce,
   `unknown`, baseline existente, múltiplas evidências, versão sem suporte,
   paths hostis, symlink escape, ausência de secrets, hardening do container e
   conteúdo hostil no relatório.
6. Atualizar o exemplo de workflow, guias da Action e drift, README, matriz de
   compatibilidade e fontes do portal em EN/pt-BR. Declarar a integração como
   development-only até que o canal público correspondente seja verificado.

### 4. Validar e entregar por recortes

1. Manter onboarding, ajustes de CLI e integração da Action em PRs rastreáveis
   quando puderem ser revisados independentemente.
2. Executar primeiro os testes de classe/scripts da superfície alterada; ampliar
   para os gates de CLI, Action, documentação e portal proporcionais ao diff.
3. Revisar o diff completo e executar `git diff --check`. Registrar comandos
   realmente executados, resultados e checks não executados.
4. Não publicar pacote, imagem, extensão, tag ou alias como efeito colateral da
   fase. Publicação coordenada exige autorização e validação próprias.

## Execução técnica da issue #209

Esta seção registra o walkthrough técnico conduzido em 4 de novembro de 2026.
Ele valida a reprodução dos cenários e os artefatos públicos disponíveis, mas
não substitui testes independentes de usabilidade. Nenhum participante externo
foi recrutado nesta execução; por isso não há tempos, intervenções ou métricas
de adoção a reportar.

### Ambiente e inventário público

O teste foi executado no Windows 11 Pro 64 bits `10.0.26300`, com .NET SDK
`10.0.403`, Git `2.56.0.windows.2` e VS Code `1.142.0`. O HEAD da branch era
`85da1379befbc181017a9fbd0e0c6849d90e86e2`.

| Superfície | Artefato efetivamente verificado | Publicação e identidade | Diferença em relação ao source testado |
| --- | --- | --- | --- |
| CLI | NuGet `RodriOliveira.AdrGuard` `1.9.0`, instalado com `dotnet tool install --tool-path` | Publicado em 20 de outubro de 2026; `adr-guard --version` retornou `1.9.0+8c6858a5bf2e2fc7dae36568ae06c60320cd71ac` | O source ainda declara `1.9.0`, mas a branch está oito commits e o plano atual à frente do SHA publicado. Todos os cenários de CLI usaram o executável publicado. |
| VS Code | Marketplace `rodrioliveira.adr-guard` `1.1.0`, instalado com sucesso em um `--extensions-dir` temporário | Publicado originalmente em 8 de outubro e atualizado em 20 de outubro de 2026, segundo a API pública do Marketplace | `extensions/vscode/package.json` também declara `1.1.0`. O perfil normal da máquina ainda tinha `0.9.0`; ele não foi alterado. O artefato isolado foi validado quanto à instalação, não por interação visual. |
| MCP | NuGet `RodriOliveira.AdrGuard.Mcp` `1.0.0`, instalado com `dotnet tool install --tool-path` | Publicado em 17 de outubro de 2026, SHA `19f2af215287ecb90cec510d74d0a43c73dec700`; o Registry manifest continua em `1.0.0` | O projeto source declara o candidato não publicado `1.0.1`. O teste de protocolo apontou explicitamente para o executável público `1.0.0`. |
| GitHub Action | Tags públicas `v1` e `v1.9.0` | Ambas resolviam para `8c6858a5bf2e2fc7dae36568ae06c60320cd71ac`; release publicada em 20 de outubro de 2026 | `action.yml` e `scripts/github-action.sh` não diferiam desse SHA. O contrato público aceitava `check`, `index`, `review` e `impact`, mas ainda não `drift`. |

As identidades NuGet foram confirmadas pelo índice e catálogo públicos, além da
instalação. Tags foram resolvidas por HTTPS sem credenciais. Nenhum download,
estrela ou execução de CI foi interpretado como evidência de adoção.

### Roteiro reproduzível

Use um diretório temporário novo para cada cenário e mantenha o pacote fora dos
repositórios sob teste. Em PowerShell, o bootstrap usado foi equivalente a:

```powershell
$root = Join-Path ([System.IO.Path]::GetTempPath()) 'adr-guard-onboarding-209'
dotnet tool install --tool-path (Join-Path $root tools) `
  RodriOliveira.AdrGuard --version 1.9.0 `
  --source https://api.nuget.org/v3/index.json --no-cache
$cli = Join-Path $root 'tools/adr-guard.exe'
& $cli --version
```

O contrato `edit --form` não é um editor interativo. O roteiro enviou
`{"schemaVersion":"1.0","operation":"load"}` por `stdin`, preservou o
`fingerprint` e os IDs retornados e enviou uma operação `save` com título e
valores completos. Isso reproduz o mesmo limite usado pelo adapter do VS Code
sem depender de UI ou de IA.

#### Cenário A: projeto começando a usar ADRs

1. Criar um repositório Git temporário vazio e executar
   `adr-guard init . --adr-directory docs/adr --template minimal`.
2. Executar `adr-guard new docs/adr --title "Use PostgreSQL for order persistence"`.
3. Carregar e salvar o ADR com `edit --form`, mantendo `Proposed` e registrando
   contexto, decisão e consequências sobre consistência transacional, migração
   em lotes e responsabilidade operacional.
4. Criar e editar um segundo ADR, `Choose an event broker`, com contexto sobre
   entrega durável e replay, decisão por RabbitMQ e consequências operacionais.
5. Remover deliberadamente o conteúdo da seção `Decision` do segundo ADR e
   executar `adr-guard check docs/adr --validation-profile strict`.
6. Confirmar exit `1` e o diagnóstico `ADR005`; restaurar a decisão no editor
   de texto e repetir o `check`.
7. Executar `adr-guard index docs/adr --validation-profile strict` e conferir
   os dois links e o estado `Proposed` no índice.

Resultado: `init`, os dois `new`, o primeiro ciclo de `edit`, o `check` final e
o `index` retornaram `0`. A falha intencional retornou `1` com um único
diagnóstico acionável. Depois da edição humana, o `check` retornou `0` e o
índice permaneceu determinístico e atualizado.

#### Cenário B: projeto com decisões existentes

O segundo repositório temporário reutilizou, sem alteração inicial, os quatro
ADRs de `tests/AdrGuard.Tests/Fixtures/Review/adrs`. A coleção contém um ADR
`Proposed`, dois `Accepted`, um `Superseded` e a relação `Superseded by` de
`0003-legacy-cache.md` para `0004-use-redis-cache.md`.

1. Copiar os quatro fixtures para `docs/adr` de um repositório Git temporário.
2. Executar `adr-guard init . --adr-directory docs/adr --template minimal`.
3. Executar `adr-guard check docs/adr --validation-profile strict`.
4. Executar
   `adr-guard index docs/adr --validation-profile strict --catalog enriched`.
5. Localizar no índice os estados `Proposed`, `Accepted` e `Superseded`, e
   confirmar que a linha do ADR 0003 aponta `0004-use-redis-cache.md` como
   sucessor.
6. Carregar `0004-use-redis-cache.md` por `edit --form` e conferir título,
   formato e path retornados sem modificar a decisão aceita.

Resultado: `init`, `check`, `index` enriquecido e carga por `edit` retornaram
`0`. O índice tornou encontráveis os quatro registros, seus estados e a cadeia
de substituição, sem tratar o ADR `Proposed` como decisão vigente.

### Integrações opcionais

- **VS Code:** a instalação isolada do Marketplace `1.1.0` terminou com
  sucesso e `code --list-extensions --show-versions --extensions-dir` confirmou
  a versão. Não houve sessão automatizada de Extension Host nem participante
  operando a interface; criação, edição visual e Problems continuam pendentes
  de validação visual independente. A documentação acerta ao exigir uma CLI
  separada e ao manter MCP desabilitado por padrão.
- **MCP:** o pacote público `1.0.0` foi exercitado por um cliente MCP compatível
  já existente em `McpServerIntegrationTests`. As três verificações da classe
  passaram contra o executável instalado: descoberta das sete ferramentas
  read-only, `list_adrs`, rejeição de argumentos inválidos com servidor ainda
  utilizável, impacto e drift sobre fixtures controlados. Isso valida protocolo
  e descoberta, não uma experiência de agente ou modelo externo.
- **Action:** a leitura do contrato publicado confirmou os quatro comandos
  documentados e a ausência de `drift`. O cenário não executou um runner GitHub
  hospedado nem publicou workflow; a integração continua opcional e a diferença
  planejada de `drift` permanece trabalho separado.
- **IA:** `draft` e `review` não foram invocados. Nenhuma API, credencial,
  telemetria ou conteúdo externo foi usado.

### Findings e decisão

| Prioridade | Categoria e evidência | Decisão |
| --- | --- | --- |
| Média, evidência técnica única | `edit --form` retorna exit `1` ao tentar carregar um ADR com seção obrigatória vazia. A resposta não contém o formulário/fingerprint, portanto o próprio contrato visual não pode reparar esse ADR. A recuperação por edição de texto funcionou imediatamente. | Registrar como oportunidade de UX/documentação. Não alterar a CLI nem a extensão sem repetição independente ou um requisito específico de recuperação visual. |
| Baixa, ambiente | O perfil normal do VS Code tinha `0.9.0`, apesar de `1.1.0` estar disponível e instalar corretamente em isolamento. | Não é defeito do produto. Manter a orientação de verificar a versão no mesmo host local/remoto da extensão. |
| Informativa | CLI, MCP, extensão e Action têm versões e canais independentes; source e publicação divergiam apenas no MCP (`1.0.1` candidato versus `1.0.0` público) e nos commits posteriores ao release da CLI/Action. | Manter a matriz de compatibilidade e a distinção explícita entre source, candidato e publicado. |

Recomendação desta execução: **manter o onboarding atual**, sem mudança de
produto ou documentação pública. O fluxo offline publicado é reproduzível, o
erro estrutural possui diagnóstico e recuperação manual claros, e a descoberta
de decisões existentes funcionou. A oportunidade de recuperação via formulário
deve ser reavaliada somente com evidência adicional. Testes independentes com
ao menos três participantes consentidos continuam pendentes; esta execução não
fecha essa parte da issue #209 e não sustenta alegações de adoção.

### Verificações executadas

- Instalação e execução da CLI NuGet `1.9.0` nos dois repositórios temporários:
  todos os passos finais passaram; a falha controlada retornou o exit esperado.
- Instalação isolada da extensão Marketplace `1.1.0`: passou.
- `dotnet test --project tests/AdrGuard.Mcp.Tests/AdrGuard.Mcp.Tests.csproj
  --configuration Release --filter-class '*McpServerIntegrationTests*'`, com
  `ADR_GUARD_MCP_TEST_COMMAND` apontando para o MCP NuGet `1.0.0`: `Passed: 3,
  Failed: 0, Skipped: 0`.
- Uma tentativa anterior com filtro de método não selecionou testes e retornou
  exit `8`; ela não foi contabilizada como validação.
- Comparação de `action.yml` e `scripts/github-action.sh` entre o SHA público e
  o HEAD: sem diferenças.
- Não executados: sessão visual do VS Code, runner hospedado da Action, APIs de
  IA, publicação, testes com participantes e suíte .NET completa.

### Validação integrada dos Prompts 2 a 6

A integração foi repetida em 4 de novembro de 2026 na branch
`feat/phase-2-developer-experience`, sobre o HEAD `3d71439`, usando o executável
Release local `1.9.0+3d714392aed78b3ca18896bcdc29ab1ced416a75`. O diff contra
`main` foi separado nos seguintes recortes observáveis:

| Recorte | Testes e contratos afetados |
| --- | --- |
| Plano e evidência de onboarding | Contratos existentes de `init`, `new`, `edit --form`, `check`, `index`, perfis de validação e exit `1` para findings. |
| Contrato e implementação de `doctor` | Help/roteamento da CLI, argumentos e streams, exits `0`, `2` e `3`, configuração schema 1, descoberta confinada da raiz, diretório de ADRs, runtime e probe limitado do Git. |
| `drift` na Action | Manifesto de inputs, comandos existentes da Action, exits `0`, `2`, `3` e `4`, JSON schema `1.0`, summary limitado, paths/symlinks, mounts read-only, rede/capabilities, ausência de secrets e documentação EN/pt-BR. |
| Compatibilidade não alterada | JSON/SARIF de `check`, configuração e políticas existentes. MCP e VS Code não tiveram arquivos ou contratos alterados; `AdrRepositoryRoot.TryResolve` é consumido somente por CLI/Core (`check`, `baseline`, `index` e `doctor`). |

Verificações concluídas:

- Testes MTP/xUnit v3 direcionados em Release: `DoctorCommandIntegrationTests`
  (`Passed: 12, Failed: 0, Skipped: 0`), `CliApplicationTests` (`7/0/0`),
  `CliArgumentContractTests` (`32/0/0`), `CheckReportIntegrationTests`
  (`9/0/0`), `ConfigurationProcessIntegrationTests` (`15/0/0`) e
  `DriftCommandIntegrationTests` (`8/0/0`). Isso cobre help e argumentos
  existentes, exits, JSON/SARIF, configuração, `doctor` e drift da CLI.
- `scripts/github-action-impact-test.sh`, `github-action-drift-test.sh`,
  `github-action-report-test.sh`, `github-action-docs-test.sh` e
  `github-marketplace-readiness-test.sh` passaram com Docker capturado e
  reports limitados. `github-action-security-test.sh 1.9.0` passou contra a
  imagem pública anônima no digest
  `sha256:6e21eedd232b2b2858611c8876ba06b52098986336de1461c5d950b7582f4ae9`.
- O Cenário A passou com `init`, dois `new`, dois ciclos válidos de
  `edit --form`, falha deliberada `ADR005` com exit `1`, recuperação manual,
  `check --validation-profile strict` e índice com os dois ADRs `Proposed`.
- O Cenário B passou ao copiar explicitamente os quatro ADRs descritos no
  roteiro, seguido de `init`, `check` estrito, catálogo enriquecido, cadeia de
  supersessão e `edit --form` somente leitura do ADR 0004. A pasta de fixtures
  contém também `0009-invalid.md`; copiar a pasta inteira deixa de reproduzir
  o cenário de quatro ADRs e produz corretamente três findings `ADR005`.
- `npm run sync:docs` sincronizou 78 documentos sem produzir diff adicional.
  Os checks da Action confirmaram os pares EN/pt-BR, exemplos, matriz de
  compatibilidade e a distinção entre source de desenvolvimento e Action
  publicada.

Limitações e pendências desta execução:

- `github-action-smoke-test.sh 1.9.0` confirmou `check`, `index` e as rejeições
  de inputs anteriores, mas não concluiu no Windows: o Git Bash não criou o
  symlink Linux usado pelo caso de escape, e o wrapper recebeu um diretório
  interno válido. O gate completo continua pendente em runner Linux.
- `github-action-contract-test.sh` não foi executado porque sua fixture gerada
  `.tmp/adr-action-contract` não estava presente. Nenhuma fixture substituta foi
  inventada; o gate permanece a cargo do job Linux que a prepara.
- MCP e VS Code não foram repetidos, pois o diff não alcança essas superfícies
  nem um comportamento compartilhado consumido por elas. Permanecem também
  fora do escopo a sessão visual, runner hospedado, participantes externos,
  publicação e a suíte .NET completa.
- Nenhuma regressão introduzida pela Fase 2 foi comprovada. Portanto, não houve
  correção de produto, alteração de contrato, refactor ou funcionalidade
  adicional nesta validação.

## Critérios de conclusão

- A issue #209 possui relatório conciso e roteiro reproduzível com dois
  contextos externos, versões/SHA/ambiente, caminho de sucesso, falha e
  recuperação, observações anonimizadas, limitações e findings priorizados.
- O resultado recomenda explicitamente manter o onboarding atual, corrigir
  blockers específicos ou adiar melhorias; nenhuma alegação de adoção é
  inferida de CI, fixtures, downloads ou estrelas.
- Toda mudança de CLI está ligada a uma reprodução e teste de regressão, mantém
  o caminho offline e preserva contratos não relacionados. Se não houver
  evidência suficiente, a conclusão documentada é não alterar a CLI.
- `command: drift` na Action, se implementado, adapta o mesmo contrato da
  CLI/Core, valida inputs e paths antes do container, permanece read-only e
  secretless, produz reporting seguro e preserva os exit codes de drift.
- Baseline criado/atualizado e evidência externa não atestada nunca são
  convertidos pela Action em aprovação ou conformidade.
- Testes focados cobrem compatibilidade dos modos existentes, fluxo de drift,
  falhas fechadas e entradas/saídas hostis; os gates proporcionais executados
  terminam com sucesso e são reportados sem afirmar checks não executados.
- Documentação pública afetada está equivalente em EN/pt-BR, usa as páginas
  existentes, distingue source de publicação real e tem o portal regenerado
  pelo fluxo oficial quando aplicável.
- Não há mudança não planejada em schemas, API pública, defaults, exit codes,
  versões, tags, releases ou autoridade humana sobre ADRs.

## Riscos relevantes

- **Evidência insuficiente:** walkthrough de maintainer ou fixture sintética não
  substitui observação independente; a limitação precisa permanecer explícita.
- **Contrato publicado divergente:** source, NuGet, GHCR, Marketplace, MCP e
  `@v1` evoluem separadamente. Testar a `main` não valida o primeiro uso real.
- **Regressão de CLI:** mensagens, ordem de erro, streams e exits são consumidos
  por scripts e adapters, mesmo quando não parecem uma API formal.
- **Ambiguidade de inputs:** `policy` hoje descreve review na Action, embora
  `drift` use os mesmos valores. Reuso sem decisão explícita pode alterar o
  significado percebido de um contrato existente.
- **Falso verde em drift:** `unknown`, evidência ausente ou produtor externo não
  atestado não podem virar sucesso por conveniência de CI.
- **Escrita e confiança:** expor atualização de baseline pela Action ampliaria o
  mount gravável e permitiria aceitar dívida sem revisão; por isso fica fora do
  modo composto inicial.
- **Conteúdo hostil:** ADRs, manifests, evidências e relatórios podem tentar
  escapar paths, injetar workflow commands ou HTML e esgotar limites; validação,
  escaping e bounds existentes devem ser estendidos ao novo modo.
- **Escopo misturado:** combinar onboarding, refactor remanescente da Fase 1 e
  integração de Action em uma única mudança dificulta atribuir regressões e
  enfraquece a evidência de conclusão.
