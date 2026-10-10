# Analyze architecture impact from explicit local mappings

## Status

Accepted

## Context

ADR Guard can validate decision records and inspect ADR changes relative to Git, but it cannot currently show which decisions may be relevant when ordinary repository files change. Inferring that relationship from arbitrary source code, names, or AI output would be non-deterministic, difficult to review, and unsafe for CI.

Phase 0 also separated stable ADR identity from filenames, ordinary Markdown links from decision relationships, and lifecycle state from human approval. Architecture-impact analysis must preserve those boundaries and must not change the existing check JSON/SARIF contracts.

## Decision

Architecture impact will be an opt-in, local-only analysis driven by a strict JSON manifest named explicitly by the caller. The version 1.0 manifest schema is `docs/schemas/adr-impact-map-v1.schema.json`.

Each mapping owns one declared decision reference:

- `stableId` is the normalized Phase 0 identity, such as `ADR-1` or `slug:use-cache`;
- `path` is the forward-slash repository-relative ADR path and protects against ambiguous or stale identifiers;
- `patterns` are bounded repository-relative coverage patterns using only literal segments, `*` within one segment, and `**` as a complete segment;
- `relationship` is one of `governs`, `implements`, `constrains`, or `depends-on`;
- `reason` is human-authored review evidence and never an executable predicate.

Both identity and path must resolve to the same discovered ADR. A missing, renamed, duplicated, rejected, deprecated, superseded, or otherwise inactive target is preserved as `unknown`; it is never silently treated as compliance or as a violation. Proposed decisions may be reported as affected candidates, explicitly labelled non-effective. Effective-versus-pending supersession follows the Phase 0 lifecycle model.

Git scope is explicit. The caller supplies a base ref. Analysis uses the merge base against `HEAD`, then combines committed, staged, unstaged, and untracked changes. Rename/copy records retain old and new paths; deletes retain the old path. ADR Guard does not fetch, checkout, mutate Git state, call GitHub, or use credentials. Missing history, invalid refs, truncated output, and partial Git failures are operational errors rather than empty change sets.

Correlation produces candidates, not verdicts. A mapped changed path yields `affected` with inspectable evidence: change kind, current/previous path, matched pattern, relationship, and mapping reason. Changes with no mapping are `not-matched` and are disclosed as uncovered. Invalid mappings, unresolved decisions, or incomplete Git evidence produce `unknown`. Output ordering is ordinal and reproducible by stable ADR identity, ADR path, changed path, evidence type, and pattern.

Impact JSON will use an independent `schemaVersion: "1.0"`. It will not add fields to the existing check JSON v1.0, SARIF, diagnostic baseline, or review schemas. Text and JSON reports are bounded and disclose repository-relative paths but never source content. The successful presence of affected decisions does not change exit status: impact is advisory and requires human review.

## Security and compatibility acceptance matrix

| Concern | Accepted behavior | Rejected behavior |
| --- | --- | --- |
| Activation | Explicit manifest and base ref | Implicit repository scan or hidden fetch |
| ADR identity | Stable ID and path resolve to one document | Filename-only guesses, ambiguity, stale target |
| Paths | Normalized repository-relative paths contained physically in checkout | Absolute paths, traversal, symlink/reparse escape |
| Patterns | Literals, segment `*`, complete-segment `**`, bounded size/count | Regex, brace expansion, shell syntax, executable rules |
| Lifecycle | Advisory candidate with effective/proposed/inactive context | Approval, violation claim, automatic status change |
| Git | Merge-base plus committed/staged/unstaged/untracked inventory | Network, checkout, credentials, misleading empty diff |
| Evidence | Path, change kind, matched pattern, relationship and reason | Source contents, inferred intent, fabricated evidence |
| Contracts | Independent manifest/report schema 1.0 | Mutation of check JSON/SARIF or published defaults |
| Failure | Usage or operational error with actionable message | Partial success presented as safe/not affected |

## Limits

Version 1.0 limits manifest size, mapping count, patterns per mapping, field lengths, Git output, changed entries, and rendered evidence. Case behavior follows the local platform and repository rules; exact collision checks use the effective path comparer. External repositories, submodule traversal, network dereferencing, code parsing, policy enforcement, and AI inference are out of scope.

## Consequences

Mappings are reviewable alongside code and reusable by CLI, Action, and later drift analysis without coupling the domain service to an interface. Teams must maintain mappings explicitly, and incomplete coverage remains visible. The design favors deterministic evidence and safe uncertainty over broad but unverifiable inference.
