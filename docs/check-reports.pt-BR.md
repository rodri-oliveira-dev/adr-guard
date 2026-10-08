# JSON, SARIF e GitHub Code Scanning

`adr-guard check --format json` emite o [schema v1 do relatório do ADR Guard](schemas/adr-check-report-v1.schema.json). Diagnósticos são ordenados por arquivo, código e mensagem. `valid` representa a validação estrutural; resumo e lista de arquivos tornam explícitos os relatórios vazios/de sucesso.

`adr-guard check --format sarif` emite SARIF 2.1.0. Cada diagnóstico vira um resultado com `ruleId` estável, mensagem, severidade e URI do artefato adequada ao workspace. Como o ADR Guard não possui posições Markdown confiáveis, ele não inventa linha/região.

Mantenha a validação normal somente leitura. Para upload no Code Scanning, use um job/etapa separado e conceda `security-events: write` apenas a ele:

```yaml
permissions:
  contents: read

jobs:
  adr-sarif:
    permissions:
      contents: read
      security-events: write
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v7
        with:
          persist-credentials: false
      - uses: actions/setup-dotnet@v6
        with:
          global-json-file: global.json
      - run: dotnet tool restore
      - name: Produzir SARIF mesmo com falha de validação
        shell: bash
        run: |
          set +e
          dotnet tool run adr-guard check docs/adr --format sarif > adr-guard.sarif
          status=$?
          test "$status" -eq 0 || test "$status" -eq 1
      - uses: github/codeql-action/upload-sarif@v4
        with:
          sarif_file: adr-guard.sarif
```

Em produção, fixe actions de terceiros em SHAs revisados. PRs de forks podem não ter permissão para upload de security events; execute o check somente leitura em forks e faça upload apenas em workflow confiável que nunca faça checkout nem execute código não confiável com credenciais elevadas.
