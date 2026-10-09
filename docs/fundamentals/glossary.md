# ADR glossary and further reading

[Português (Brasil)](glossary.pt-BR.md) · [Documentation home](../index.md) · [Previous](lifecycle.md)

## Glossary

| Term | Meaning |
| --- | --- |
| Architecture decision | A choice with significant effects on a system's structure, quality attributes, dependencies, interfaces, operations, or construction. |
| ADR | Architecture Decision Record: a durable account of one decision and its context and consequences. |
| Canonical format | ADR Guard's default format, with `Status`, `Context`, `Decision`, and `Consequences` level-two sections. |
| Decision category | The domain affected by a decision, such as data, security, or infrastructure. It does not determine the file format. |
| Decision driver | A requirement, quality attribute, constraint, or priority used to compare options. |
| MADR | Markdown Architectural Decision Records, an external ADR template family. ADR Guard supports an explicit MADR 4.0 validation subset. |
| Template | The starting document shape and guidance used to author a record. ADR Guard generates canonical Minimal, Extended, or structurally compliant Custom records. |
| Validation | A deterministic check of structure and relationships. It is not architectural review or approval. |
| Supersede | Replace a historical decision with a newer ADR while preserving both records and their relationship. |

## Further reading

- Michael Nygard's [Documenting Architecture Decisions](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions), the original short-form ADR proposal.
- The community [Architecture Decision Records site](https://adr.github.io/) for practices, templates, and tooling.
- The [ADR template catalog](https://adr.github.io/adr-templates/) for educational comparison. A listed template is not automatically supported by ADR Guard.
- The [MADR project](https://adr.github.io/madr/) and its [4.0.0 templates](https://github.com/adr/madr/tree/4.0.0/template).
- ADR Guard's [format selection guide](../decision-design/choosing-a-template.md) for the exact supported boundary.
