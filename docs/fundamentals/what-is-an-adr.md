# What is an Architecture Decision Record?

[Português (Brasil)](what-is-an-adr.pt-BR.md) · [Documentation home](../index.md) · [Next: Why use ADRs?](why-use-adrs.md)

An **architecture decision** is a choice that significantly affects a system's structure, quality attributes, dependencies, interfaces, operations, or ways of working. An **Architecture Decision Record (ADR)** is a short, durable document that explains one such choice.

An ADR answers four questions:

1. **Context:** What problem and constraints made a decision necessary?
2. **Decision:** What will the team do?
3. **Consequences:** What benefits, costs, risks, and follow-up work result?
4. **Status:** Is the choice still being proposed, currently accepted, deprecated, or replaced?

## The problem ADRs solve

Code shows what a system does, but it rarely preserves why a team selected one design over another. Tickets and chat threads can contain pieces of that reasoning, yet they are often scattered, inaccessible to new teammates, or detached from the code they influenced.

Without a durable record, future maintainers may repeat an investigation, keep an obsolete choice because its constraints are unknown, or reverse a choice without seeing the trade-offs it protected. An ADR places the reasoning near the work and keeps historical decisions visible.

## A small example

Suppose an order service needs a durable relational store. “Use PostgreSQL” alone records a result, not a decision. A useful ADR explains that orders require transactions and auditability, identifies alternatives such as the current document store, chooses PostgreSQL for those requirements, and records consequences such as migration effort and operational ownership.

An ADR is not:

- a complete system design or replacement for diagrams and runbooks;
- a meeting transcript, project plan, or list of every minor implementation choice;
- proof that an architecture is correct;
- an approval produced by a validation or AI tool.

ADR Guard checks structural rules such as filenames, required sections, statuses, links, and relationship integrity. It can assist drafting and review, but architectural acceptance remains a human responsibility.

Next, learn [why ADRs help teams and where they do not](why-use-adrs.md).
