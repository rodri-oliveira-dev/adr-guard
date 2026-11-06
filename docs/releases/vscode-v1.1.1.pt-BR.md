# ADR Guard para VS Code 1.1.1 — candidata a release

[English](vscode-v1.1.1.md)

ADR Guard para VS Code `1.1.1` está preparada, mas **não foi publicada**. A versão pública atual no Marketplace continua sendo `1.1.0`, verificada pela Gallery API do Marketplace em 05/11/2026.

Esta candidata preserva todos os comandos e configurações existentes, VS Code `1.101.0+`, contratos de criação/edição visual no schema `1.0` e o provider MCP opcional e desabilitado por padrão. Ela incorpora melhorias em caminhos, mensagens limitadas, manutenção e testes de regressão, e adiciona um comando somente leitura para o grafo de relacionamentos de ADRs sem incluir binários da CLI ou do MCP no VSIX.

O grafo consome exclusivamente o schema `1.0` de relacionamentos da CLI, usa layout determinístico em camadas limitado a 200 ADRs e 400 relacionamentos, diferencia dependências/substituições e estados resolvido/não resolvido/ambíguo e renderiza textos não confiáveis do contrato com APIs seguras de DOM sob uma CSP restritiva de recursos locais. Ele apresenta explicitamente carregamento, vazio, ausência de vínculos, CLI incompatível, erro operacional e limite de renderização. A capacidade permanece disponível apenas no código-fonte até que a candidata da extensão e uma CLI contendo `relationships` sejam publicadas.

A extensão continua executando uma CLI compatível instalada separadamente. A CLI `1.10.0` publicada fornece o conjunto de comandos atualmente publicado; verificações de capacidade e fallbacks seguros permanecem ativos. O branch de código-fonte adiciona o contrato JSON `relationships` depois do baseline preparado da CLI `1.11.0`, portanto o grafo apresenta o estado de CLI incompatível com os pacotes publicados atualmente. MCP `1.0.1` é opcional, instalado de forma independente e publicado separadamente.

Após o merge, execute **VS Code VSIX Package** na `main` para gerar e validar `adr-guard-1.1.1.vsix`. O workflow não publica no Marketplace. A publicação no Marketplace exige uma ação separada e explicitamente autorizada da pessoa proprietária, com autenticação do Marketplace, seguida da confirmação de que `rodrioliveira.adr-guard` informa `1.1.1`.
