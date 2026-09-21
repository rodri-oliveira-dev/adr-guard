# Política de release da GitHub Action

O ADR Guard publica sua GitHub Action a partir do mesmo commit validado e da mesma versão de release usados pela .NET Tool e pelas imagens de container.

## Referências publicadas da Action

Cada release bem-sucedida publica duas referências da Action:

- `vMAJOR.MINOR.PATCH` — imutável. É criada uma única vez e deve sempre apontar para o commit validado daquela release exata.
- `vMAJOR` — referência móvel de compatibilidade. Avança para a release bem-sucedida mais recente daquela major e nunca volta para trás quando uma release antiga é reexecutada.

Exemplos:

```yaml
# Source da Action e versão de runtime reproduzíveis.
uses: rodri-oliveira-dev/adr-guard@v1.2.3

# Recebe atualizações compatíveis dentro da major 1.
uses: rodri-oliveira-dev/adr-guard@v1
```

A Action nunca faz fallback para `latest`. Uma tag exata da Action seleciona a tag exata correspondente no GHCR (`@v1.2.3` -> `:1.2.3`). Uma tag major móvel da Action seleciona a tag major móvel correspondente da imagem (`@v1` -> `:1`).

Também é possível fixar o source da Action por SHA de commit. Como o SHA não codifica a versão do container, esse modo exige o input `version` exato:

```yaml
uses: rodri-oliveira-dev/adr-guard@<commit-sha>
with:
  path: docs/adr
  command: check
  version: 1.2.3
```

## Ordem da release

O workflow de release executado após o CI preserva o commit validado pelo workflow `CI` bem-sucedido e segue esta ordem:

1. faz build, testes e empacotamento do commit validado;
2. reserva o SemVer resolvido com uma tag interna `release-reservation/vMAJOR.MINOR.PATCH` vinculada ao commit validado;
3. publica a .NET Tool no NuGet.org;
4. publica o pacote no GitHub Packages;
5. publica o container multi-plataforma no GHCR e Docker Hub, incluindo tags exata, minor, major e `latest`, além de attestations de SBOM/provenance;
6. verifica que as referências exata e major do GHCR resolvem para o digest OCI produzido pela release;
7. executa smoke test da resolução de runtime da Action pelas referências exata e major;
8. cria ou verifica a tag Git imutável `vMAJOR.MINOR.PATCH`, atualiza `vMAJOR` e remove a reserva concluída;
9. cria a GitHub Release.

As tags da Action só são publicadas depois que o job de container termina com sucesso. Assim, uma falha na publicação do container não consegue expor uma nova tag da Action cujo runtime ainda não exista. A tag interna de reserva não é uma referência suportada da Action e impede que um commit posterior reutilize um SemVer que possa ter artefatos parcialmente publicados.

## Idempotência e conflitos

A tag SemVer exata é imutável. Em uma reexecução:

- se `vMAJOR.MINOR.PATCH` já aponta para o commit validado, ela é reutilizada;
- se aponta para qualquer outro commit, a release falha em vez de repontá-la;
- se `vMAJOR` já aponta para a mesma release, nada é alterado;
- se `vMAJOR` aponta para uma release mais antiga da mesma major, ela avança;
- se uma release antiga for reexecutada depois de uma release mais nova, `vMAJOR` permanece na mais nova;
- se a tag major existente não puder ser associada a uma tag imutável de release, o workflow se recusa a sobrescrevê-la.

Uma GitHub Release já existente também é tratada como imutável. Um asset ausente pode ser completado, mas um asset existente não é substituído com `--clobber`.

## Verificar uma release

Para a release `v1.2.3`, primeiro confira as tags Git:

```bash
git ls-remote --tags https://github.com/rodri-oliveira-dev/adr-guard.git \
  refs/tags/v1.2.3 refs/tags/v1
```

Logo após a release, ambas devem resolver para o mesmo commit. A tag exata deve continuar apontando para esse commit para sempre; a tag major pode avançar em releases futuras `v1.x.y`.

Confira as imagens de runtime:

```bash
docker buildx imagetools inspect ghcr.io/rodri-oliveira-dev/adr-guard:1.2.3
docker buildx imagetools inspect ghcr.io/rodri-oliveira-dev/adr-guard:1
```

Imediatamente após a publicação, as duas referências devem exibir o mesmo digest OCI de nível superior. Releases posteriores podem mover `:1`, enquanto `:1.2.3` permanece imutável.

Por fim, valide a Action em um repositório consumidor:

```yaml
permissions:
  contents: read

steps:
  - uses: actions/checkout@<commit-fixado>
    with:
      persist-credentials: false

  - uses: rodri-oliveira-dev/adr-guard@v1.2.3
    with:
      path: docs/adr
      command: check
```

Para detalhes de supply chain e isolamento de runtime, consulte o [modelo de segurança da GitHub Action](github-action-security.pt-BR.md) e o [guia de container e supply chain](container.pt-BR.md).
