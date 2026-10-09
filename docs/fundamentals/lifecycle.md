# ADR lifecycle

[Português (Brasil)](lifecycle.pt-BR.md) · [Documentation home](../index.md) · [Previous](when-to-write-an-adr.md) · [Next](glossary.md)

An ADR is a living decision record with an append-only history. Teams discuss and refine a proposal, accept it through their own governance process, and preserve it when the decision later changes.

## Typical flow

1. **Identify:** recognize a consequential choice and its decision owner.
2. **Propose:** describe context, viable options, a recommended decision, and consequences.
3. **Review:** involve affected people, test assumptions, and record meaningful trade-offs.
4. **Accept or decline:** an authorized person or group decides. ADR Guard never performs this approval.
5. **Implement and observe:** link delivery work or evidence when useful; verify whether assumptions hold.
6. **Revisit:** deprecate a decision that is discouraged without a direct replacement, or supersede it with a new ADR.

Teams may add states such as rejected in their broader process, but ADR Guard's canonical validator accepts exactly these status values:

| Canonical status | Meaning |
| --- | --- |
| `Proposed` | Under discussion; not yet approved. `new` and `draft` create this status. |
| `Accepted` | Approved through the team's human decision process and currently applicable. |
| `Deprecated` | Retained as history but no longer recommended; there may be no single replacement. |
| `Superseded` | Replaced by a later ADR. A canonical record must include `Superseded by` with a link to an ADR in the validated set. |

MADR 4.0 uses optional status metadata as prose. ADR Guard does not restrict that metadata to the four canonical values; it interprets the explicit `superseded by ADR-NNNN` form for relationship integrity. See the [MADR compatibility boundary](../madr-4.md).

## Preserve history

Do not silently rewrite an accepted ADR to make a new choice appear old. Create a new proposed ADR, link the relationship, review it, then update the previous record's status and replacement link. Minor corrections that do not change the decision can use normal version history, but material changes deserve a new record.

Structural validation means that the record follows known rules. It does not mean the evidence is sound, the right stakeholders agreed, implementation is complete, or the decision is architecturally approved.
