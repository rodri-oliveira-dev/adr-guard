#!/usr/bin/env bash
set -euo pipefail

IMAGE_VERSION="${1:-0.1.12}"
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

run_wrapper() {
  env \
    GITHUB_WORKSPACE="${WORKSPACE}" \
    RUNNER_OS=Linux \
    PATH="${PATH}" \
    DOCKER_CAPTURE="${DOCKER_CAPTURE:-}" \
    DOCKER_EXIT_CODE="${DOCKER_EXIT_CODE:-0}" \
    ADR_GUARD_PATH="${ADR_GUARD_PATH-docs/adr}" \
    ADR_GUARD_COMMAND="${ADR_GUARD_COMMAND-check}" \
    ADR_GUARD_VERSION="${ADR_GUARD_VERSION-${IMAGE_VERSION}}" \
    ADR_GUARD_ACTION_REF="${ADR_GUARD_ACTION_REF-feature-branch}" \
    bash "${ROOT_DIR}/scripts/github-action.sh"
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

assert_exit_code 0 run_wrapper

ADR_GUARD_PATH="docs/invalid ADRs" assert_exit_code 1 run_wrapper

rm -f "${WORKSPACE}/docs/adr/README.md"
ADR_GUARD_COMMAND=index assert_exit_code 0 run_wrapper
test -s "${WORKSPACE}/docs/adr/README.md"
grep -q "# Architecture Decision Records" "${WORKSPACE}/docs/adr/README.md"

ADR_GUARD_COMMAND=unsupported assert_exit_code 2 run_wrapper
ADR_GUARD_COMMAND="" assert_exit_code 2 run_wrapper
ADR_GUARD_PATH="" assert_exit_code 2 run_wrapper
ADR_GUARD_PATH="../outside" assert_exit_code 2 run_wrapper
ADR_GUARD_PATH="/tmp" assert_exit_code 2 run_wrapper
ADR_GUARD_PATH="docs/missing" assert_exit_code 2 run_wrapper

mkdir -p "${TEMP_DIR}/outside-workspace"
ln -s "${TEMP_DIR}/outside-workspace" "${WORKSPACE}/docs/escape"
ADR_GUARD_PATH="docs/escape" assert_exit_code 2 run_wrapper

ADR_GUARD_VERSION="latest" assert_exit_code 2 run_wrapper

# When version is omitted, an exact action release ref selects the matching image.
ADR_GUARD_VERSION="" ADR_GUARD_ACTION_REF="v${IMAGE_VERSION}" assert_exit_code 0 run_wrapper

# Verify mount permissions without relying on ADR Guard behavior.
FAKE_BIN="${TEMP_DIR}/fake-bin"
DOCKER_CAPTURE="${TEMP_DIR}/docker-args.txt"
mkdir -p "${FAKE_BIN}"
cat >"${FAKE_BIN}/docker" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
if [[ "${1:-}" == "info" ]]; then
  exit 0
fi
printf '%s\n' "$@" >"${DOCKER_CAPTURE:?}"
exit "${DOCKER_EXIT_CODE:-0}"
EOF
chmod +x "${FAKE_BIN}/docker"

PATH="${FAKE_BIN}:${PATH}" DOCKER_CAPTURE="${DOCKER_CAPTURE}" \
  ADR_GUARD_COMMAND=check assert_exit_code 0 run_wrapper
grep -Fxq "type=bind,src=${WORKSPACE},dst=/workspace,readonly" "${DOCKER_CAPTURE}"

PATH="${FAKE_BIN}:${PATH}" DOCKER_CAPTURE="${DOCKER_CAPTURE}" \
  ADR_GUARD_COMMAND=index assert_exit_code 0 run_wrapper
grep -Fxq "type=bind,src=${WORKSPACE},dst=/workspace" "${DOCKER_CAPTURE}"
if grep -Fxq "type=bind,src=${WORKSPACE},dst=/workspace,readonly" "${DOCKER_CAPTURE}"; then
  echo "Index mount must be writable." >&2
  exit 1
fi
grep -Fxq -- "--user" "${DOCKER_CAPTURE}"

PATH="${FAKE_BIN}:${PATH}" DOCKER_CAPTURE="${DOCKER_CAPTURE}" DOCKER_EXIT_CODE=125 \
  ADR_GUARD_COMMAND=check ADR_GUARD_VERSION=999.999.999 assert_exit_code 3 run_wrapper

echo "GitHub Action wrapper smoke tests passed."
