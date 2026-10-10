# Architecture impact analysis

`adr-guard impact` is an opt-in, read-only analysis that correlates Git changes with explicit ADR-to-code mappings. It is advisory: an affected decision is a review prompt, not a policy violation or proof of non-compliance.

```bash
adr-guard impact . --base-ref origin/main --map .adrguard-impact.json
adr-guard impact . --base-ref origin/main --map .adrguard-impact.json --format json
```

The base reference and mapping file are always explicit. The command does not fetch, checkout, modify files, or change Git state. It inventories committed changes since the merge base plus staged, unstaged, and untracked paths. A successful analysis returns `0` even when ADRs are affected or changes are not mapped; invalid arguments return `2`, and repository, Git, manifest, bounds, or cancellation failures return `3`.

## Contracts and compatibility

The mapping contract is [`adr-impact-map-v1.schema.json`](../schemas/adr-impact-map-v1.schema.json). Paths use repository-relative forward slashes and patterns support literals, `*` within one segment, and a complete `**` segment. ADR identity is resolved from the declared stable ID and path; unresolved, inactive, renamed, or lifecycle-ambiguous mappings are reported as `unknown` rather than silently treated as current governance.

JSON output follows [`adr-impact-report-v1.schema.json`](../schemas/adr-impact-report-v1.schema.json); see the [reproducible example](../examples/adr-impact-report-v1.json). Field names and meanings are versioned by `schemaVersion: "1.0"`. Additive fields require a compatible evolution; removals or semantic changes require a new schema version. This contract does not change existing `check` JSON or SARIF schemas.

Reports are bounded to 10,000 Git changes, 1,000 mappings, 100 patterns per mapping, 1,000,000 match evaluations, 5,000 evidence entries, and 500 evidence entries per decision. Git command output, execution time, and manifest bytes are also bounded.

## Security and disclosure

Output can disclose repository-relative paths plus mapping reasons and ADR titles. Treat all source-derived values as untrusted when forwarding JSON or text to logs, workflow commands, HTML, or other tools. The command never reads file contents as evidence and does not send data over the network. JSON is written only to stdout; operational errors are written only to stderr.
