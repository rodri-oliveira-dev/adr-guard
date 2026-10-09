# Write your first ADR

[Português (Brasil)](getting-started.pt-BR.md) · [Documentation home](../index.md) · [Complete examples](../examples/README.md)

This tutorial starts with the decision, not the tool. You will frame a realistic choice, create a canonical Minimal record, replace its guidance with real reasoning, review it, validate it, and generate an index.

## 1. Identify a decision worth recording

Assume an order service stores critical order data in a document database. New audit and consistency requirements need atomic updates across order headers, lines, and payments. Reversing a storage choice would require migration and operational work, so the decision deserves an ADR.

Write down before choosing a technology:

- the problem: atomic changes and auditability;
- constraints: existing data, limited migration window, service ownership;
- viable options: keep the document store with compensating logic, or adopt a relational store;
- decision drivers: consistency, auditability, migration risk, and operational support.

## 2. Choose a model

Use **Minimal** when the comparison and consequences fit clearly in the core sections. Use [Extended](../decision-design/choosing-a-template.md) if reviewers need explicit option-by-option analysis. This tutorial uses Minimal.

## 3. Install and initialize

Install the published .NET Tool, then run these commands from the root of an existing repository:

```bash
dotnet tool install --global RodriOliveira.AdrGuard
adr-guard init . --adr-directory docs/adr --template minimal
adr-guard new docs/adr --title "Use PostgreSQL as the order system of record"
```

`init` creates the ADR directory and `.adrguard.yml`; it does not overwrite a different existing configuration unless `--overwrite` is explicitly supplied. `new` creates the next `NNNN-lowercase-kebab-case.md` file as `Proposed`. It works offline and does not update the index.

## 4. Replace the guidance

Edit the generated file. Remove every `[EDIT: ...]` instruction and write complete content:

- **Context:** facts, constraints, stakeholders, and why a decision is needed now.
- **Decision:** the selected option, its scope, and the reason it fits the drivers.
- **Consequences:** benefits and disadvantages, including migration and operational ownership.

Use the [complete Minimal example](../examples/canonical-minimal/en/0001-use-postgresql-for-order-data.md) as a reference, not as text to copy without checking your context.

## 5. Review and decide

Open the ADR in the team's normal pull-request workflow. Include service and platform owners and any security, data, or product stakeholders affected by the choice. Review evidence, viable alternatives, costs, failure modes, and assumptions.

Keep `Proposed` while the choice is under discussion. Only an authorized human decision process changes it to `Accepted`. A passing validator or AI suggestion is not approval.

## 6. Validate and index

```bash
adr-guard check docs/adr
adr-guard index docs/adr
git status --short -- .adrguard.yml docs/adr
git add -- .adrguard.yml docs/adr
git diff --cached -- .adrguard.yml docs/adr
```

`check` returns `0` when the set passes structural validation and `1` for validation findings. `index` validates first, then creates or refreshes `docs/adr/README.md` deterministically. Review `git status` before staging to avoid including unrelated files. `git add` stages new files too, so `git diff --cached` shows the new ADR, generated index, and configuration alongside changes to tracked files. Confirm the staged diff before committing the ADR and generated index together when your project tracks that index.

You now have the first decision in a durable history. Continue with [team adoption](team-adoption.md), or inspect [creation options and exit codes](../creation.md).
