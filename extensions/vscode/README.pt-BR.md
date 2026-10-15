# ADR Guard para VS Code

ADR Guard para VS Code é uma extensão de workspace que apresenta a CLI existente do ADR Guard dentro do editor. Ela oferece comandos nativos, diagnósticos no Problems e um contêiner próprio na Barra de Atividades com o ADR Explorer, mantendo parser, validação, políticas, geração e análise baseada em Git na CLI .NET.

> **Publicado:** o [ADR Guard para VS Code 0.1.1](https://marketplace.visualstudio.com/items?itemName=rodrioliveira.adr-guard) está disponível no Visual Studio Marketplace como `rodrioliveira.adr-guard`. Os contratos compatíveis da CLI ADR Guard foram publicados no NuGet **v1.3.0** (incluindo o antigo escopo de desenvolvimento v1.4). A extensão VS Code e a CLI .NET são distribuídas e versionadas separadamente; a extensão não inclui a CLI.

## Funcionalidades

- Verificação da CLI no host da extensão do workspace.
- Inicialização de repositório com prévia `--dry-run` e confirmação explícita.
- Criação de ADRs com template configurado, mínimo, estendido ou Markdown local seguro.
- Criação de ADRs por formulário visual orientado, com prévia do Markdown, quando o template configurado é nativo.
- Consulta, busca, filtro, ordenação e abertura de ADRs pela tela inicial visual do ADR Guard.
- Validação por JSON versionado com resultados no painel Problems.
- Validação opcional e agrupada após salvar uma ADR.
- Geração explícita do índice, sempre com confirmação.
- Explorer nativo para formatos canônico e MADR 4.0, com status e relacionamentos apenas para apresentação/navegação segura.
- Validação incremental contra uma ref Git local informada, sem `git fetch` oculto.
- Classificação contra um baseline existente e somente leitura.
- Interface em inglês por padrão e português brasileiro quando o VS Code usa `pt-BR`.

## Prévia da interface (não publicada)

As imagens abaixo documentam a branch de desenvolvimento da extensão 1.0.0 em um Extension Development Host real, com uma CLI compilada da mesma branch. Elas documentam o comportamento da branch de desenvolvimento, não o pacote 0.1.1 publicado atualmente no Marketplace. A criação e a edição visuais exigem os contratos de formulário schema `1.0` da CLI 1.7.0.

### Consultar e buscar

![Listagem visual com status, busca, ordenação e ações de edição e abertura do Markdown](../../website/public/images/vscode/adr-list.png)

### Criar com um template nativo

![Formulário Minimal de criação com configuração do documento e conteúdo da decisão](../../website/public/images/vscode/adr-create-minimal.png)

### Editar uma ADR compatível

![Formulário de edição com título, status e seções do documento](../../website/public/images/vscode/adr-edit.png)

### Configurar a extensão

![Configurações do ADR Guard para idioma, diretório do repositório e formato de ADR](../../website/public/images/vscode/adr-settings.png)

O [Guia Visual do VS Code](https://rodri-oliveira-dev.github.io/adr-guard/pt-br/product/vscode-visual-guide/) no portal cobre Explorer, filtros, criação Minimal e Extended, prévia Markdown, edição segura contra alterações externas, configurações de validação, integração com a CLI e diagnósticos em Problems.

## Requisitos e instalação

- VS Code `1.100.0` ou superior na linha `1.x`.
- Workspace local `file:` e confiável.
- Executável `adr-guard` compatível instalado no ambiente em que o **workspace extension host** é executado.

Instale a CLI compatível (v1.3.0 ou superior) no ambiente local ou remoto do host da extensão de workspace:

```shell
dotnet tool install --global RodriOliveira.AdrGuard
adr-guard --version
```

Para capacidades de desenvolvimento ainda não publicadas, compile ou instale a CLI da branch correspondente do repositório e informe o caminho absoluto confiável em `adrGuard.cli.path`. Caso contrário, a extensão pesquisa diretórios absolutos no `PATH` do host. Ela nunca baixa nem instala a CLI.

Instale a extensão publicada pela aba **Extensões** do VS Code (pesquise **ADR Guard**) ou execute:

```shell
code --install-extension rodrioliveira.adr-guard
```

Para testar manualmente um VSIX gerado localmente, use **Extensions: Install from VSIX...** e selecione `adr-guard-1.0.0.vsix`. Esse processo é diferente da instalação pelo Marketplace.

## Comandos

Abra a Paleta de Comandos e escolha a ação sob **ADR Guard**:

| Comando | Comportamento |
| --- | --- |
| Verificar instalação | Executa `adr-guard --version` por descoberta segura. |
| Inicializar repositório | Mostra a prévia de `init --dry-run` e pede confirmação antes de escrever. |
| Criar ADR | Executa o fluxo tradicional compatível de `new` e abre somente o arquivo criado e verificado. |
| Abrir ADR Guard | Abre ou revela a listagem visual de ADRs sem criar arquivos nem executar validação. |
| Criar ADR com formulário | Abre o formulário orientado, gera a prévia pela CLI e cria somente após envio explícito. |
| Validar ADRs | Executa validação JSON completa e atualiza Problems. |
| Validar ADRs alteradas | Usa `--changed --base-ref`; nunca executa `git fetch`. |
| Validar ADRs com baseline | Lê o baseline existente e informa novos/existentes/resolvidos. |
| Gerar índice de ADRs | Alerta antes de a CLI atualizar o `README.md` das ADRs. |
| Selecionar formato de ADR | Seleciona `canonical` ou `madr-4` para a pasta. |
| Atualizar ADR Explorer | Invalida o cache do Explorer e recarrega. |
| Revelar ADR atual | Seleciona a ADR ativa no Explorer. |

Workspaces com múltiplas raízes são suportados. Quando possível, os comandos usam a pasta do arquivo ativo; caso contrário, solicitam a pasta. Configurações de escopo de recurso podem variar por pasta.

## Listagem visual de ADRs (não publicada)

Execute **ADR Guard: Abrir ADR Guard** pela Paleta de Comandos ou pelo título do ADR Explorer. O painel visual reutilizável começa no diretório de ADRs configurado para a pasta selecionada e mostra os metadados disponíveis de identificador, título, status, data e caminho relativo. A busca ignora maiúsculas e minúsculas em título, identificador e caminho; filtros de status e ordenações determinísticas por número, título ou data são aplicados localmente, sem executar validação completa pela CLI. Metadados ausentes continuam visivelmente desconhecidos, e documentos sem número ficam depois das ADRs numeradas.

Ao clicar no ícone dedicado do ADR Guard na Barra de Atividades, essa listagem visual também é aberta ou revelada. O mesmo painel é reutilizado, portanto cliques repetidos não criam abas duplicadas.

O cabeçalho da listagem agrupa atualização, verificação da instalação, inicialização do repositório, criação e uma engrenagem de configurações. A inicialização fica disponível apenas enquanto o diretório configurado não possui ADRs; quando já existem ADRs, o controle desabilitado informa que o repositório já foi inicializado.

A listagem diferencia carregamento, diretório vazio, filtros sem resultados, diretório inexistente, workspace indisponível e erro de leitura. Ela nunca cria diretórios nem inicializa o repositório durante a consulta. Alterações em arquivos são agrupadas pelo watcher compartilhado com o ADR Explorer, e respostas antigas não substituem a pasta atual durante trocas rápidas.

**Nova ADR** abre o formulário existente no mesmo painel e preserva pasta, diretório, busca, filtro, ordem e rolagem para o retorno. Após uma criação bem-sucedida, a listagem é atualizada, a nova ADR é destacada, o Explorer nativo é atualizado e a ação **Abrir Markdown** é oferecida sem retirar o foco automaticamente. Se os filtros ocultarem o novo registro, o painel informa e oferece uma ação explícita para revelá-lo. **Cancelar** e **Voltar para a listagem** retornam sem gravar; sair de um formulário alterado por essas ações controladas exige confirmação. O comando direto **Criar ADR com formulário** continua disponível e também retorna para a listagem.

Voltar e Cancelar são ações exclusivamente de navegação no cadastro, na edição e nas configurações. Elas nunca executam `new`, `edit --save`, `check` ou `index`; enquanto um salvamento de cadastro, edição ou configurações está ativo, esses controles permanecem desabilitados para impedir uma transição concorrente. Atualização, busca, filtros, ordenação, seleção de workspace e watchers do Explorer também atualizam apenas o catálogo somente leitura, sem iniciar validação de ADRs.

Cada item também oferece **Editar**. O mesmo painel visual carrega o documento canônico ou MADR 4.0 selecionado pelo contrato schema `1.0` `edit --form` da CLI, apresenta título, status, metadados e seções de nível dois suportados e retorna à listagem preservada após salvar ou cancelar. A CLI executa validação conforme o formato e substituições localizadas no Markdown, mantém caminho e identificador, preserva metadados/seções/espaçamento não alterados e rejeita o salvamento quando o fingerprint do arquivo mudou depois do carregamento. A extensão revalida os limites do workspace e do catálogo antes de cada leitura ou gravação.

## Formulário visual de criação de ADR (não publicado)

Execute **ADR Guard: Criar ADR com formulário** pela Paleta de Comandos ou use a ação de novo arquivo no título do ADR Explorer. O formulário separa a pasta do workspace e o idioma do documento do idioma da interface do VS Code, inicia o destino com `adrGuard.validation.directory`, usa o idioma e o template padrão da pasta e ainda permite alterações para um documento específico.

O formulário oferece os templates nativos `minimal` e `extended`. O mínimo exige Context, Decision e Consequences. O estendido expõe esses campos obrigatórios e também Decision Drivers, Options Considered, Rationale, Positive/Negative Consequences, Risks e References como opcionais; seções opcionais vazias são omitidas. A criação continua canônica—o formato de validação `canonical`/`madr-4` é um conceito separado e não aparece como opção de template de criação.

Templates personalizados configurados não podem ser mapeados com segurança para campos. O formulário explica a limitação e oferece **Usar criação tradicional**, sem substituir o template personalizado. O comando tradicional **Criar ADR** continua disponível para todos os templates personalizados compatíveis e para CLIs antigas.

A criação visual requer a CLI 1.7.0 ou superior com o contrato schema `1.0` de `new --form`. Prévia e criação usam o mesmo renderer da CLI; a prévia não grava, reserva número nem atualiza o índice, enquanto a criação resolve a numeração definitiva de forma atômica e nunca sobrescreve arquivos. O estado permanece em memória enquanto o painel retido fica oculto. O VS Code não oferece um evento cancelável de fechamento de WebviewPanel; portanto, não é possível garantir uma confirmação de alterações não salvas ao fechar, e o estado em memória é descartado.

## Configurações visuais (não publicadas)

Use a engrenagem do cabeçalho da listagem para configurar o painel existente sem abrir outra aba. O idioma da interface é salvo nas configurações do usuário como Automático (VS Code), Português (Brasil) ou English. Automático acompanha a localização do editor somente nos textos da Webview do ADR Guard; comandos e controles nativos do VS Code mantêm o idioma do próprio editor. O idioma padrão dos documentos e o template de criação são configurações independentes por pasta do workspace, inclusive em workspaces com múltiplas raízes.

A seção Repositório reutiliza `adrGuard.validation.directory` e `adrGuard.validation.adrFormat`. O diretório precisa ser um caminho relativo seguro cuja parte existente permaneça dentro do workspace selecionado. Salvar preferências não cria, move, exclui, valida nem indexa arquivos ADR. Voltar e Cancelar preservam o estado da listagem e exigem confirmação antes de descartar configurações alteradas.

As configurações são gravadas pela API do VS Code nos escopos documentados de usuário/máquina ou pasta do workspace. Se uma gravação do grupo falhar, as anteriores são revertidas e o rascunho permanece disponível para nova tentativa; o painel informa falha em vez de apresentar persistência parcial como sucesso.

A seção Validação configura a validação opt-in ao salvar, seu debounce de 200 a 5000 ms, o caminho somente leitura do baseline e a referência Git local padrão sugerida pela validação incremental. Desligar a validação ao salvar preserva o valor do debounce. O comando incremental ainda permite editar a referência antes da execução e nunca executa `git fetch`. Alterar configurações apenas atualiza preferências do VS Code; a validação ocorre depois somente em um salvamento de arquivo aplicável ou por comando explícito.

A seção Integração com a CLI expõe o executável, timeout e limite de saída no escopo de máquina. Um executável vazio usa a descoberta segura no `PATH` do host da extensão; valores configurados precisam ser caminhos locais absolutos e continuam passando pela validação existente do executável. Timeout e limite de saída ficam na seção avançada recolhível. **Verificar instalação** é sempre uma ação explícita, usa as últimas configurações da CLI salvas e informa a versão detectada ou uma falha acionável, sem instalar nem atualizar nada.

## ADR Explorer e formatos

A view **Registros de Decisões de Arquitetura** aparece no Explorer padrão. Ela percorre o diretório configurado, ignora o `README.md` gerado e exibe um subconjunto limitado de título, status e relacionamentos Markdown reconhecidos. Esses dados servem apenas à navegação; a CLI continua sendo a autoridade para estrutura e governança canônica e MADR 4.0.

Destinos de relacionamentos precisam resolver para arquivos Markdown regulares dentro do workspace e diretório de ADRs selecionados. Destinos ambíguos, inexistentes, externos, virtuais ou que escapem por symlink não são abertos.

## Diagnósticos, validação ao salvar e baseline

`Validar ADRs` converte achados do JSON da CLI com schema `1.0` em Problems. Como o schema não define linha e coluna, o diagnóstico navega para `(0, 0)` sem afirmar uma localização exata. JSON malformado, schema incompatível, divergência entre exit code e `valid`, arquivo fora do diretório e exits `2`–`4` são falhas operacionais.

`adrGuard.validation.onSave` começa como `false`. Quando habilitada, somente ADRs Markdown salvas dentro do diretório configurado disparam validação; rajadas são agrupadas, execuções antigas são canceladas e resultados obsoletos não substituem os atuais. Esse fluxo nunca cria ADR, gera índice, chama IA, busca refs Git ou escreve baseline.

A validação com baseline usa o JSON existente em `adrGuard.validation.baseline` somente para leitura. Criar ou atualizar o baseline permanece um fluxo explícito da CLI fora deste MVP.

## Workspace Trust, ambientes remotos e segurança

A extensão declara workspaces não confiáveis e virtuais como não suportados. Nenhum processo é executado até o workspace ser confiável e a pasta selecionada resolver para um caminho local `file:`. A configuração do executável tem escopo de máquina; candidatos no PATH controlados por um workspace aberto são rejeitados. Argumentos são enviados como array literal com `shell: false`, com cancelamento, timeout e limites independentes de stdout/stderr.

Em WSL, Remote-SSH e Dev Containers, a extensão de workspace roda remotamente. Instale e configure a CLI **nesse ambiente remoto**, não apenas na máquina da interface. VS Code Web puro e workspaces virtuais sem host Node e sistema de arquivos local não são suportados.

A extensão não coleta telemetria, instala software, acessa credenciais de provedores de IA, chama IA automaticamente, altera ADRs ao salvar, escreve baseline automaticamente nem executa rede ou Git fetch ocultos. Os comandos explícitos `init`, `new` e `index` podem escrever arquivos conforme descrito.

## Configurações

- `adrGuard.cli.path`: caminho absoluto confiável da CLI em configuração de usuário/máquina.
- `adrGuard.cli.timeoutMilliseconds`: timeout do processo, entre 1 e 120 segundos.
- `adrGuard.cli.maxOutputBytes`: limite separado para stdout e stderr.
- `adrGuard.ui.language`: idioma da Webview do ADR Guard no escopo do usuário (`auto`, `pt-BR` ou `en-US`).
- `adrGuard.creation.documentLanguage`: idioma padrão de novas ADRs por pasta do workspace.
- `adrGuard.creation.defaultTemplate`: template padrão de criação por pasta (`configured`, `minimal` ou `extended`).
- `adrGuard.validation.directory`: diretório de ADRs relativo ao workspace.
- `adrGuard.validation.adrFormat`: `canonical` ou `madr-4`.
- `adrGuard.validation.baseline`: caminho relativo para o JSON de baseline.
- `adrGuard.validation.onSave`: validação ao salvar, opt-in.
- `adrGuard.validation.debounceMilliseconds`: intervalo de agrupamento dos salvamentos.
- `adrGuard.validation.baseReference`: referência padrão por pasta sugerida pela validação incremental (`main` por padrão).

## Solução de problemas

- **CLI não encontrada:** execute **Verificar instalação**, confirme `adr-guard --version` no mesmo ambiente local/remoto do workspace e, se necessário, configure o caminho absoluto.
- **Formulário visual indisponível:** instale a CLI 1.7.0 ou um build de desenvolvimento com `new --form`, ou escolha **Usar criação tradicional**. A extensão detecta o suporte pelo contrato do comando, sem confiar apenas na string de versão.
- **Opção não suportada / exit 2:** a CLI é anterior ao contrato solicitado de MADR, validação incremental ou baseline. Instale uma CLI compatível ou use a validação canônica completa.
- **Workspace desabilitado:** confie no workspace e use uma pasta local `file:`. Workspaces virtuais e hosts somente web não são suportados.
- **Timeout/limite de saída:** aumente a configuração limitada de máquina somente se o repositório realmente exigir.
- **Sem linha exata:** o schema JSON `1.0` informa o arquivo, mas não uma posição; abra o diagnóstico e consulte a regra ADR indicada.
- **CLI remota incompatível:** instale/configure a CLI no host remoto da extensão.

Para bugs reproduzíveis e suporte, use as [issues do GitHub](https://github.com/rodri-oliveira-dev/adr-guard/issues). Relatos de segurança seguem a [política de segurança](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/SECURITY.md), e contribuições seguem as orientações do [repositório](https://github.com/rodri-oliveira-dev/adr-guard). A extensão usa a [Licença MIT](LICENSE). A documentação padrão em inglês está em [README.md](README.md).
