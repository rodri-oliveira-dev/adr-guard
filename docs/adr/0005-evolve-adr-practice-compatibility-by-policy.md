# Evolve ADR practice compatibility by explicit policy

## Status

Accepted

## Context

ADR Guard currently couples several independent concepts: a four-digit file prefix is the ADR identity, every local Markdown link is validated as if it were an ADR relationship, lifecycle values are fixed, and validation strictness follows the selected document format. Those defaults are published behavior used by the CLI, GitHub Action, VS Code extension, JSON and SARIF consumers, incremental checks, baselines, generation, and indexing.

Teams also use legitimate ADR practices with different file names, lifecycle labels, supersession notation, and optional accountability metadata. The architecture-decision-record project documents useful conventions, but it is guidance rather than a single normative schema.

## Decision

We will evolve compatibility through a versioned, bounded policy model while preserving the current canonical and MADR 4.0 defaults.

The model keeps these concepts separate:

1. A document link is a Markdown navigation link. An ADR relationship is an explicitly declared, typed graph edge. Local document links are resolved within a declared repository root; ADR edges are resolved only against the validated ADR set. External URLs and anchors are never dereferenced.
2. Lifecycle status has a normalized semantic kind and an original display value. The legacy four statuses remain the default. `Rejected`, aliases, and organization-defined in-progress states require an explicit lifecycle policy. Tools may validate status but never approve, reject, or mutate a decision.
3. Stable ADR identity is distinct from its display file name. The legacy `NNNN-kebab-case.md` convention and allocator remain the default. Alternate numbered, prefixed, variable-width, and unnumbered forms are opt-in and must reject ambiguous identities.
4. Document format (`canonical` or `madr-4`) selects parsing and structural shape. A validation profile independently selects deterministic rule severity. Profiles use a closed rule catalog; they cannot execute code or arbitrary predicates.
5. Relationship normalization records type, target, effectiveness, and provenance. A proposed replacement is pending until its successor is active. Historical ADRs are never rewritten automatically.
6. Optional metadata records user-supplied provenance and traceability. It cannot imply approval. Parsing is bounded, rejects duplicate keys, and does not support YAML evaluation, tags, anchors, or network lookup.
7. The existing index remains byte-for-byte stable by default. An enriched governance catalog is an explicit output mode with deterministic ordering and escaping.

New policy data uses `.adrguard.yml` schema version 1 with a closed set of scalar and bounded-list properties. Command-line values override repository configuration. Unknown properties and values fail with a usage/configuration diagnostic before validation. Existing diagnostic identifiers `ADR001` through `ADR014`, JSON/SARIF schemas, baseline fingerprints, and exit-code behavior remain stable. New opt-in checks receive new identifiers and are added without changing existing meanings.

Path inputs are untrusted. Resolution is lexical and physical where the platform permits, rejects traversal and symbolic-link/reparse-point escapes, and never reads outside the repository boundary. Markdown parsing ignores fenced code examples, strips query and fragment suffixes for local path checks, and remains deterministic. No feature executes document or configuration content.

## Consequences

Existing users retain current results without configuration changes. Adoption of alternate practices is deliberate and reviewable, and integrations share one core policy rather than duplicating rules.

The policy surface and fixture matrix require more tests. Some conventions cannot be combined safely; ambiguous identities and conflicting status declarations will produce actionable diagnostics instead of guesses. Migration is report-only: ADR Guard will explain incompatible files but will not rename or rewrite historical decisions.

## Compatibility and fixture matrix

| Area | Default regression fixture | Opt-in fixture | Malformed or hostile fixture |
| --- | --- | --- | --- |
| Canonical | Four statuses, `NNNN-slug.md`, sections | Rejected/custom lifecycle | Duplicate status, empty content |
| MADR 4.0 | Existing front matter and sections | Named lifecycle values | Conflicting or duplicate metadata |
| Links | ADR edge and ordinary local document | Nested paths, spaces, titles, fragments | Traversal, symlink/reparse escape, malformed link |
| Supersession | Section-based reciprocal pair and chain | Conventional status text, pending successor | Self, cycle, multiple, ambiguous target |
| Identity | Four-digit allocation | unnumbered, `ADR-`, variable width | zero, overflow, collision, Unicode/case ambiguity |
| Hygiene | Existing validation unchanged | EN/PT placeholders with configured severity | fenced examples, interpolation false positives |
| Metadata | Absent metadata | dates, actors, requirements, follow-ups | duplicate, oversized, YAML features |
| Profiles | canonical/MADR current behavior | strict, standard, advisory, legacy | unknown profile/rule/severity |
| Catalog | Existing README bytes | enriched Markdown catalog | escaping, missing fields, 1,000 ADRs |
| Integrations | CLI, Action, extension, JSON/SARIF | policy arguments/config | incremental and baseline stability |

