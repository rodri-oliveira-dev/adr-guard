#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ACTION="${ROOT_DIR}/action.yml"
GUIDE_EN="${ROOT_DIR}/docs/github-action.md"
GUIDE_PT="${ROOT_DIR}/docs/github-action.pt-BR.md"
README_EN="${ROOT_DIR}/README.md"
README_PT="${ROOT_DIR}/README.pt-BR.md"
EXAMPLE_PR="${ROOT_DIR}/docs/examples/github-action-pr.yml"
EXAMPLE_MAIN="${ROOT_DIR}/docs/examples/github-action-main.yml"
EXAMPLE_INCREMENTAL="${ROOT_DIR}/docs/examples/github-action-incremental.yml"
EXAMPLE_FORK="${ROOT_DIR}/docs/examples/github-action-fork.yml"
EXTERNAL_EN="${ROOT_DIR}/docs/github-action-external-verification.md"
EXTERNAL_PT="${ROOT_DIR}/docs/github-action-external-verification.pt-BR.md"
REVIEW_EN="${ROOT_DIR}/docs/github-action-review.md"
REVIEW_PT="${ROOT_DIR}/docs/github-action-review.pt-BR.md"
AUDIT_EN="${ROOT_DIR}/docs/public-release-audit.md"
AUDIT_PT="${ROOT_DIR}/docs/public-release-audit.pt-BR.md"

for file in "${ACTION}" "${GUIDE_EN}" "${GUIDE_PT}" "${README_EN}" "${README_PT}" "${EXAMPLE_PR}" "${EXAMPLE_MAIN}" "${EXAMPLE_INCREMENTAL}" "${EXAMPLE_FORK}" "${EXTERNAL_EN}" "${EXTERNAL_PT}" "${REVIEW_EN}" "${REVIEW_PT}" "${AUDIT_EN}" "${AUDIT_PT}"; do
  test -s "${file}" || {
    echo "Required GitHub Action consumer documentation is missing: ${file}" >&2
    exit 1
  }
done

# Public input contract must stay synchronized with the guides.
for input in path command version review-target provider model endpoint context-files include-existing-adrs policy policy-file; do
  grep -Eq "^  ${input}:" "${ACTION}" || {
    echo "action.yml no longer exposes expected input '${input}'." >&2
    exit 1
  }
  input_token="$(printf '\140%s\140' "${input}")"
  grep -Fq "${input_token}" "${GUIDE_EN}" || {
    echo "English guide does not document input '${input}'." >&2
    exit 1
  }
  grep -Fq "${input_token}" "${GUIDE_PT}" || {
    echo "pt-BR guide does not document input '${input}'." >&2
    exit 1
  }
done

grep -Fq 'default: docs/adr' "${ACTION}"
grep -Fq 'default: check' "${ACTION}"
grep -Fq '| `path` | `docs/adr` |' "${GUIDE_EN}"
grep -Fq '| `path` | `docs/adr` |' "${GUIDE_PT}"
grep -Fq '| `command` | `check` |' "${GUIDE_EN}"
grep -Fq '| `command` | `check` |' "${GUIDE_PT}"

for review_guide in "${REVIEW_EN}" "${REVIEW_PT}"; do
  grep -Fq 'contents: read' "${review_guide}"
  grep -Fq 'pull_request_target' "${review_guide}"
  grep -Fq 'command: review' "${review_guide}"
  grep -Fq 'review-target:' "${review_guide}"
  grep -Fq 'policy: advisory' "${review_guide}"
  grep -Fq 'GITHUB_TOKEN' "${review_guide}"
done

# Consumer workflow examples are deliberately minimal and use only public inputs.
for example in "${EXAMPLE_PR}" "${EXAMPLE_MAIN}"; do
  if grep -Fiq 'forthcoming consumer example' "${example}"; then
    echo "Consumer example still claims @v1 is forthcoming: ${example}" >&2
    exit 1
  fi
  grep -Fq 'permissions:' "${example}"
  grep -Fq 'contents: read' "${example}"
  grep -Fq 'uses: actions/checkout@v7' "${example}"
  grep -Fq 'persist-credentials: false' "${example}"
  grep -Fq 'uses: rodri-oliveira-dev/adr-guard@v1' "${example}"
  grep -Fq 'path: docs/adr' "${example}"
  grep -Fq 'command: check' "${example}"

  if grep -Eq '^[[:space:]]+(version|provider|token|api-key):' "${example}"; then
    echo "Consumer example contains an unsupported or unnecessary input: ${example}" >&2
    exit 1
  fi
done

grep -Fq 'pull_request:' "${EXAMPLE_PR}"
grep -Fq 'push:' "${EXAMPLE_MAIN}"

# v1.4 source-preparation examples keep fork checks secretless and make full
# history an explicit prerequisite for opt-in Git comparison.
grep -Fq 'fetch-depth: 0' "${EXAMPLE_INCREMENTAL}"
grep -Fq -- '--changed --base-ref origin/main' "${EXAMPLE_INCREMENTAL}"
grep -Fq -- '--baseline .adrguard-baseline.json' "${EXAMPLE_INCREMENTAL}"
grep -Fq 'permissions:' "${EXAMPLE_FORK}"
grep -Fq 'contents: read' "${EXAMPLE_FORK}"
grep -Fq 'command: check' "${EXAMPLE_FORK}"
if grep -Eiq 'pull_request_target|OPENAI_API_KEY|ANTHROPIC_API_KEY|GEMINI_API_KEY|secrets\.' "${EXAMPLE_FORK}"; then
  echo "Fork-safe example must not use privileged events or provider secrets." >&2
  exit 1
