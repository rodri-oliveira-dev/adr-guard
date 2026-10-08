# Deterministic ADR relationship governance

ADR Guard validates only relationships that authors declare explicitly. This keeps governance inspectable and avoids treating architectural judgment as a blocking rule.

Recognized declarations are Markdown links under level-two `Superseded by`, `Supersedes`, `Depends on`, or `Dependencies` headings, plus MADR `status: "superseded by ADR-NNNN"`. Narrative links elsewhere still receive broken-reference validation but do not become dependency/supersession semantics.

| Code | Objective invariant |
| --- | --- |
| `ADR010` | A decision is part of a directed supersession cycle. |
| `ADR011` | A decision declares itself as its superseding decision. |
| `ADR012` | A decision declares more than one distinct superseding target. |
| `ADR013` | Explicit supersession direction conflicts with status or a named target. |
| `ADR014` | An explicit dependency targets a `Deprecated` or `Superseded` decision. |

`ADR001`–`ADR009` retain their published meanings. Broken local Markdown/MADR targets remain `ADR007`; a canonical `Superseded` record without a usable target remains `ADR008`.

Long historical chains are valid: a decision that superseded an older one may itself later be superseded. Circular narrative references are also valid unless they are explicit supersession edges. The graph is evaluated in deterministic path order and does not use AI or semantic interpretation.
