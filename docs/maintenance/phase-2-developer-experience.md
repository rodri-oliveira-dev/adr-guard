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
