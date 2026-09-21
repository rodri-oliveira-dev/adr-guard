#!/usr/bin/env bash
set -euo pipefail

IMAGE_VERSION="${1:-0.1.12}"
FIXTURE_RELATIVE="${2:-.tmp/adr-action-contract}"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
FIXTURE_ROOT="${ROOT_DIR}/${FIXTURE_RELATIVE}"
IMAGE="ghcr.io/rodri-oliveira-dev/adr-guard:${IMAGE_VERSION}"
TEMP_DIR="$(mktemp -d)"
trap 'rm -rf -- "${TEMP_DIR}"' EXIT

assert_exit_code() {
  local expected="$1"
  shift

  set +e
  "$@"
  local actual=$?
  set -e

  if [[ "$actual" -ne "$expected" ]]; then
    echo "Expected exit code ${expected}, got ${actual}: $*" >&2
    exit 1
  fi
}

run_wrapper() {
  local path="$1"
  local command="${2:-check}"
  local summary="${3:-${TEMP_DIR}/summary.md}"

  : >"$summary"
  env \
    GITHUB_WORKSPACE="${ROOT_DIR}" \
    GITHUB_STEP_SUMMARY="$summary" \
    RUNNER_OS=Linux \
    ADR_GUARD_PATH="$path" \
    ADR_GUARD_COMMAND="$command" \
    ADR_GUARD_VERSION="${IMAGE_VERSION}" \
    ADR_GUARD_ACTION_REF="contract-test" \
    bash "${ROOT_DIR}/scripts/github-action.sh"
}

direct_check() {
  local path="$1"
  docker run --rm --read-only \
    --mount "type=bind,src=${ROOT_DIR},dst=/workspace,readonly" \
    --workdir /workspace \
    "${IMAGE}" \
    check "/workspace/${path}"
}

if [[ ! -d "${FIXTURE_ROOT}" ]]; then
  echo "Fixture root does not exist: ${FIXTURE_ROOT}" >&2
  exit 1
fi

# Positive consumer paths, including nested paths and spaces.
assert_exit_code 0 run_wrapper "${FIXTURE_RELATIVE}/valid ADRs"
assert_exit_code 0 run_wrapper "${FIXTURE_RELATIVE}/custom path/adr records"

# Input failures retain the public CLI-style exit contract.
assert_exit_code 2 run_wrapper "${FIXTURE_RELATIVE}/missing ADRs"
assert_exit_code 2 run_wrapper "${FIXTURE_RELATIVE}/valid ADRs" unsupported

# A check must not mutate the selected checkout content.
before="${TEMP_DIR}/check-before.sha256"
after="${TEMP_DIR}/check-after.sha256"
find "${FIXTURE_ROOT}/custom path/adr records" -type f -print0 \
  | sort -z \
  | xargs -0 sha256sum >"$before"
assert_exit_code 0 run_wrapper "${FIXTURE_RELATIVE}/custom path/adr records"
find "${FIXTURE_ROOT}/custom path/adr records" -type f -print0 \
  | sort -z \
  | xargs -0 sha256sum >"$after"
cmp "$before" "$after"

# Each stable ADR rule is exercised through the published image. The local
# Action wrapper must preserve the same exit result and produce the matching
# file annotation/summary from the real CLI output.
for code in ADR001 ADR002 ADR003 ADR004 ADR005 ADR006 ADR007 ADR008 ADR009; do
  path="${FIXTURE_RELATIVE}/matrix/${code}"
  direct_stdout="${TEMP_DIR}/${code}.direct.stdout"
  direct_stderr="${TEMP_DIR}/${code}.direct.stderr"
  action_log="${TEMP_DIR}/${code}.action.log"
  summary="${TEMP_DIR}/${code}.summary.md"

  set +e
  direct_check "$path" >"$direct_stdout" 2>"$direct_stderr"
  direct_status=$?
  set -e

  if [[ "$direct_status" -ne 1 ]]; then
    echo "${code}: published CLI expected exit 1, got ${direct_status}." >&2
    cat "$direct_stdout" "$direct_stderr" >&2
    exit 1
  fi
  grep -Fq "${code}" "$direct_stderr"

  set +e
  run_wrapper "$path" check "$summary" >"$action_log" 2>&1
  action_status=$?
  set -e

  if [[ "$action_status" -ne "$direct_status" ]]; then
    echo "${code}: Action exit ${action_status} differs from direct CLI exit ${direct_status}." >&2
    cat "$action_log" >&2
    exit 1
  fi

  grep -Fq "title=${code}::" "$action_log"
  grep -Fq "| ${code} |" "$summary"
  grep -Fq '### ADR Guard — ADR validation failed' "$summary"
done

echo "GitHub Action contract regression tests passed with published image ${IMAGE_VERSION}."
