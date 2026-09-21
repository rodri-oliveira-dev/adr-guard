#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ACTION="${ROOT_DIR}/action.yml"
GUIDE_EN="${ROOT_DIR}/docs/github-action.md"
GUIDE_PT="${ROOT_DIR}/docs/github-action.pt-BR.md"
EXAMPLE_PR="${ROOT_DIR}/docs/examples/github-action-pr.yml"
EXAMPLE_MAIN="${ROOT_DIR}/docs/examples/github-action-main.yml"

for file in "${ACTION}" "${GUIDE_EN}" "${GUIDE_PT}" "${EXAMPLE_PR}" "${EXAMPLE_MAIN}"; do
  test -s "${file}" || {
    echo "Required GitHub Action consumer documentation is missing: ${file}" >&2
    exit 1
  }
done

# Public input contract must stay synchronized with the guides.
for input in path command version; do
  grep -Eq "^  ${input}:" "${ACTION}" || {
    echo "action.yml no longer exposes expected input '${input}'." >&2
    exit 1
  }
  grep -Fq "`\${input}`" "${GUIDE_EN}" || {
    echo "English guide does not document input '${input}'." >&2
    exit 1
  }
  grep -Fq "`\${input}`" "${GUIDE_PT}" || {
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

# Consumer workflow examples are deliberately minimal and use only public inputs.
for example in "${EXAMPLE_PR}" "${EXAMPLE_MAIN}"; do
  grep -Fq '# Forthcoming consumer example: @v1 is not published yet.' "${example}"
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

# Both languages must cover the same observable contract.
for term in 'ADR001' 'ADR009' 'GITHUB_STEP_SUMMARY' '50' 'exit code `2`' 'exit code `3`' '`check`' '`index`' '`@v1.2.3`' '`@v1`' '`@<commit-sha>`' 'contents: read' 'Docker'; do
  grep -Fq "${term}" "${GUIDE_EN}" || {
    echo "English guide is missing contract term: ${term}" >&2
    exit 1
  }
  grep -Fq "${term}" "${GUIDE_PT}" || {
    echo "pt-BR guide is missing contract term: ${term}" >&2
    exit 1
  }
done

# Availability claims are stateful. Until v1 exists, the docs must say so.
v1_remote="$(git ls-remote --tags origin refs/tags/v1 | awk 'NR == 1 { print $1 }')"
if [[ -z "${v1_remote}" ]]; then
  grep -Fiq 'forthcoming' "${GUIDE_EN}"
  grep -Fiq 'futura' "${GUIDE_PT}"

  if grep -Eq 'github\.com/marketplace/actions/' "${GUIDE_EN}" "${GUIDE_PT}"; then
    echo "Marketplace URL must not be published before a verified listing exists." >&2
    exit 1
  fi
else
  echo "::error::Tag v1 now exists. Update consumer docs/examples to remove the forthcoming warning and verify the published Action before merging."
  exit 1
fi

# Release notes are real today and should remain linked.
grep -Fq 'https://github.com/rodri-oliveira-dev/adr-guard/releases' "${GUIDE_EN}"
grep -Fq 'https://github.com/rodri-oliveira-dev/adr-guard/releases' "${GUIDE_PT}"

echo "GitHub Action consumer documentation checks passed."