fi

# Both languages must cover the same observable contract.
for term in 'ADR001' 'ADR009' 'GITHUB_STEP_SUMMARY' '50' 'exit code `2`' 'exit code `3`' '`check`' '`index`' '`@v1.2.3`' '`@v1`' '`@<commit-sha>`' 'contents: read' 'Docker' 'Python 3'; do
  grep -Fiq "${term}" "${GUIDE_EN}" || {
    echo "English guide is missing contract term: ${term}" >&2
    exit 1
  }
  grep -Fiq "${term}" "${GUIDE_PT}" || {
    echo "pt-BR guide is missing contract term: ${term}" >&2
    exit 1
  }
done

# @v1 is part of the published public contract. Keep this test deterministic:
# validate repository content instead of querying mutable remote tag state.
grep -Fiq 'compatibility tag `v1` is published' "${GUIDE_EN}" || {
  echo "English guide must describe the published v1 compatibility tag." >&2
  exit 1
}
grep -Fiq 'tag de compatibilidade `v1` está publicada' "${GUIDE_PT}" || {
  echo "pt-BR guide must describe the published v1 compatibility tag." >&2
  exit 1
}

grep -Fq 'supports `check`, `index`, and opt-in `review`' "${GUIDE_EN}" || {
  echo "English guide must describe review as part of the published v1 contract." >&2
  exit 1
}
grep -Fq 'suporta `check`, `index` e `review` opt-in' "${GUIDE_PT}" || {
  echo "pt-BR guide must describe review as part of the published v1 contract." >&2
  exit 1
}
grep -Fiq 'moving `@v1` tag is published' "${README_EN}" || {
  echo "English README must describe @v1 as published." >&2
  exit 1
}
grep -Fiq 'tag móvel `@v1` está publicada' "${README_PT}" || {
  echo "pt-BR README must describe @v1 as published." >&2
  exit 1
}

# The owner supplied the canonical listing URL; static checks only ensure bilingual consistency.
marketplace_url="https://github.com/marketplace/actions/adr-guard-architecture-decision-validator"
for page in "${GUIDE_EN}" "${GUIDE_PT}" "${README_EN}" "${README_PT}"; do
  grep -Fq "${marketplace_url}" "${page}" || {
    echo "Marketplace URL missing from ${page}." >&2
    exit 1
  }
done

for audit in "${AUDIT_EN}" "${AUDIT_PT}"; do
  grep -Fq 'v1.1.6' "${audit}"
  grep -Fq '36356961234' "${audit}"
  grep -Fq 'RodriOliveira.AdrGuard' "${audit}"
  grep -Fq 'Docker Hub' "${audit}"
  grep -Fq 'Marketplace' "${audit}"
done

for stale_claim in   'current published **v1.1.5**'   'current published CLI package/image | `v1.1.5`'   'Pacote/imagem do CLI publicados atualmente | `v1.1.5`'   'PR #85 on this branch'   'PR #85 nesta branch'   'still exposes `check`/`index` only'   'ainda expõe somente `check`/`index`'; do
  if grep -Fiq "${stale_claim}" "${GUIDE_EN}" "${GUIDE_PT}" "${README_EN}" "${README_PT}" "${REVIEW_EN}" "${REVIEW_PT}"; then
    echo "Documentation contains stale post-v1.1.6 availability wording: ${stale_claim}" >&2
    exit 1
  fi
done

# Historical external-consumer evidence must identify the real published @v1 line without
# depending on one exact prose sentence.
for token in 'v1.1.6' 'rodri-oliveira-dev/adr-guard@v1'; do
  grep -Fq "${token}" "${EXTERNAL_EN}" || {
    echo "English external-verification guide is missing published v1 evidence: ${token}" >&2
    exit 1
  }
  grep -Fq "${token}" "${EXTERNAL_PT}" || {
    echo "pt-BR external-verification guide is missing published v1 evidence: ${token}" >&2
    exit 1
  }
done
for stale_claim in \
  '`@v1` does not exist yet' \
  '`@v1` ainda não existe' \
  'until this branch is merged, a real Action release/tag exists' \
  'até esta branch entrar na `main`, existir uma release/tag real' \
  'not part of the current remote `@v1`' \
  'ainda não faz parte do `@v1` remoto atual' \
  'gains `review` only after the corresponding release' \
  'recebe `review` somente após a release correspondente'; do
  if grep -Fiq "${stale_claim}" "${EXTERNAL_EN}" "${EXTERNAL_PT}" "${GUIDE_EN}" "${GUIDE_PT}" "${README_EN}" "${README_PT}" "${REVIEW_EN}" "${REVIEW_PT}"; then
    echo "Documentation contains an obsolete pre-v1 availability claim: ${stale_claim}" >&2
    exit 1
  fi
done

# Release notes are real today and should remain linked.
grep -Fq 'https://github.com/rodri-oliveira-dev/adr-guard/releases' "${GUIDE_EN}"
grep -Fq 'https://github.com/rodri-oliveira-dev/adr-guard/releases' "${GUIDE_PT}"

echo "GitHub Action consumer documentation checks passed."
