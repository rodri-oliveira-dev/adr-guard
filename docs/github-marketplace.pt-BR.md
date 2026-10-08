# Checklist de publicação no GitHub Marketplace

Requisitos e estado da listagem originalmente conferidos em **27/09/2026**.

> **Atualização 07/10/2026:** O proprietário forneceu a [URL da listagem do ADR Guard no Marketplace](https://github.com/marketplace/actions/adr-guard-architecture-decision-validator). A verificação pública independente em sessão deslogada permanece pendente na #49. O novo teste de consumidor externo do `@v1` publicado foi aprovado (consulte as [evidências externas](github-action-external-verification.pt-BR.md)). As instruções e observações de 27/09/2026 abaixo são preservadas como histórico.

Referências oficiais:

- https://docs.github.com/pt/actions/how-tos/create-and-publish-actions/publish-in-github-marketplace
- https://docs.github.com/pt/actions/reference/workflows-and-actions/metadata-syntax
- https://docs.github.com/en/actions/how-tos/create-and-publish-actions/release-and-maintain-actions

## Identidade da listagem

Nome proposto no Marketplace:

`ADR Guard - Architecture Decision Validator`

Descrição:

`Validate, index, or explicitly review Architecture Decision Records (ADRs) with safe GitHub reporting.`

Branding:

- ícone: `shield`
- cor: `purple`
- categoria primária recomendada: **Code quality**
- categoria secundária recomendada: **Continuous integration**

As categorias são selecionadas na interface de release/Marketplace do GitHub e não fazem parte do `action.yml`.

Uma nova busca pública em 27/09/2026 não encontrou uma listagem verificável do ADR Guard no Marketplace nem uma URL canônica `github.com/marketplace/actions/...`. A validação definitiva de unicidade é feita pelo próprio GitHub no momento da publicação; se a interface indicar colisão de nome, a publicação deve ser interrompida. Até que um proprietário autorizado publique a Action e registre a URL canônica resultante, o repositório deve declarar que a publicação no Marketplace está incompleta.

## Requisitos do repositório

Estado atual:

- repositório: `rodri-oliveira-dev/adr-guard`
- visibilidade: pública
- metadata raiz: `action.yml`
- `action.yaml` alternativo na raiz: não utilizado
- licença: MIT
- documentação de consumo: inglês e pt-BR
- suporte: [../SUPPORT.md](../SUPPORT.md)
- segurança: [../SECURITY.md](../SECURITY.md)

Os requisitos oficiais do Marketplace para Actions exigem repositório público, um arquivo de metadata da Action na raiz e `name` único.

Este repositório contém o CLI, build do container, testes, scripts e documentação necessários para construir, publicar, validar e operar o ADR Guard. Não há outro arquivo de metadata da Action na raiz.

## Prontidão da release

O fluxo de release da Action:

- valida o commit no CI;
- publica a .NET Tool e os containers;
- verifica a imagem de runtime no GHCR;
- cria a tag imutável `vMAJOR.MINOR.PATCH`;
- avança `vMAJOR` apenas depois da publicação bem-sucedida;
- cria a GitHub Release depois que as tags da Action existem.

A listagem deve selecionar uma versão da Action associada a uma release. Não publique a partir de branch de desenvolvimento.

A referência de consumo suportada no Marketplace é `@v1`. A tag de compatibilidade `v1` real está publicada e o CI do repositório exercita continuamente essa referência pública para `check`/`index`; `review` está publicado na linha `v1` desde a `v1.1.6`. A issue #49 ainda exige seu rerun mais estrito em repositório independente antes que o roadmap do Marketplace possa ser considerado totalmente concluído.

A verificação externa pré-release passou em `rodri-oliveira-dev/poc-arquitetura`; consulte [github-action-external-verification.pt-BR.md](github-action-external-verification.pt-BR.md). O repositório também possui cobertura equivalente de produção em consumidor isolado contra o `@v1` público real. O rerun no repositório independente continua sendo um critério explícito da #49 para concluir o Marketplace, e não um pré-requisito para afirmar que a tag da Action é pública.

## Gates manuais do proprietário

As etapas abaixo dependem do proprietário da conta/repositório na interface web do GitHub e não são automatizadas:

- Confirmar que o nome proposto passa pela validação online de unicidade do Marketplace.
- Aceitar o **GitHub Marketplace Developer Agreement** na conta proprietária, caso ainda não tenha sido aceito.
- Garantir que a conta usada na publicação cumpra os requisitos de autenticação do GitHub, incluindo 2FA.
- Depois de existir uma tag de release da Action, abrir o `action.yml` raiz e usar o fluxo de publicação / draft release do Marketplace.
- Marcar **Publish this Action to the GitHub Marketplace**.
- Corrigir todos os avisos de metadata até o GitHub indicar validação bem-sucedida.
- Selecionar **Code quality** como categoria primária e **Continuous integration** como secundária quando essas categorias estiverem disponíveis na interface atual.
- Selecionar a tag exata da release que será publicada.
- Revisar preview, título, release notes, suporte, segurança e exemplo de consumo.
- Publicar a release/listagem.
- Abrir a página publicada do Marketplace em sessão deslogada e verificar instalação e links.
- Somente depois disso, substituir os avisos de "Marketplace futuro" dos READMEs e guias pela URL real.

## Verificação / badges

O badge de **verified creator** do Marketplace é separado da publicação comum de uma Action. A documentação do GitHub o descreve como um badge para organizações parceiras; ele não é requisito para publicar esta Action.

Não adicione badge do Marketplace nem afirme que a Action está disponível no Marketplace antes de a listagem existir e sua URL ser verificada.

## Verificações pós-publicação

Depois da publicação:

1. conferir nome, descrição, autor, branding shield/purple, categorias e versão na página do Marketplace;
2. validar um workflow consumidor limpo usando `@v1`;
3. confirmar que a Action resolve para a imagem major esperada no GHCR;
4. verificar links de suporte, segurança, licença, releases e documentação bilíngue;
5. atualizar [github-action.md](github-action.md), [github-action.pt-BR.md](github-action.pt-BR.md), `README.md` e `README.pt-BR.md` com a URL real do Marketplace.
