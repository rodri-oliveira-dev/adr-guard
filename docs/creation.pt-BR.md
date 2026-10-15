# Criação offline de ADRs e templates

O nome de arquivo padrão continua sendo `NNNN-kebab-case.md`. A configuração do repositório pode habilitar `unnumbered`, `adr-prefix` ou `numeric:1..9` por meio de `filename-policy`. `adr-guard new` usa essa política para novos arquivos e nunca renomeia ADRs existentes. Execute `check` primeiro para identificar colisões ou identidades estáveis ambíguas antes de uma migração manual.

[English](creation.md) · [README](../README.pt-BR.md) · [Contrato de templates personalizados](custom-templates.pt-BR.md) · [Integração de templates com IA](draft-templates.pt-BR.md)

> **Disponibilidade por versão:** `new` e o `draft` opcional com templates estão disponíveis nas releases publicadas da CLI/container desde a **v1.1.0**; ferramentas 1.0.x não os oferecem. Instale ou atualize a .NET Tool estável, ou use uma imagem de release publicada. A GitHub Action `@v1` publicada suporta `check`, `index` e `review` opt-in; `new` e `draft` assistido por IA continuam sendo fluxos de CLI/container direto.

> A criação e a edição visuais estruturadas estão implementadas para a linha 1.7.0 da CLI e exigem uma versão compatível da extensão VS Code, versionada separadamente. Confirme a publicação dos dois artefatos antes de depender dos formulários visuais; a implementação nesta branch não os torna disponíveis no NuGet ou no Visual Studio Marketplace.

## Início rápido — sem IA, conta, chave de API ou rede

Execute na raiz do repositório com o comando `adr-guard` publicado na **v1.1.0 ou superior** instalado ou disponível no `PATH`. O diretório de destino **precisa existir**. O modelo padrão é `minimal` e o idioma padrão das instruções é `en-US`.

```bash
mkdir -p docs/adr
adr-guard new docs/adr --title "Adotar Redis"
adr-guard check docs/adr
adr-guard index docs/adr
```

O primeiro comando grava `docs/adr/NNNN-adotar-redis.md`; `NNNN` é o próximo ID após o **maior ID já existente**, não a primeira lacuna. Num diretório vazio, será `0001-adotar-redis.md`. `new` **não** atualiza `docs/adr/README.md`; `check` valida o conjunto e `index` cria/atualiza o índice determinístico somente depois de validar. `index` não sobrescreve arquivos de ADR. Executar `new` novamente com o mesmo título após uma criação bem-sucedida aloca um novo ID; criadores concorrentes do mesmo título e mesmo caminho de prévia recebem conflito explícito, nunca sobrescrita silenciosa.

## Escolha Minimal, Extended ou Custom

```bash
adr-guard new docs/adr --title "Adotar Redis" --template minimal --culture pt-BR
adr-guard new docs/adr --title "Adotar Mensageria" --template extended --culture pt-BR
adr-guard new docs/adr --title "Adotar Cache" --template-file docs/examples/templates/team.pt-BR.md --culture pt-BR
adr-guard new docs/adr --title "Testar Redis" --template extended --preview
adr-guard new docs/adr --title "Testar Redis" --template extended --dry-run
```

Os únicos nomes internos são `minimal` e `extended` (diferenciam maiúsculas/minúsculas). `minimal` gera as seções canônicas `Context`, `Decision` e `Consequences`; `extended` acrescenta critérios, alternativas, justificativa, consequências, riscos e referências. `--culture` altera **somente as instruções editoriais** e aceita `en-US` ou `pt-BR`. Mesmo em português, os títulos Markdown canônicos **`Status`, `Context`, `Decision`, `Consequences`** e o status inicial **`Proposed`** permanecem em inglês, para que o validador existente os reconheça.

O template personalizado é **um único arquivo `.md` local em UTF-8**, selecionado explicitamente, com no máximo **65.536 bytes** (64 KiB incluindo BOM UTF-8 opcional). O caminho relativo em `--template-file` é resolvido a partir do diretório no qual o comando foi invocado, não de `docs/adr`. Coloque os templates **fora do diretório de ADRs**, para evitar que `check` os interprete como documentos a validar. `--template` explícito e `--template-file` são mutuamente exclusivos, inclusive com `--template minimal`. Não existe descoberta recursiva de arquivos. Arquivos ausentes, inacessíveis, grandes demais, malformados ou com UTF-8 inválido são recusados antes da gravação. Consulte a [gramática e os placeholders](custom-templates.pt-BR.md) e um [template personalizado pronto para copiar](examples/templates/team.pt-BR.md).

