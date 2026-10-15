# Segurança do MCP do ADR Guard

[English](mcp-security.md) · [Visão geral](mcp.pt-BR.md) · [Configuração](mcp-configuration.pt-BR.md) · [Ferramentas](mcp-tools.pt-BR.md)

O adaptador MCP é um limite local e somente leitura sobre os serviços existentes do ADR Guard. Seus controles reduzem a expansão acidental de escopo; eles não tornam o conteúdo do repositório confiável nem substituem o modelo de permissões do agente cliente.

## Limites de confiança e processo

- O servidor usa apenas `stdio` local; não abre endpoint de rede e não exige credencial.
- A raiz do repositório e o diretório de ADRs são fixados por argumentos explícitos e validados antes de atender solicitações.
- Caminhos das ferramentas são normalizados, precisam permanecer relativos e contidos e não podem escapar da raiz por symlinks/reparse points.
- Nenhuma solicitação é montada como comando de shell. O servidor não executa Markdown, configuração, evidência ou saída de provider.
- O logging operacional usa a saída de erro, registra nomes de ferramentas e tipos de exceção em vez de payloads e remove a raiz do repositório dos erros operacionais retornados.

Quando o registro é feito pela extensão VS Code, há controles adicionais: `workspace.isTrusted` deve ser verdadeiro, pastas devem usar o scheme local `file:`, o servidor deve ser um arquivo regular que não seja symlink e esteja fora de todos os workspaces, e um caminho configurado no workspace é ignorado. Argumentos são passados como valores separados. O VS Code controla início e encerramento do processo; a ativação da extensão não inicia MCP.

## Menor privilégio e isolamento de workspace

Use um processo MCP por repositório. Em workspace multi-root do VS Code, o provider emite uma definição identificável por pasta habilitada, cada uma com seus próprios `cwd`, `--repository` e `--adr-directory`. Ele nunca concede a um processo uma raiz global abrangendo todas as pastas.

Habilite somente as pastas necessárias ao agente e revise a permissão do cliente para cada ferramenta. Desabilite `adrGuard.mcp.enabled` quando o acesso não for necessário. A desabilitação remove a definição pelo ciclo do provider sem afetar funcionalidades tradicionais da CLI.

As ferramentas atuais não escrevem. Elas não criam/atualizam baselines, índices, ADRs, mapeamentos, regras ou estado Git. `analyze_impact` lê o Git local sem fetch; `verify_architecture` consome evidência existente sem executar seu produtor.

## Conteúdo não confiável e recursos limitados

ADRs, Markdown, manifestos, evidências, saída Git e configuração são entradas não confiáveis. O servidor:

- marca Markdown e trechos de pesquisa retornados como não confiáveis;
- limita a descoberta a 1.000 ADRs e cada ADR a 512 KiB;
- limita a pesquisa a 256 caracteres imprimíveis e no máximo 100 resultados;
- limita entradas de evidência a 20 arquivos;
- valida contratos versionados de impacto, drift, evidência, baseline e configuração pelos serviços de domínio compartilhados;
- aplica teto de 1 MiB à resposta serializada e timeout configurável de 1 a 300 segundos;
- respeita cancelamento e trata evidência malformada, ausente, desatualizada ou contraditória de forma conservadora.

Um cliente ou agente não deve interpretar texto Markdown como instrução de ferramenta. Revise ações subsequentes e mantenha habilitadas as aprovações de ferramentas do cliente.

## Privacidade e credenciais

O MCP do ADR Guard não chama modelos e não exige API key. Ele não precisa das credenciais de provider usadas pelas funcionalidades separadas de rascunho/revisão da CLI. Não inclua segredos em argumentos MCP, configurações, logs, exemplos ou entradas de ambiente do cliente.

Um agente pode transmitir resultados conforme a política de privacidade do agente/provider. Antes de autorizar uma solicitação, considere se conteúdo de ADRs, nomes de arquivos, mapeamentos ou evidências podem sair da máquina. Configure e audite o agente separadamente; o limite MCP local não controla o uso dos dados devolvidos pelo cliente.

## Semântica dos resultados

- Impacto é consultivo e indica que uma decisão pode precisar de revisão.
- `unknown` e resultados inconclusivos de drift não são conformidade.
- `policyFailed` relata enforcement determinístico, mas não aprova nem rejeita uma ADR.
- Interpretação por IA é consultiva e não pode dispensar diagnósticos, estabelecer evidência nem mudar status.

Pessoas permanecem responsáveis por decisões arquiteturais, aprovação, revisão de segurança e mudanças no repositório.

## Incidentes e diagnóstico

Interrompa ou desabilite a definição no cliente se o escopo parecer incorreto. Verifique repositório e diretório de ADRs efetivos, caminho do binário, confiança do workspace, host remoto e histórico de permissões do cliente. Não copie credenciais nem payloads sensíveis completos para issues. Relate vulnerabilidades suspeitas por [SECURITY.md](../SECURITY.md), não por issue pública.
