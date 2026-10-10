# ADR practice compatibility fixtures

This directory inventories the regression fixtures introduced by ADR #0005. Tests add focused documents under `canonical`, `madr-4`, `links`, `relationships`, `identity`, `metadata`, and `security` as each policy is implemented.

Every fixture states whether it exercises unchanged default behavior or an explicit opt-in policy. Hostile fixtures are data only and must never be executed or resolved outside their temporary repository root.

| Directory | Policy | Purpose |
| --- | --- | --- |
| `canonical` | default | Published canonical contract |
| `madr-4` | `adr-format: madr-4` | MADR metadata authority |
| `legacy-prefix` | `filename-policy: adr-prefix` | Stable numeric identity with prefixed files |
| `document-links` | default | ADR link to ordinary repository documentation |
| `pending-supersession` | default | Proposed successor without premature status mutation |
| `metadata` | `validation-profile: strict` | Typed traceability metadata and deterministic hygiene |