`--preview` e `--dry-run` calculam o caminho provável, validam e exibem **todo o Markdown candidato** sem criar arquivo, índice, artefato temporário ou reserva de ID. Se outro processo criar uma ADR antes da gravação real, o ID final poderá mudar. O diretório de destino precisa existir. O renderizador deixa instruções editáveis, sem simular uma decisão arquitetural pronta.

## Exemplos gerados e validados

São **rascunhos estruturalmente válidos**, com instruções propositadamente mantidas para o autor substituir. Cada exemplo fica num diretório separado para evitar IDs duplicados:

- [Minimal, inglês: `0001-adopt-redis.md`](examples/generated/minimal/0001-adopt-redis.md)
- [Extended, instruções em português: `0001-adotar-redis.md`](examples/generated/extended/0001-adotar-redis.md)
- [Custom, inglês: `0001-adopt-cache.md`](examples/generated/custom/0001-adopt-cache.md) a partir de [`team.en-US.md`](examples/templates/team.en-US.md)
- [Custom, português: `0001-adotar-cache.md`](examples/generated/custom-pt-BR/0001-adotar-cache.md) a partir de [`team.pt-BR.md`](examples/templates/team.pt-BR.md)

```bash
adr-guard check docs/examples/generated/minimal
adr-guard check docs/examples/generated/extended
adr-guard check docs/examples/generated/custom
adr-guard check docs/examples/generated/custom-pt-BR
```

No CI, a ferramenta empacotada e instalada gera os quatro exemplos separadamente, compara o conteúdo byte a byte e executa os comandos de validação. Não copie os quatro exemplos para o mesmo diretório de ADRs: cada um usa intencionalmente o ID `0001`.

## Formato personalizado e placeholders

O template começa com `# {{title}}`, seguido por `## Status`, cujo corpo inteiro é `{{status}}` ou `Proposed`, e pelas seções não vazias `## Context`, `## Decision` e `## Consequences`. Seções H2 literais adicionais são permitidas, desde que não dupliquem as canônicas. O renderizador controla o título H1 único, os títulos estruturais e `Proposed`. Templates não executam código.

Placeholders exatos e sensíveis a maiúsculas/minúsculas: `{{title}}` (título de uma linha escapado para Markdown), `{{id}}` (quatro dígitos), `{{status}}` (sempre Proposed), `{{context}}`, `{{decision}}`, `{{consequences}}`, `{{guidance-context}}`, `{{guidance-decision}}` e `{{guidance-consequences}}`. Em `new` offline, placeholders de conteúdo da IA viram strings vazias; as instruções localizadas e os comentários editáveis mantêm as seções não vazias. Os três campos de conteúdo gerado são preenchidos somente se um `draft` com template e provedor configurado for executado separadamente.

Tokens desconhecidos/malformados, headings estruturais duplicados e tentativa de sobrescrever o status são rejeitados. Substituições ocorrem **uma única vez**, sem executar shell, interpretar expressões ou expandir modelos recursivamente. O template não controla diretório de saída nem nome do arquivo.

## Contrato do adaptador de formulário visual

`adr-guard new <diretório> --form` é um contrato limitado para adaptadores de interface confiáveis, implementado para a CLI 1.7.0. Ele não é um formulário interativo do terminal: lê um documento JSON schema `1.0` pelo stdin e grava uma resposta JSON schema `1.0` no stdout. As operações `describe`, `preview` e `create` oferecem apenas os templates nativos `minimal` e `extended`. A prévia usa o mesmo renderer da criação, mas nunca grava nem reserva ID; `create` realoca e persiste de forma atômica. Um `template-file` configurado é informado como incompatível para que o adaptador ofereça o fluxo tradicional sem trocar templates silenciosamente.

As requisições usam `schemaVersion` e `operation`; `preview` e `create` também exigem `title` mais o objeto `fields` de strings retornado pela descrição, enquanto `describe` não aceita conteúdo de formulário não nulo. As respostas contêm `schemaVersion`, `operation`, `template` (`name`, `compatible`, `reason` opcional e `fields` ordenados), `written` e `path`/`markdown` opcionais. Propriedades desconhecidas/duplicadas, IDs de campos não suportados, NULs, profundidade/tamanho excessivos, campos obrigatórios ausentes e versões diferentes de `1.0` falham de forma fechada. O conteúdo do formulário nunca é aceito em argumentos de linha de comando nem em logs operacionais.

