# ADR Guard documentation

[Português (Brasil)](index.pt-BR.md) · [Project README](../README.md)

Architecture Decision Records (ADRs) preserve the context, choice, and consequences of decisions that shape a software system. ADR Guard helps teams keep those records structurally consistent, connected, and reviewable. It validates documentation; people still own the architectural decision.

## Choose your path

| I want to... | Start here |
| --- | --- |
| Understand ADRs for the first time | [What is an ADR?](fundamentals/what-is-an-adr.md), then [why teams use them](fundamentals/why-use-adrs.md) |
| Decide whether a topic deserves an ADR | [When to write an ADR](fundamentals/when-to-write-an-adr.md) |
| Create and validate a first record | [Write your first ADR](guides/getting-started.md) |
| Select Minimal, Extended, Custom, or MADR 4.0 | [Choose a template or format](decision-design/choosing-a-template.md) |
| Establish a team practice | [Adopt ADRs as a team](guides/team-adoption.md) |
| Find commands and integration contracts | [Technical reference](#technical-reference) |

## Learn the practice

### Fundamentals

- [What is an ADR?](fundamentals/what-is-an-adr.md) — the problem, the record, and a small example.
- [Why use ADRs?](fundamentals/why-use-adrs.md) — benefits, organizational impact, and limitations.
- [When to write an ADR](fundamentals/when-to-write-an-adr.md) — practical criteria and when not to add one.
- [ADR lifecycle](fundamentals/lifecycle.md) — propose, review, accept, deprecate, and supersede.
- [Glossary and further reading](fundamentals/glossary.md) — shared vocabulary and authoritative references.

### Design a useful decision record

- [Decision categories](decision-design/decision-categories.md) — architecture, data, security, operations, and other domains.
- [Formats and templates](decision-design/formats-and-templates.md) — the difference between a decision subject and its document shape.
- [Template selection guide](decision-design/choosing-a-template.md) — Minimal, Extended, Custom, and MADR 4.0 compared.
- [Writing effective ADRs](decision-design/writing-effective-adrs.md) — evidence, options, trade-offs, and clear outcomes.
- [Anti-patterns](decision-design/anti-patterns.md) — weak rationale, hidden costs, and silent history rewriting.

### Put it into practice

- [Write your first ADR](guides/getting-started.md) — a beginner-friendly end-to-end tutorial.
- [Adopt ADRs as a team](guides/team-adoption.md) — ownership, review, governance, and rollout.
- [Complete examples](examples/README.md) — one realistic decision expressed as Minimal, Extended, and MADR 4.0.

## Technical reference

| Area | Documentation |
| --- | --- |
| CLI and configuration | [CLI reference](cli-reference.md), [offline creation](creation.md), [custom templates](custom-templates.md) |
| Validation modes | [MADR 4.0](madr-4.md), [incremental validation and baselines](incremental-validation.md), [relationship governance](relationship-governance.md) |
| Reports | [check reports](check-reports.md), [review report schema](adr-review-report-v1.md), [review policy](adr-review-policy-v1.md) |
| AI-assisted workflows | [draft templates and privacy](draft-templates.md), [ADR review](adr-review.md), [comparative review](comparative-review.md), [review security](adr-review-security.md) |
| Integrations | [GitHub Action](github-action.md), [VS Code](../extensions/vscode/README.md), [container images](container.md) |
| Release and supply chain | [Action release policy](github-action-release.md), [Action security](github-action-security.md), [public release audit](public-release-audit.md) |
| Project architecture | [ADR Guard's own ADR index](adr/README.md) |

Release-specific notes remain in [`docs/releases`](releases/). Use [GitHub Releases](https://github.com/rodri-oliveira-dev/adr-guard/releases) for the latest published version.
