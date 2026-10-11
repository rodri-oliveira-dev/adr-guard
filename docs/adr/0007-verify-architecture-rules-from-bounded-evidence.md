# Verify architecture rules from bounded evidence

## Status

Accepted

## Context

Architecture-impact mappings identify decisions that may deserve review, but they do not prove conformance. AI review findings are advisory observations and likewise cannot safely become a CI compliance gate. Architectural drift needs a separate, versioned contract that connects an effective ADR to a narrowly defined assertion and inspectable evidence.

## Decision

ADR Guard will use the opt-in `adr-fitness-rules-v1` manifest (`schemaVersion: "1.0"`). Every rule has a stable rule ID, an ADR stable ID plus repository-relative path, explicit project scope, one supported check, an evidence source, declared failure and inconclusive outcomes, and an opt-in gate flag.

The v1 outcome vocabulary is:

- `pass`: complete deterministic evidence proves the declared condition;
- `violation`: complete deterministic evidence proves the condition false;
- `unknown`: evidence is missing, malformed, contradictory, stale, conditional, dynamic, or otherwise insufficient;
- `not-applicable`: the referenced decision is not currently effective under the configured lifecycle policy.

Only an accepted/effective ADR is evaluated. Proposed, rejected, deprecated, and effectively superseded ADRs are not silently enforced. Their rules produce `not-applicable`; unresolved identity or lifecycle produces `unknown`. Absence of evidence never produces `pass`.

Static `forbid-project-reference` checks inspect only explicitly scoped `.csproj` XML. They never invoke MSBuild, expand properties, import files, execute targets, or use the network. External-test checks consume only explicitly named, bounded JSON artifacts and preserve their reported provenance; a reported pass is evidence with a weaker confidence class than a local static proof.

Finding fingerprints are computed from normalized semantic identity (schema version, ADR stable ID, rule ID, check type, normalized scope and observed relationship), not rendered prose, timestamps, absolute paths, or JSON formatting. Reports sort ordinally by ADR stable ID, rule ID, scope, outcome, and fingerprint.

`gate` is an explicit declaration of intent but does not itself authorize failure. Enforcement is selected by the caller and may fail only for a proven deterministic `violation`. `unknown` remains visible and non-green but is not converted into a violation.

## Security boundaries

Manifests, project XML, and evidence JSON are untrusted input. Readers use strict UTF-8, reject duplicate or unknown JSON properties, cap bytes/count/depth, resolve only normalized repository-relative paths, reject symbolic-link/reparse traversal, disable DTD and external XML resolution, support cancellation, and never dereference URLs. Source code, credentials, and evidence are not transmitted externally.

## Compatibility

Fitness manifests and reports have independent v1 schemas. Existing `check`, `index`, `new`, `draft`, `review`, `impact`, JSON, SARIF, diagnostic baseline, and impact contracts remain unchanged. Fitness rules reuse the existing ADR identity, lifecycle, supersession, configuration, and repository-containment behavior rather than introducing a second ADR validator.

## Consequences

The first contract is deliberately narrow and reviewable. Conditional or property-expanded project references may be common, but reporting them as `unknown` preserves truthfulness. Teams retain responsibility for deciding which ADRs become executable rules and when an explicit baseline should change.