```json
{"schemaVersion":"1.0","operation":"preview","title":"Adotar uma fila","fields":{"context":"...","decision":"...","consequences":"..."}}
```

A [extensão VS Code](../extensions/vscode/README.pt-BR.md), versionada separadamente, usa esse contrato na criação orientada. A edição estruturada usa `adr-guard edit <arquivo> --form` com `load` e `save`: aceita documentos canônicos e MADR 4.0, preserva o Markdown não alterado e rejeita salvamentos obsoletos comparando o fingerprint retornado por `load`. Consulte a [referência da CLI](cli-reference.pt-BR.md#criar-uma-adr-por-um-cliente-estruturado) para as mensagens exatas de criação e edição, campos de resposta, validação e códigos de saída. Use `new --title` tradicional para uso direto no terminal e para templates personalizados.

## Compatibilidade de renderização e quebras de linha

A saída produzida por **templates** (`new` e `draft` com `--template`/`--template-file`) usa LF (`\n`) de forma determinística em Linux, Windows e macOS. Já o `draft` legado **sem template** preserva intencionalmente o contrato publicado antes deste roadmap, incluindo `Environment.NewLine` nativo do host. Essa exceção evita uma alteração byte a byte silenciosa no fluxo existente ao mesmo tempo em que mantém o novo contrato de templates reproduzível entre plataformas.

## Escrita concorrente, segurança e erros

`new` offline e `draft` com IA compartilham a mesma infraestrutura de criação: escritores cooperantes executados pelo **mesmo usuário do sistema operacional no mesmo host** serializam a alocação do ID com mutex nomeado entre sessões, cuja identidade resolve aliases por symlink e aliases de capitalização no Windows/macOS; o template é renderizado novamente sob bloqueio com o **ID final**, validado, escrito em arquivo temporário e promovido atomicamente sem sobrescrever o destino. Falhas/cancelamentos limpam temporários; um ID não persistido pode ser reutilizado. Escritores externos não cooperantes e hosts diferentes em filesystem compartilhado exigem coordenação adicional.

O contrato de códigos de saída é `0` sucesso, `1` erro de validação de ADR, `2` uso incorreto da CLI (ex.: templates conflitantes, nome/idioma desconhecido, título ausente), `3` erro operacional (ex.: diretório de ADR inexistente, arquivo de template ausente/malformado, I/O, ID esgotado ou cancelamento). ADRs existentes inválidas produzem código `1` e não criam outro arquivo. Após criação bem-sucedida, o status é **Proposed**, nunca aprovação automática: um revisor deve substituir as instruções e examinar a justificativa e os trade-offs antes de aceitar a decisão. O ADR Guard valida **estrutura**, não mérito técnico nem formatos alternativos/MADR nativos.

## Draft opcional com IA e distribuições

O `draft` sem seleção de template mantém o comportamento anterior, as culturas .NET e o contrato com o provedor. Quando a IA é usada intencionalmente, pode-se selecionar `--template minimal|extended` **ou** `--template-file` no `draft`. Conteúdo, orientação e caminho do arquivo de template continuam **locais** e **não são enviados ao provedor**: ele recebe o `--context` obrigatório, cada `--context-file` explicitamente selecionado e dados interpretados das ADRs existentes **somente** com `--include-existing-adrs`. Limites de contexto, autenticação/endpoint do provedor e revisão humana permanecem válidos; `draft --preview` **chama o provedor**, mas não grava uma ADR. Consulte [exemplos de provedores e privacidade](draft-templates.pt-BR.md).

NuGet.org e GitHub Packages distribuem a .NET Tool. GHCR (`ghcr.io/rodri-oliveira-dev/adr-guard`) e Docker Hub (`rodrigodotnet/adr-guard`) distribuem a mesma CLI em imagem versionada: execute `new` offline montando o diretório de ADRs com permissão de escrita e **sem credenciais de IA**; execute `draft` com credenciais e contexto fornecidos conscientemente. Consulte os [exemplos de container](container.pt-BR.md). A GitHub Action publicada `rodri-oliveira-dev/adr-guard@v1` suporta `check`, `index` e `review` opt-in; nem `new` nem `draft` são comandos da Action.
