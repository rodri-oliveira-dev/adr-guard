# Incremental validation and diagnostic baselines

> Availability: Git-aware incremental validation and diagnostic baselines are published in [ADR Guard v1.3.0](https://github.com/rodri-oliveira-dev/adr-guard/releases/tag/v1.3.0), including the former v1.4 development scope; both remain opt-in.

## Changed ADRs

```bash
adr-guard check docs/adr --changed --base-ref main
```

ADR Guard verifies the base as a commit, computes its merge base with `HEAD`, and combines committed branch changes, worktree changes, renames, deletions, and untracked ADRs. Git is started directly with an argument list; ref/file content is never interpolated into a shell.

Document-local structural debt in unchanged files is omitted in changed mode. Global integrity still runs over the complete current ADR set: duplicate IDs, broken references, supersession target/cycles/consistency, and explicit inactive dependencies cannot be skipped. Deleting or renaming a referenced ADR therefore still affects referring ADRs.

Full `check` remains the default. Changed mode returns operational exit code `3` when Git is missing, the directory is not in a repository, the base ref is invalid/unavailable, no merge base exists, history is too shallow, or cancellation occurs. ADR Guard never fetches automatically.

In GitHub Actions, use `actions/checkout` with `fetch-depth: 0` (pin to a reviewed commit SHA in production). Fork PRs can safely run this deterministic mode with `contents: read`; it requires no secrets. If the fork base ref is unavailable locally, run a full check instead of introducing a privileged fetch workflow.

## Baselines

Generate or explicitly update the strict [baseline schema v1](schemas/adr-diagnostic-baseline-v1.schema.json):

```bash
adr-guard baseline docs/adr --output .adrguard-baseline.json
adr-guard baseline docs/adr --output .adrguard-baseline.json --update
adr-guard check docs/adr --baseline .adrguard-baseline.json
```

Fingerprints are SHA-256 over diagnostic code, normalized repository-relative file, and message. A rename or diagnostic message change is therefore visible as one resolved and one new finding. Deletion produces resolved findings; exact recurrence matches the retained entry again.

Baseline reports show `new`, `existing`, and `resolved` counts in text, JSON, and SARIF. Only exact known document-local findings can be classified as existing. **ADR006–ADR008 and ADR010–ADR014** are global integrity diagnostics and remain mandatory/new even if manually inserted into a baseline. **ADR009** (duplicate canonical section) is document-local and can be recognized as existing. With `--changed --baseline`, `new` and `existing` counts use the selected diagnostic set, while `resolved` always compares the full validation result so unchanged-file findings are not falsely reported as resolved. Invalid/incompatible JSON, IO/Git/provider failures, and cancellation remain errors. Baseline files change only through the explicit `baseline` command; writes are atomic and never replace symlinks.

For gradual adoption, review and commit the generated JSON. CI should consume it read-only. Updating it should be a deliberate reviewable change, not an automatic step in validation.
