# Configurar o servidor MCP do ADR Guard

[English](mcp-configuration.md) · [Visão geral](mcp.pt-BR.md) · [Ferramentas](mcp-tools.pt-BR.md) · [Segurança](mcp-security.pt-BR.md)

O MCP 1.0.0 está preparado como .NET Tool com versionamento independente, mas nenhum pacote oficial ou entrada no Registry foi publicado. Os comandos abaixo distinguem a futura instalação pelo NuGet de um pacote local ou build do código-fonte utilizável agora.

## Instalação após a primeira publicação verificada

O pacote exige o runtime .NET 10 e instala `adr-guard-mcp` independentemente da CLI tradicional `adr-guard`:

```shell
dotnet tool install --global RodriOliveira.AdrGuard.Mcp --version 1.0.0
adr-guard-mcp --repository /caminho/absoluto/do/repositorio --adr-directory docs/adr
```

Para um manifesto local de ferramentas, use `dotnet tool install --local RodriOliveira.AdrGuard.Mcp --version 1.0.0` e `dotnet tool run adr-guard-mcp -- --repository ...`. Confirme primeiro o pacote NuGet e a entrada no Registry; a existência do workflow não significa publicação.

## Pré-requisitos e build do código-fonte

- SDK .NET `10.0.402`, fixado em `global.json`.
- Checkout local contendo `src/AdrGuard.Mcp`.
- Git no host do servidor somente ao usar `analyze_impact` ou quando `verify_architecture` registra o commit atual.

Na raiz do repositório:

```shell
dotnet restore AdrGuard.slnx
dotnet build AdrGuard.slnx --configuration Release --no-restore
```

O ponto de entrada dependente do runtime é:

```text
src/AdrGuard.Mcp/bin/Release/net10.0/adr-guard-mcp.dll
```

Execute-o com o runtime e uma raiz de repositório fixa:

```shell
dotnet src/AdrGuard.Mcp/bin/Release/net10.0/adr-guard-mcp.dll \
  --repository /caminho/absoluto/do/repositorio \
  --adr-directory docs/adr \
  --timeout-seconds 30
```

O processo aguarda mensagens MCP na entrada padrão. Iniciá-lo de forma interativa é útil apenas para confirmar erros de configuração na saída de erro; use um cliente MCP para a comunicação do protocolo.

## Argumentos de inicialização

| Argumento | Obrigatório | Padrão | Contrato |
| --- | --- | --- | --- |
| `--repository <path>` | Não | Diretório de trabalho atual | Raiz local existente; resolvida como caminho completo na inicialização. |
| `--adr-directory <relative-path>` | Não | `.adrguard.yml`, depois `docs/adr` quando existir, senão `.` | Diretório existente contido no repositório. |
| `--timeout-seconds <1-300>` | Não | `30` | Timeout por operação de ferramenta. |

Argumentos desconhecidos, ausentes ou malformados retornam exit code `2`. Falhas de I/O ou acesso na inicialização retornam `3`. O repositório e o diretório de ADRs são validados antes do início do host de protocolo.

## Integração com a extensão VS Code

A integração preparada para a extensão VS Code 1.1.0 usa a API estável de provider MCP do VS Code e começa desabilitada:

1. Compile o servidor no ambiente que hospeda a extensão de workspace.
2. Abra um workspace local `file:` e confiável.
3. Abra as configurações visuais do ADR Guard e escolha **Integração MCP**, ou edite as configurações do VS Code.
4. Defina `adrGuard.mcp.path` como caminho absoluto fora de todos os workspaces abertos. Use `adr-guard-mcp.dll` ou o app host da plataforma chamado `adr-guard-mcp` (`adr-guard-mcp.exe` no Windows).
5. Habilite `adrGuard.mcp.enabled` para a pasta desejada.
6. Use **Verificar configuração MCP** na tela. “Disponível para iniciar” valida a configuração; não afirma que há processo conectado ou em execução.
7. Salve explicitamente. O VS Code atualiza as definições MCP e inicia/encerra servidores por seu próprio ciclo quando um agente os solicita.

Exemplo de configurações (substitua o placeholder por um caminho absoluto real no host):

```json
{
  "adrGuard.mcp.enabled": true,
  "adrGuard.mcp.path": "/opt/adr-guard/adr-guard-mcp.dll"
}
```

A extensão contribui com o provider `adrGuard.mcpServers`. Ela cria uma definição `stdio` distinguível por pasta habilitada e fornece `--repository` mais o `adrGuard.validation.directory` daquela pasta. Ela nunca inicia o MCP durante a ativação e não modifica `.vscode/mcp.json`.

O VS Code pode oferecer essas definições a experiências de agentes compatíveis, inclusive recursos de agente do GitHub Copilot quando instalados, habilitados e autorizados separadamente pelo usuário. O ADR Guard não possui dependência obrigatória do Copilot. Os testes do repositório verificam o provider do VS Code e um harness MCP local sem provider pago, token ou chamada externa a modelo; eles não afirmam validar todos os produtos de agentes.

A extensão aceita `adrGuard.mcp.path` somente da configuração confiável de usuário/máquina. Um valor no workspace é ignorado. Uma `.dll` exige `dotnet` no `PATH` do host da extensão; a descoberta examina uma quantidade limitada de entradas absolutas e nunca usa shell.

## Desenvolvimento remoto

Em Remote-SSH, Dev Containers e WSL, o host da extensão de workspace é remoto. Compile ou coloque o servidor nesse ambiente, configure o escopo de máquina remoto e use caminhos existentes nele. Um caminho da máquina local da interface não é reutilizado remotamente. Workspaces virtuais ou não confiáveis não recebem definições MCP.

## Configuração direta de cliente

Um cliente MCP padrão que aceite comando `stdio` pode iniciar a mesma DLL com um array de argumentos equivalente a:

```json
{
  "command": "dotnet",
  "args": [
    "/opt/adr-guard/adr-guard-mcp.dll",
    "--repository", "/work/projeto",
    "--adr-directory", "docs/adr"
  ]
}
```

Esse formato descreve o contrato do servidor; nomes de arquivo e controles de aprovação variam por cliente. O repositório atualmente verifica o registro pelo provider da extensão VS Code, não por arquivos de configuração mantidos para outros clientes. Não copie segredos para comando, argumentos ou ambiente salvo se um cliente os exigir de forma independente; o MCP do ADR Guard não exige credenciais.

## Solução de problemas

- **Não configurado:** informe um caminho absoluto e habilite explicitamente a pasta.
- **Executável não encontrado:** confirme que o arquivo existe no host da extensão; para DLL, confirme `dotnet` no `PATH` desse host.
- **Configuração inválida:** use o nome exato, um arquivo regular que não seja symlink e esteja fora de todos os workspaces, além de um caminho absoluto local suportado. Caminhos UNC são rejeitados no Windows.
- **Sem definição:** confie no workspace, use uma pasta local `file:`, salve a habilitação por pasta e verifique o caminho de máquina.
- **Servidor encerra com erro de uso:** confira as três opções aceitas e forneça valores como argumentos separados.
- **Timeout ou resposta grande demais:** restrinja a solicitação ou configure `--timeout-seconds` no intervalo suportado; o limite de resposta de 1 MiB é fixo.
