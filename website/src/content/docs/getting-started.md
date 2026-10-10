---
title: Create your first ADR
description: A progressive, verified tutorial from identifying a decision to validating and indexing its record.
sidebar:
  order: 2
---

An architecture decision record is most useful when it captures a consequential choice while the context is still fresh. This tutorial uses ADR Guard’s **canonical Minimal** format and keeps every command local.

## 1. Frame the decision

Write one sentence that names the tension, not the tool: “How should our API reduce repeated product reads without returning stale prices for too long?” A decision deserves an ADR when it has lasting technical consequences, meaningful trade-offs, or affects more than one contributor.

## 2. Choose a template

Use [the template selector](/adr-guard/templates/) as a heuristic. Minimal is appropriate for this focused, reversible pilot. Extended is better when alternatives, drivers, or risks need a fuller record. MADR 4.0 is a separate validation mode, not another canonical generation template.

## 3. Initialize and create

```bash
dotnet tool install --global RodriOliveira.AdrGuard
adr-guard init . --adr-directory docs/adr --template minimal
adr-guard new docs/adr --title "Adopt Redis for distributed caching" --template minimal
```

`init` prepares the configured ADR directory. `new` allocates an identifier and writes a canonical record. Review the generated path before editing.

## 4. Write context, decision, and consequences

Describe the current pressure and constraints in **Context**. In **Decision**, state the choice precisely enough to guide implementation. In **Consequences**, record benefits and costs—including operational work, failure modes, and what remains unknown.

```md
## Context

Repeated product reads increase database load. Prices may be stale for at most 30 seconds.

## Decision

Use managed Redis with cache-aside reads and a 30-second TTL for product prices.

## Consequences

Read latency and database load should fall. We must operate Redis, observe hit rate,
and handle cache failure without blocking the source of truth.
```

## 5. Review the reasoning

Ask people affected by the decision to check assumptions, alternatives, security, operability, and reversibility. ADR Guard can check structure and can optionally assist a review; it does **not** accept the architecture decision for your team.

## 6. Validate

```bash
adr-guard check docs/adr
```

A valid structure is necessary, but it does not prove the decision is good. Fix validation findings, then obtain the human approval required by your team’s process.

## 7. Generate the index

```bash
adr-guard index docs/adr
```

Review the generated index in the same change. ADR Guard validates before replacing it, protecting the previous index from invalid input.

Inspect new files before committing: plain `git diff` omits untracked files. Review the staged configuration, ADR, and index.

```bash
git status --short -- .adrguard.yml docs/adr
git add -- .adrguard.yml docs/adr
git diff --cached -- .adrguard.yml docs/adr
```

## 8. Integrate with development

Add [incremental validation](/adr-guard/product/incremental-validation/) locally or use the [GitHub Action](/adr-guard/product/github-action/) in pull requests. Start with validation feedback; add policy or AI-assisted workflows only when the team has a clear need.

## Next steps

- Compare [Minimal, Extended, and MADR 4.0](/adr-guard/templates/).
- Read the complete [Redis example](/adr-guard/examples/redis-cache/).
- Plan a lightweight [team adoption](/adr-guard/adoption/) pilot.
- Use [Agent Skills](/adr-guard/skills/) for a guided workflow; install the skill and ADR Guard CLI separately.
