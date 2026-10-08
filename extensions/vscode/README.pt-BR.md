# ADR Guard para VS Code

ADR Guard para VS Code é uma extensão de workspace que apresenta a CLI existente do ADR Guard dentro do editor. Ela oferece comandos nativos, diagnósticos no Problems e um ADR Explorer, mantendo parser, validação, políticas, geração e análise baseada em Git na CLI .NET.

> Estado: a versão **0.1.0** da extensão é um MVP distribuído para revisão por VSIX. Ela não está publicada no Visual Studio Marketplace. O Publisher no Marketplace foi registrado como `rodrioliveira`; a publicação ainda está pendente. Os comandos avançados exigem os contratos compatíveis da CLI previstos no NuGet v1.3.0 (incluindo o antigo escopo de desenvolvimento v1.4); essa release pública da CLI deve estar disponível antes da publicação no Marketplace.

## Funcionalidades

- Verificação da CLI no host da extensão do workspace.
- Inicialização de repositório com prévia `--dry-run` e confirmação explícita.
- Criação de ADRs com template configurado, mínimo, estendido ou Markdown local seguro.
- Validação por JSON versionado com resultados no painel Problems.
- Validação opcional e agrupada após salvar uma ADR.
- Geração explícita do índice, sempre com confirmação.
- Explorer nativo para formatos canônico e MADR 4.0, com status e relacionamentos apenas para apresentação/navegação segura.
- Validação incremental contra uma ref Git local informada, sem `git fetch` oculto.
- Classificação contra um baseline existente e somente leitura.
- Interface em inglês por padrão e português brasileiro quando o VS Code usa `pt-BR`.

## Requisitos e instalação

- VS Code `1.100.0` ou superior na linha `1.x`.
- Workspace local `file:` e confiável.
- Executável `adr-guard` compatível instalado no ambiente em que o **workspace extension host** é executado.

Quando a versão necessária da CLI estiver disponível, instale-a como ferramenta .NET:

```shell
dotnet tool install --global RodriOliveira.AdrGuard
adr-guard --version
```

Para capacidades de desenvolvimento ainda não publicadas, compile ou instale a CLI da branch correspondente do repositório e informe o caminho absoluto confiável em `adrGuard.cli.path`. Caso contrário, a extensão pesquisa diretórios absolutos no `PATH` do host. Ela nunca baixa nem instala a CLI.

Para instalar manualmente o artefato de revisão, execute **Extensions: Install from VSIX...** e selecione `adr-guard-0.1.0.vsix`, ou use:

```shell
code --install-extension adr-guard-0.1.0.vsix
```

## Comandos

Abra a Paleta de Comandos e escolha a ação sob **ADR Guard**:

| Comando | Comportamento |
| --- | --- |
| Verificar instalação | Executa `adr-guard --version` por descoberta segura. |
| Inicializar repositório | Mostra a prévia de `init --dry-run` e pede confirmação antes de escrever. |
| Criar ADR | Executa `new` e abre somente o arquivo criado e verificado. |
| Validar ADRs | Executa validação JSON completa e atualiza Problems. |
| Validar ADRs alteradas | Usa `--changed --base-ref`; nunca executa `git fetch`. |
| Validar ADRs com baseline | Lê o baseline existente e informa novos/existentes/resolvidos. |
| Gerar índice de ADRs | Alerta antes de a CLI atualizar o `README.md` das ADRs. |
| Selecionar formato de ADR | Seleciona `canonical` ou `madr-4` para a pasta. |
| Atualizar ADR Explorer | Invalida o cache do Explorer e recarrega. |
| Revelar ADR atual | Seleciona a ADR ativa no Explorer. |

Workspaces com múltiplas raízes são suportados. Quando possível, os comandos usam a pasta do arquivo ativo; caso contrário, solicitam a pasta. Configurações de escopo de recurso podem variar por pasta.

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
- `adrGuard.validation.directory`: diretório de ADRs relativo ao workspace.
- `adrGuard.validation.adrFormat`: `canonical` ou `madr-4`.
- `adrGuard.validation.baseline`: caminho relativo para o JSON de baseline.
- `adrGuard.validation.onSave`: validação ao salvar, opt-in.
- `adrGuard.validation.debounceMilliseconds`: intervalo de agrupamento dos salvamentos.

## Solução de problemas

- **CLI não encontrada:** execute **Verificar instalação**, confirme `adr-guard --version` no mesmo ambiente local/remoto do workspace e, se necessário, configure o caminho absoluto.
- **Opção não suportada / exit 2:** a CLI é anterior ao contrato solicitado de MADR, validação incremental ou baseline. Instale uma CLI compatível ou use a validação canônica completa.
- **Workspace desabilitado:** confie no workspace e use uma pasta local `file:`. Workspaces virtuais e hosts somente web não são suportados.
- **Timeout/limite de saída:** aumente a configuração limitada de máquina somente se o repositório realmente exigir.
- **Sem linha exata:** o schema JSON `1.0` informa o arquivo, mas não uma posição; abra o diagnóstico e consulte a regra ADR indicada.
- **CLI remota incompatível:** instale/configure a CLI no host remoto da extensão.

Para bugs reproduzíveis e suporte, use as [issues do GitHub](https://github.com/rodri-oliveira-dev/adr-guard/issues). Relatos de segurança seguem a [política de segurança](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/SECURITY.md), e contribuições seguem as orientações do [repositório](https://github.com/rodri-oliveira-dev/adr-guard). A extensão usa a [Licença MIT](LICENSE). A documentação padrão em inglês está em [README.md](README.md).
