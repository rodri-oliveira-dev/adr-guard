#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ACTION="${ROOT_DIR}/action.yml"
MARKETPLACE_EN="${ROOT_DIR}/docs/github-marketplace.md"
MARKETPLACE_PT="${ROOT_DIR}/docs/github-marketplace.pt-BR.md"

expected_name="ADR Guard - Architecture Decision Validator"
expected_description="Validate and index Architecture Decision Records (ADRs) with deterministic checks and GitHub file annotations."

if [[ "${REPOSITORY_VISIBILITY:-}" != "public" ]]; then
  echo "GitHub Marketplace publication requires a public repository; got '${REPOSITORY_VISIBILITY:-<unknown>}'." >&2
  exit 1
fi

mapfile -t root_metadata < <(
  find "${ROOT_DIR}" -maxdepth 1 -type f \( -name action.yml -o -name action.yaml \) -printf '%f\n' | sort
)

if [[ "${#root_metadata[@]}" -ne 1 || "${root_metadata[0]}" != "action.yml" ]]; then
  echo "Exactly one root Action metadata file named action.yml is required." >&2
  printf 'Found: %s\n' "${root_metadata[*]:-<none>}" >&2
  exit 1
fi

grep -Fxq "name: ${expected_name}" "${ACTION}"
grep -Fxq "description: ${expected_description}" "${ACTION}"
grep -Fxq "author: Rodrigo de Oliveira" "${ACTION}"
grep -Fxq "  icon: shield" "${ACTION}"
grep -Fxq "  color: purple" "${ACTION}"

for input in path command version; do
  grep -Eq "^  ${input}:" "${ACTION}" || {
    echo "Marketplace metadata lost public input '${input}'." >&2
    exit 1
  }
done

for required in LICENSE SUPPORT.md SECURITY.md README.md README.pt-BR.md docs/github-action.md docs/github-action.pt-BR.md docs/github-marketplace.md docs/github-marketplace.pt-BR.md docs/github-action-external-verification.md docs/github-action-external-verification.pt-BR.md; do
  test -s "${ROOT_DIR}/${required}" || {
    echo "Marketplace readiness file is missing or empty: ${required}" >&2
    exit 1
  }
done

grep -Fq 'MIT License' "${ROOT_DIR}/LICENSE"
grep -Fq 'https://github.com/rodri-oliveira-dev/adr-guard/issues' "${ROOT_DIR}/SUPPORT.md"
grep -Fq 'Reporting a vulnerability' "${ROOT_DIR}/SECURITY.md"
grep -Fq 'Pre-release external verification: passed.' "${ROOT_DIR}/docs/github-action-external-verification.md"
grep -Fq 'Production Marketplace verification: pending.' "${ROOT_DIR}/docs/github-action-external-verification.md"
grep -Fq 'Verificação externa pré-release: aprovada.' "${ROOT_DIR}/docs/github-action-external-verification.pt-BR.md"
grep -Fq 'Verificação de produção no Marketplace: pendente.' "${ROOT_DIR}/docs/github-action-external-verification.pt-BR.md"

for guide in "${MARKETPLACE_EN}" "${MARKETPLACE_PT}"; do
  grep -Fq "${expected_name}" "${guide}"
  grep -Fq 'shield' "${guide}"
  grep -Fq 'purple' "${guide}"
  grep -Fiq 'Code quality' "${guide}"
  grep -Fiq 'Continuous integration' "${guide}"
  grep -Fq 'docs.github.com' "${guide}"
  grep -Fq 'GitHub Marketplace Developer Agreement' "${guide}"
  grep -Fiq '2FA' "${guide}"
  grep -Fiq 'verified creator' "${guide}"
done

# Marketplace categories and final publication are UI choices, not metadata fields.
if grep -Eq '^[[:space:]]*(category|categories):' "${ACTION}"; then
  echo "Marketplace categories must not be invented as unsupported action.yml metadata." >&2
  exit 1
fi

# Until publication is manually verified, public docs must not claim a live Marketplace URL.
if grep -R -E 'github\.com/marketplace/actions/'   "${ROOT_DIR}/README.md"   "${ROOT_DIR}/README.pt-BR.md"   "${ROOT_DIR}/docs/github-action.md"   "${ROOT_DIR}/docs/github-action.pt-BR.md" >/dev/null; then
  echo "Marketplace listing URL found before the listing has been manually verified." >&2
  exit 1
fi

echo "GitHub Marketplace readiness checks passed."
