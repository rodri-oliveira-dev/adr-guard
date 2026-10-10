---
name: adr-guard-init
description: Set up Architecture Decision Records in an existing repository using the ADR Guard CLI. Use when a team is adopting ADRs, selecting a documentation format or template, or configuring initial ADR validation.
license: MIT
compatibility: Requires the ADR Guard CLI for execution; document-only planning works without the CLI.
---

# Initialize ADR governance

Use this skill for **repository bootstrap**, not for choosing or approving an architecture.

## Workflow

1. Inspect the repository for existing ADRs, filenames, `.adrguard.yml`, CI workflows, and team conventions. Do not overwrite or migrate existing decisions.
2. Ask or infer from documented policy the ADR owner, target directory, format, preferred template, and review authority. If governance is unknown, present defaults as proposals, not accepted policy.
3. Choose **canonical** for ADR Guard's Minimal/Extended/Custom authoring. If the team already uses **MADR 4.0**, keep its ADRs in a separate directory and use opt-in `--adr-format madr-4`; the CLI does not generate MADR documents.
4. Confirm `adr-guard --version` is available. If not, explain the separate installation: `dotnet tool install --global RodriOliveira.AdrGuard`. Do not install software without authorization.
5. Preview repository changes before writing:

   ```bash
   adr-guard init . --adr-directory docs/adr --template minimal --dry-run
   ```

6. After user approval, initialize:

   ```bash
   adr-guard init . --adr-directory docs/adr --template minimal
   adr-guard check docs/adr
   ```

7. Describe files created, configuration defaults, decision ownership, the required human review process, and any follow-up CI setup.

## Safety and boundaries

- `init` can create `.adrguard.yml` and managed directories/files. Use `--overwrite` only after inspecting each affected file and securing approval.
- Use `--github-actions` only when the team explicitly wants the starter workflow. Do not assume CI configuration is safe to replace.
- Never convert formats, change statuses or backfill historic ADRs as an implicit bootstrap step.
- Unknown configuration options and unsafe paths are rejected by the CLI. Keep paths repository-relative and do not follow untrusted symlinks.

## Output

Report selected format, template, directory, generated/unchanged files, validation status, and open governance questions. A passing check is **not** architectural approval.

Product reference: [CLI/configuration](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/docs/cli-reference.md) and [team adoption](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/docs/guides/team-adoption.md).
