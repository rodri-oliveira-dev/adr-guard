# Why use ADRs?

[Português (Brasil)](why-use-adrs.pt-BR.md) · [Documentation home](../index.md) · [Previous](what-is-an-adr.md) · [Next](when-to-write-an-adr.md)

ADRs make the reasoning behind consequential changes reviewable and durable. Their value comes from the conversation and shared memory they support, not from producing documents for their own sake.

## Benefits for people and delivery

- **Decision rationale:** reviewers see the forces, alternatives, and trade-offs behind a choice.
- **Better review:** a focused proposal gives security, operations, data, and product stakeholders a concrete artifact to challenge.
- **Onboarding and continuity:** new contributors can understand why the system looks as it does without reconstructing every discussion.
- **Traceability:** a decision can link to requirements, experiments, incidents, related ADRs, and implementation work.
- **Change safety:** when context changes, the team can revisit the original assumptions instead of blindly preserving or replacing the design.
- **Governance:** lightweight status and relationship conventions show which decisions are proposed, current, historical, or superseded.

These benefits can support developers, architects, technical leaders, auditors, and operational teams. ADRs also reduce dependence on any single person's memory. They do not eliminate disagreement; they make disagreement and its resolution easier to inspect.

## Organizational impact

A healthy ADR practice creates a shared decision trail across team boundaries. It can clarify ownership, expose cross-team dependencies earlier, and preserve institutional knowledge during reorganizations or handoffs. When ADRs live with the code, normal version-control review provides a visible history of proposals and changes.

ADRs should still be accessible to stakeholders who do not work in Git every day. Teams may share rendered repository links, include relevant people in review, and summarize decisions in the communication channels those people use.

## Limitations and costs

- Writing and reviewing a useful ADR takes time.
- Records become misleading when nobody maintains statuses or replacement links.
- Too many low-impact ADRs create noise and discourage discovery.
- A template can encourage completeness, but cannot replace evidence or judgment.
- Repository access may exclude important stakeholders unless the team deliberately includes them.
- ADRs describe decisions; implementation checks, operational controls, and outcome measurement remain separate work.

Use the lightest record that preserves enough reasoning. The next guide explains [which choices deserve an ADR](when-to-write-an-adr.md).
