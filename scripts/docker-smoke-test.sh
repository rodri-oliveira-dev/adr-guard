#!/usr/bin/env bash
set -euo pipefail

IMAGE="${1:-adr-guard:ci}"
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

configured_user="$(docker image inspect --format '{{.Config.User}}' "${IMAGE}")"
if [[ -z "${configured_user}" || "${configured_user}" == "0" || "${configured_user}" == "root" ]]; then
  echo "Container image must configure a non-root default user." >&2
  exit 1
fi

echo "Container default user: ${configured_user}"

docker run --rm --read-only "${IMAGE}" --help >"${TEMP_DIR}/help.txt"
grep -q "ADR Guard" "${TEMP_DIR}/help.txt"
grep -q "Exit codes:" "${TEMP_DIR}/help.txt"

assert_exit_code 0 \
  docker run --rm --read-only \
    --mount "type=bind,src=${ROOT_DIR}/docs/adr,dst=/workspace/docs/adr,readonly" \
    "${IMAGE}" check docs/adr

mkdir -p "${TEMP_DIR}/invalid"
cat >"${TEMP_DIR}/invalid/0001-invalid.md" <<'EOF'
# Invalid ADR

## Status

Accepted

## Context

This fixture intentionally omits a required section.

## Consequences

Validation must fail.
EOF

assert_exit_code 1 \
  docker run --rm --read-only \
    --mount "type=bind,src=${TEMP_DIR}/invalid,dst=/workspace/adrs,readonly" \
    "${IMAGE}" check adrs

assert_exit_code 2 \
  docker run --rm --read-only \
    "${IMAGE}" unsupported-command

assert_exit_code 3 \
  docker run --rm --read-only \
    "${IMAGE}" check /workspace/does-not-exist

mkdir -p "${TEMP_DIR}/writable"
cp -R "${ROOT_DIR}/docs/adr/." "${TEMP_DIR}/writable/"
rm -f "${TEMP_DIR}/writable/README.md"

assert_exit_code 0 \
  docker run --rm \
    --user "$(id -u):$(id -g)" \
    --mount "type=bind,src=${TEMP_DIR}/writable,dst=/workspace/adrs" \
    "${IMAGE}" index adrs

test -s "${TEMP_DIR}/writable/README.md"
grep -q "# Architecture Decision Records" "${TEMP_DIR}/writable/README.md"

echo "Docker smoke tests passed."
