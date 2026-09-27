# Segurança e limites de confiança do review de ADR

[English](adr-review-security.md)

A revisão assistida por IA processa documentação do repositório por meio de um modelo externo. Trate cada ADR, context file, resposta do provider e contribuição de fork como entrada não confiável.

## Limite de execução

`adr-guard review` não expõe ferramentas ao modelo. O material revisado não pode acionar:

- leitura arbitrária do filesystem;
- fetch de URL ou rede;
- comandos externos ou shell;
- mudanças em ADR/índice/status;
- escrita de arquivos além de um relatório local solicitado explicitamente;
- acesso a secrets ou variáveis de ambiente.

O material fonte é serializado em um envelope de dados JSON com flags de capabilities explicitamente definidas como `false`. Instruções embutidas em ADR ou contexto selecionado permanecem dados inertes e não alteram o contrato de review nem a avaliação determinística da policy.

O enforcement determinístico de `--policy enforce` é avaliado localmente antes da chamada ao provider. Linguagem do modelo não pode ignorar, criar nem satisfazer regras de policy.

## Credenciais e redação

Credenciais de provider são lidas apenas de variáveis de ambiente:

- `OPENAI_API_KEY`
- `ANTHROPIC_API_KEY`
- `GEMINI_API_KEY`
- `ADR_GUARD_OPENAI_COMPATIBLE_API_KEY`

Como defesa adicional, `GITHUB_TOKEN` e `GH_TOKEN` também são tratados como sensíveis quando presentes.

Valores literais exatos, não vazios, dessas variáveis são substituídos por `[REDACTED]` no conteúdo fonte selecionado antes da composição do contexto do provider e também em findings, diagnósticos e relatórios persistidos derivados do provider. Valores codificados, correspondências parciais e secrets de variáveis não listadas não são detectados. Credenciais nunca são colocadas intencionalmente no corpo do request ao provider. Headers de autenticação permanecem metadados de transporte exigidos pelo provider selecionado.

Providers oficiais usam endpoints HTTPS fixos. Endpoints remotos OpenAI-compatible exigem HTTPS. HTTP sem TLS só é permitido em loopback sem autenticação; qualquer endpoint que transporte API key precisa usar HTTPS, inclusive loopback.

## Saída do provider

Respostas do provider são não confiáveis. O ADR Guard:

- limita respostas HTTP de sucesso a 1 MiB;
- rejeita JSON de review malformado ou incompleto;
- rejeita propriedades JSON inesperadas;
- limita o número de findings;
- limita os campos source, excerpt, explanation e guidance;
- normaliza caracteres de controle e separadores de linha antes da renderização;
- neutraliza delimitadores `::` de workflow commands em texto derivado do provider;
- nunca transforma falha, recusa, timeout, rate limit ou resposta malformada do provider em “sem problemas” ou aprovação.

Timeout e cancelamento do chamador continuam distintos. Falhas de transporte/provider usam diagnósticos controlados que não incluem body da resposta, headers de autorização nem detalhes brutos de exceção de rede.

## Divulgação de contexto e processamento externo

Por padrão, o review envia somente o ADR selecionado. Context files adicionais exigem `--context-file` explícito; descoberta de ADRs existentes exige `--include-existing-adrs`. Conteúdo de ADRs existentes e contexto final composto permanecem limitados.

O material selecionado é transmitido ao provider externo configurado e pode sair da máquina, organização ou região. Retenção, logging, residência de dados, treinamento e termos de processamento são controlados por esse provider. Não envie material arquitetural sensível se os termos do provider ou as políticas da organização não permitirem.

A divulgação local de fontes lista os filenames selecionados que serão enviados. O ADR Guard não varre silenciosamente código-fonte, histórico git, variáveis de ambiente como contexto, URLs arbitrárias nem arquivos não relacionados.

Relatórios de review podem conter trechos curtos de evidência ou observações arquiteturais derivadas do provider. Trate relatórios persistidos e artefatos de CI com as mesmas regras de repositório/classificação de dados usadas para os ADRs revisados. Redação de secrets é defesa em profundidade, não substitui controle adequado de acesso aos artefatos.

## Pull requests de comunidade e forks

Não exponha credenciais de provider a código ou workflows não confiáveis de forks. A integração publicada de review da GitHub Action aplica estes limites:

- `review` em `pull_request_target` é rejeitado antes da execução do Docker/provider;
- `review` em `pull_request` vindo de fork é rejeitado antes da execução do Docker/provider;
- `GITHUB_TOKEN` e `GH_TOKEN` nunca são encaminhados ao container de review;
- somente a variável de credencial do provider selecionado pode ser encaminhada pelo ambiente do step;
- o checkout permanece somente leitura;
- não são criados comentários automáticos no PR;
- `permissions: contents: read` é suficiente.

Para contribuições não confiáveis, execute `check` determinístico no PR e faça review com provider somente após o merge ou em workflow manual/confiável separado.

Um review com provider é orientação para um revisor humano, não certificação de segurança nem aprovação arquitetural.

Consulte também [Review por IA na GitHub Action](github-action-review.pt-BR.md).
