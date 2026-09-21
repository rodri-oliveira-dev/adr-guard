#!/usr/bin/env bash
set -euo pipefail

ACTION_REF="${1:-v0.1.12}"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TEMP_DIR="$(mktemp -d)"
trap 'rm -rf "${TEMP_DIR}"' EXIT

assert_exit_code() {
  local expected="$1"
  shift

  set +e
  "$@"
  local actual=$?
  set -e

  if [[ "${actual}" -ne "${expected}" ]]; then
    echo "Expected exit code ${expected}, got ${actual}: $*" >&2
    exit 1
  fi
}

WORKSPACE="${TEMP_DIR}/workspace with spaces"
mkdir -p "${WORKSPACE}/docs/adr" "${WORKSPACE}/docs/invalid ADRs"
cp -R "${ROOT_DIR}/docs/adr/." "${WORKSPACE}/docs/adr/"

cat >"${WORKSPACE}/docs/invalid ADRs/0001-invalid.md" <<'EOF'
# Invalid ADR

## Status

Accepted

## Context

This fixture intentionally omits the Decision section.

## Consequences

Validation must fail.
EOF

assert_exit_code 0 \
  env GITHUB_WORKSPACE="${WORKSPACE}" RUNNER_OS=Linux \
  bash "${ROOT_DIR}/scripts/github-action.sh" "docs/adr" "${ACTION_REF}"

assert_exit_code 1 \
  env GITHUB_WORKSPACE="${WORKSPACE}" RUNNER_OS=Linux \
  bash "${ROOT_DIR}/scripts/github-action.sh" "docs/invalid ADRs" "${ACTION_REF}"

assert_exit_code 2 \
  env GITHUB_WORKSPACE="${WORKSPACE}" RUNNER_OS=Linux \
  bash "${ROOT_DIR}/scripts/github-action.sh" "docs/adr" "main"

echo "GitHub Action wrapper smoke tests passed."
