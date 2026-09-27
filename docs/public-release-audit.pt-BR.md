# Auditoria da release e distribuição públicas

[English](public-release-audit.md)

Este documento registra a reconciliação de release/distribuição realizada para a issue #80 em **27/09/2026**. Ele separa artefatos públicos verificáveis mecanicamente da publicação no GitHub Marketplace, que ainda exige ação do proprietário do repositório.

## Snapshot verificado da release

No início desta auditoria:

| Superfície | Estado verificado |
| --- | --- |
| GitHub Release | `v1.1.6`, publicada a partir do commit `1b2a75207801526a7564c706b40a92f0eb2ff7a5` |
| Workflow de release | Run `36356961234` concluído com sucesso; jobs de NuGet.org, GitHub Packages, containers, tags da Action e GitHub Release passaram |
| Referência móvel da Action | `refs/tags/v1` resolve para `1b2a75207801526a7564c706b40a92f0eb2ff7a5`, o mesmo commit da `v1.1.6` |
| NuGet.org | O pipeline da release publicou `RodriOliveira.AdrGuard 1.1.6`; o CI agora instala diretamente do NuGet.org a versão da latest GitHub Release e executa `new`, `check` e `index` |
| GitHub Packages | O job da release `v1.1.6` publicou com sucesso. O consumo continua sendo um caminho autenticado do GitHub Packages, conforme documentado no README |
| GHCR | O pipeline publicou e verificou imagens exata/minor/major/latest; o CI agora baixa a tag pública exata mais recente e a major móvel e executa smoke tests em ambas |
| Docker Hub | O pipeline publicou e verificou a mesma release; o CI agora baixa a tag pública exata mais recente e a major móvel e executa smoke tests em ambas |
| GitHub Action | O `@v1` público é exercitado em fixtures isoladas de consumidor para `check` e `index`; `review` está publicado na linha `v1` desde a `v1.1.6` |
| GitHub Marketplace | **Nenhuma listagem pública canônica do ADR Guard no Marketplace foi verificada.** Ainda não se deve afirmar disponibilidade no Marketplace |

Links públicos de referência:

- GitHub Releases: https://github.com/rodri-oliveira-dev/adr-guard/releases
- `v1.1.6`: https://github.com/rodri-oliveira-dev/adr-guard/releases/tag/v1.1.6
- NuGet.org: https://www.nuget.org/packages/RodriOliveira.AdrGuard
- GitHub Packages: https://github.com/rodri-oliveira-dev?tab=packages
- pacote GHCR: https://github.com/rodri-oliveira-dev/adr-guard/pkgs/container/adr-guard
- Docker Hub: https://hub.docker.com/r/rodrigodotnet/adr-guard
- Release run #29: https://github.com/rodri-oliveira-dev/adr-guard/actions/runs/36356961234

## Versão móvel versus baseline do repositório

`src/AdrGuard/AdrGuard.csproj` mantém intencionalmente `VersionPrefix=1.1.0`. O workflow de release usa esse valor como **baseline da série de releases**, compara com tags imutáveis de release/reserva e incrementa o patch nas releases seguintes.

Portanto:

- `VersionPrefix` não representa a versão pública atual;
- o empacotamento local do CI pode produzir `1.1.0` intencionalmente para testes determinísticos do baseline;
- o patch público mais recente deve ser obtido pelas superfícies públicas GitHub Releases/NuGet;
- a documentação deve registrar versões em que recursos surgiram, mas não deve fixar um “release atual” móvel fora de um snapshot de auditoria.

Isso evita criar uma release desnecessária apenas para fazer o metadata fonte parecer igual ao patch mais recente.

## Política de disparo das próximas releases

O snapshot da `v1.1.6` acima foi produzido pelo trigger automático pós-CI anterior. O PR #86 altera a política de publicação das próximas releases: merge na `main` ou CI verde **não publica artefatos**.

