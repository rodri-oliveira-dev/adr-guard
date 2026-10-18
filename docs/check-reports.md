# JSON, SARIF, and GitHub Code Scanning

> `--diagnostic-locations` is currently available only in source builds and is not included in the published CLI 1.8.0 package. The existing JSON and SARIF output remains the published default.

`adr-guard check --format json` emits the versioned [ADR Guard check report v1 schema](schemas/adr-check-report-v1.schema.json). Diagnostics are ordered by file, code, and message. `valid` reflects structural validation; the summary and file list make empty/success reports explicit. Add `--diagnostic-locations` to opt in to optional one-based `line` and `column` properties when the validator can identify a source location from parsed Markdown. The properties are omitted together when the evidence identifies only the file. Without the flag, output remains compatible with strict schema `1.0` clients that predate these optional properties.

`adr-guard check --format sarif` emits SARIF 2.1.0. Each ADR diagnostic becomes a result with its stable `ruleId`, message, severity, and workspace-appropriate artifact URI. With `--diagnostic-locations`, known positions become a `region`; file-only diagnostics omit it. ADR Guard never invents a line or column when the validator lacks location evidence.

For `ADR008`, an existing but empty or unusable `Superseded by` section is located at its parsed heading. When the section itself is absent, the finding remains file-only because no exact insertion point can be proven.

Keep the normal validation job read-only. If Code Scanning upload is desired, use a separate job/step and grant only that job `security-events: write`:

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
      - name: Produce SARIF even when ADR validation fails
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

Pin third-party actions to reviewed commit SHAs in production. Fork pull requests may not have permission to upload security events; run the read-only check for forks and upload only in a trusted workflow that never checks out or executes untrusted code with elevated credentials.
