# ADR Guard Agent Skills — Phase P0

[Português (Brasil)](README.pt-BR.md) · [Agent Skills specification](https://agentskills.io/specification)

Agent Skills are **optional agent-facing workflows** that help people use ADR Guard consistently. They do not bundle the ADR Guard executable, replace deterministic validation, make decisions, or approve architecture.

## Available skills

| Skill | Use when |
| --- | --- |
| [adr-guard-init](../../skills/adr-guard-init/SKILL.md) | Bootstrapping ADRs in a repository |
| [adr-guard-create](../../skills/adr-guard-create/SKILL.md) | Creating a new Proposed decision record |
| [adr-guard-validate](../../skills/adr-guard-validate/SKILL.md) | Checking structure, links, IDs and relationships |
| [adr-guard-technical-review](../../skills/adr-guard-technical-review/SKILL.md) | Reviewing rationale, trade-offs, risks and evidence |
| [adr-guard-lifecycle](../../skills/adr-guard-lifecycle/SKILL.md) | Recording human-authorized status changes and replacements |
| [adr-guard-ci-setup](../../skills/adr-guard-ci-setup/SKILL.md) | Adding ADR validation to GitHub Actions |

## Install and use

Discover the six skills in this repository:

```bash
npx skills add rodri-oliveira-dev/adr-guard --list
```

Install only the relevant skill for Codex (run inside the **consumer** project):

```bash
npx skills add rodri-oliveira-dev/adr-guard --skill adr-guard-create --agent codex
```

For a review of unpublished branch content, use a supported Git reference/source URL rather than expecting commands pointed at the default branch to see unmerged files. Verify `npx skills add ... --list` against the public default branch after merge. Skills CLI source: [vercel-labs/skills](https://github.com/vercel-labs/skills).

The skills are separate from the CLI:

```bash
dotnet tool install --global RodriOliveira.AdrGuard
adr-guard --version
```

If the tool is unavailable, the agent can propose steps but must not claim to have validated or written records using ADR Guard.

## Design principles

1. **Evidence over confident prose:** never fabricate constraints, compliance obligations, performance data or approvals.
2. **Human authority:** `Proposed` is the default for created documents; approval/rejection is never performed autonomously.
3. **Privacy by opt-in:** provider-backed `draft` and `review` may send selected information to a third party; users must explicitly choose the provider, model and transmitted context.
4. **One source of truth:** deterministic validations belong to the ADR Guard CLI; a skill invokes and explains the CLI rather than reimplementing it.
5. **Portable skills:** each `SKILL.md` works after an individual installation, without relying on a relative link back to this repository.
6. **No silent writes:** inspect target files, preview operations when supported, and obtain authorization before overwriting configurations, statuses or CI workflows.

## Validate changes to the catalog

```bash
python3 scripts/validate-agent-skills.py
python3 -m unittest discover -s tests/agent_skills -p 'test_*.py'
```

The dedicated GitHub Actions workflow runs both checks on relevant changes. Local validation verifies the lightweight YAML frontmatter subset used here, the six P0 skill names and portable local references; it is **not** a replacement for integration testing each agent.