A publicação futura só começa quando um mantenedor autorizado executar manualmente **Actions → Release → Run workflow** com a branch `main` selecionada. O workflow `Release` passa a usar somente `workflow_dispatch`, rejeita dispatches fora da `main`, verifica que o `github.sha` exato selecionado já possui uma execução `CI` de push concluída com sucesso, vincula a release a esse commit e executa novamente build/testes/smoke do pacote antes de iniciar publicação em NuGet, packages, containers, tags da Action ou GitHub Release.

## Verificação contínua dos artefatos públicos

`scripts/public-distribution-smoke-test.sh` transforma a auditoria em um gate reproduzível do CI. Ele:

1. resolve dinamicamente a latest GitHub Release estável;
2. verifica que a tag móvel `vMAJOR` da Action resolve para o mesmo commit da última tag exata;
3. instala do NuGet.org, sem cache, essa versão pública exata;
4. executa `new`, `check` e `index` com a ferramenta pública em diretório temporário limpo;
5. baixa e executa as imagens exata e major móvel do GHCR;
6. baixa e executa as imagens exata e major móvel do Docker Hub;
7. confirma que a major móvel do container corresponde à release exata mais recente para a plataforma do runner.

O job de contrato da GitHub Action já existente executa separadamente a referência pública real `rodri-oliveira-dev/adr-guard@v1` em fixtures isoladas de consumidor, exercitando `check` e `index` sem depender do source local da Action.

## Estado do Marketplace e blocker manual

Uma nova busca pública durante esta auditoria não encontrou uma listagem verificável do ADR Guard no Marketplace, e não existe URL canônica de Marketplace registrada nas evidências do repositório. Portanto o estado correto continua sendo: **a publicação no Marketplace não está concluída**.

O trabalho restante não pode ser representado honestamente como automatizado por este PR. Um proprietário autorizado da conta/repositório precisa usar o fluxo de publicação do Marketplace no GitHub, satisfazer os requisitos de acordo da conta/2FA/validação de nome apresentados pela interface, selecionar uma release elegível, publicar a Action e então verificar a página resultante em sessão deslogada.

Somente depois desse passo manual o repositório deve:

- registrar a URL canônica exata `github.com/marketplace/actions/...`;
- atualizar README/guias de consumo com a listagem real;
- executar novamente o consumidor independente `poc-arquitetura` contra o `@v1` publicado caso se queira encerrar o critério mais estrito da #49;
- concluir os critérios de Marketplace nas issues #49/#50.

Até lá, a ausência de uma URL do Marketplace é deliberada e verdadeira.

## Reconciliação dos roadmaps históricos

- #49 foi encerrada enquanto a própria evidência ainda dizia que a publicação no Marketplace e o rerun independente pós-release com `@v1` estavam pendentes. Ela deve permanecer/reabrir como follow-up manual de Marketplace.
- #50 foi encerrada com a #49 e os critérios globais de Marketplace ainda desmarcados. Ela deve permanecer/reabrir até a #49 estar realmente concluída.
- #59 entregou o roadmap de templates da `v1.1.0` e as releases públicas posteriores demonstram que o fluxo coordenado funciona. Seu bookkeeping antigo com checkboxes não marcados é referenciado pela #80, e não tratado como evidência de implementação ausente.
- #73 separou corretamente o estado publicado do `@v1` da publicação no Marketplace.
- #74 tratou corretamente a verificação dos artefatos públicos como gate pós-merge; a automação atual publicou desde então a linha `v1.1.x` com sucesso.

## Regra para promoção

Promova somente o que pode ser consumido agora:

- CLI/.NET Tool: público;
- GitHub Release: pública;
- imagens GHCR e Docker Hub: públicas;
- Action reutilizável `@v1`: pública;
- listagem no GitHub Marketplace: **ainda não verificada/evidenciada publicamente**.

O gate de distribuição pública no CI é a fonte verificável por máquina para os quatro primeiros itens. O Marketplace continua sendo um marco manual com evidência separada.
