---
title: Install ADR Guard Agent Skills
description: List and install ADR Guard Agent Skills with the Skills CLI, set up the independent .NET CLI, and verify a safe development workflow.
sidebar:
  order: 2
---

Agent Skills and ADR Guard's .NET CLI are **two different installations**. The skill teaches your coding agent what to do; the CLI performs deterministic file operations and validation. You can use a skill to discuss decisions even without the CLI, but it must not claim to have run validation without the tool.

## Prerequisites

- A compatible coding agent, such as Codex, that supports Agent Skills.
- Node.js and `npx` to run the [Skills CLI](https://github.com/vercel-labs/skills). Review your organization's package execution policy before running packages through `npx`.
- The [ADR Guard .NET Tool](../../product/cli/) for CLI-backed workflows. Provider-backed AI review/drafting is optional and requires a separately chosen provider, model and credentials.

## 1. Discover available skills

Run from your **consumer repository**, where you want the agent to use the workflows:

```bash
npx skills add rodri-oliveira-dev/adr-guard --list
```

Read the selected skill before installing it; skills are executable instructions for an agent, and third-party content should be reviewed as code-like input.

**Branch awareness:** the command above discovers skills from the repository's default branch. When reviewing unmerged changes, inspect the proposed branch directly instead of assuming unpublished skills are already available through the default-branch listing.

## 2. Install only what you need

For Codex, install the creation skill:

```bash
npx skills add rodri-oliveira-dev/adr-guard --skill adr-guard-create --agent codex
```

For architectural comparison rather than record creation:

```bash
npx skills add rodri-oliveira-dev/adr-guard --skill adr-guard-tradeoff-analysis --agent codex
```

The [catalog](../catalog/) describes all 11 skills. Other agents may be supported by the Skills CLI; use the CLI's current help to verify exact agent identifiers and installation modes. Avoid installing every skill by default.

## 3. Install ADR Guard separately

If your task needs `init`, `new`, `check`, `index` or `review`, install the published .NET Tool and verify its version:

```bash
dotnet tool install --global RodriOliveira.AdrGuard
adr-guard --version
```

To update an existing global installation:

```bash
dotnet tool update --global RodriOliveira.AdrGuard
```

Do not install tools or change repository files without authorization. Team-managed local tools may use a different, pinned installation policy.

## 4. Try a safe first task

Ask the agent:

> Examine the current repository and explain whether choosing Redis as a distributed cache needs an ADR. List the known constraints, unknowns, alternatives and reviewers. Do not write files yet.

When you decide to create a new record, authorize the agent to preview before writing:

```bash
adr-guard new docs/adr --title "Use Redis for caching" --template extended --preview
```

The destination ADR directory must already exist. On approval, the agent can use `new`, then `check` and `index`; the new record starts as **Proposed**.

## Troubleshooting

| Problem | What to check |
| --- | --- |
| `--list` shows no ADR Guard skills | Have the skills merged into the repository default branch? |
| Agent recognizes a skill, but `adr-guard` is unavailable | Install or restore the independent .NET Tool and verify `adr-guard --version`. |
| CLI rejects the document | Check the repository's `.adrguard.yml`, required canonical headings, format and real diagnostics; see [validation](../catalog/#adr-guard-validate). |
| AI review needs credentials | Provide explicit provider/model and authorization for transmitted content. There is **no** automatic provider selection. |
| MADR 4.0 document does not match a generated template | MADR is separately authored and validated with `--adr-format madr-4`; `new` does not generate MADR files. |

Next: [browse the catalog](../catalog/), [follow task examples](../examples/) or [review trust boundaries](../security-and-governance/).
