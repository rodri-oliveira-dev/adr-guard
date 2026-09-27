#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TEMP_DIR="$(mktemp -d)"

cleanup() {
  local status=$?
  trap - EXIT
  if [[ "${status}" -ne 0 ]]; then
    echo "GitHub Action AI review test failed at line ${BASH_LINENO[0]} while running: ${BASH_COMMAND}" >&2
  fi
  rm -rf -- "${TEMP_DIR}"
  exit "${status}"
}
trap cleanup EXIT

bash -n "${ROOT_DIR}/scripts/github-action.sh"
python3 -m py_compile "${ROOT_DIR}/scripts/github-action-review-report.py"

WORKSPACE="${TEMP_DIR}/workspace"
FAKE_BIN="${TEMP_DIR}/fake-bin"
DOCKER_CAPTURE="${TEMP_DIR}/docker-args.txt"
MOCK_STDOUT="${TEMP_DIR}/mock.stdout"
MOCK_STDERR="${TEMP_DIR}/mock.stderr"
mkdir -p "${WORKSPACE}/docs/adr" "${WORKSPACE}/docs/context" "${FAKE_BIN}"

cat >"${WORKSPACE}/docs/adr/0001-review.md" <<'EOF'
# Review Action

## Status

Proposed

## Context

Review this ADR without mutating it.

## Decision

Keep AI review advisory.

## Consequences

Human review remains authoritative.
EOF

cat >"${WORKSPACE}/docs/context/security.md" <<'EOF'
# Security context

The selected workflow is trusted and uses least privilege.
EOF

cat >"${WORKSPACE}/docs/review-policy.json" <<'EOF'
{
  "schemaVersion": "1.0",
  "rules": [
    {
      "name": "context-present",
      "type": "required-section-content",
      "section": "Context"
    }
  ]
}
EOF

cat >"${MOCK_STDOUT}" <<'EOF'
{
  "schemaVersion": "1.0",
  "target": {
    "sourceId": "target",
    "path": "0001-review.md"
  },
  "inputScope": {
    "explicitContext": [
      {
        "sourceId": "context-1",
        "path": "security.md"
      }
    ],
    "existingAdrs": []
  },
  "outcome": "follow-up-suggested",
  "findings": [
    {
      "dimension": "security",
      "classification": "potential-risk",
      "followUpPriority": "recommended",
      "uncertainty": "human verification required",
      "explanation": "Confirm ![result](https://attacker.example/pixel) **the documented trust boundary** before rollout.",
      "guidance": "Verify [the workflow event](https://attacker.example/review) and credential scope.",
      "evidence": [
        {
          "sourceId": "target",
          "path": "0001-review.md",
          "line": 12,
          "excerpt": "Keep AI review advisory."
        }
      ]
    }
  ]
}
EOF
: >"${MOCK_STDERR}"

cat >"${FAKE_BIN}/docker" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail

case "${1:-}" in
  info)
    exit 0
    ;;
  pull)
    exit "${MOCK_PULL_EXIT:-0}"
    ;;
  run)
    printf '%s\n' "$@" >"${DOCKER_CAPTURE:?}"
    if [[ -n "${MOCK_STDOUT_FILE:-}" ]]; then
      cat -- "${MOCK_STDOUT_FILE}"
    fi
    if [[ -n "${MOCK_STDERR_FILE:-}" ]]; then
      cat -- "${MOCK_STDERR_FILE}" >&2
    fi
    exit "${MOCK_EXIT_CODE:-0}"
    ;;
  *)
    echo "Unexpected docker invocation: $*" >&2
    exit 99
    ;;
esac
EOF
chmod +x "${FAKE_BIN}/docker"

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

run_action() {
  local summary="$1"
  : >"${summary}"

  env \
    PATH="${FAKE_BIN}:${PATH}" \
    GITHUB_WORKSPACE="${WORKSPACE}" \
    GITHUB_STEP_SUMMARY="${summary}" \
    RUNNER_OS=Linux \
    ADR_GUARD_ACTION_REF=review-test \
    ADR_GUARD_VERSION=1.1.0 \
    ADR_GUARD_PATH="${ADR_GUARD_PATH-docs/adr}" \
    ADR_GUARD_COMMAND="${ADR_GUARD_COMMAND-check}" \
    ADR_GUARD_REVIEW_TARGET="${ADR_GUARD_REVIEW_TARGET-}" \
    ADR_GUARD_REVIEW_PROVIDER="${ADR_GUARD_REVIEW_PROVIDER-}" \
    ADR_GUARD_REVIEW_MODEL="${ADR_GUARD_REVIEW_MODEL-}" \
    ADR_GUARD_REVIEW_ENDPOINT="${ADR_GUARD_REVIEW_ENDPOINT-}" \
    ADR_GUARD_REVIEW_CONTEXT_FILES="${ADR_GUARD_REVIEW_CONTEXT_FILES-}" \
    ADR_GUARD_REVIEW_INCLUDE_EXISTING_ADRS="${ADR_GUARD_REVIEW_INCLUDE_EXISTING_ADRS-false}" \
    ADR_GUARD_REVIEW_POLICY="${ADR_GUARD_REVIEW_POLICY-advisory}" \
    ADR_GUARD_REVIEW_POLICY_FILE="${ADR_GUARD_REVIEW_POLICY_FILE-}" \
    ADR_GUARD_EVENT_NAME="${ADR_GUARD_EVENT_NAME-push}" \
    ADR_GUARD_REPOSITORY="${ADR_GUARD_REPOSITORY-acme/example}" \
    ADR_GUARD_PR_HEAD_REPOSITORY="${ADR_GUARD_PR_HEAD_REPOSITORY-}" \
    DOCKER_CAPTURE="${DOCKER_CAPTURE}" \
    MOCK_STDOUT_FILE="${MOCK_STDOUT_FILE-}" \
    MOCK_STDERR_FILE="${MOCK_STDERR_FILE-}" \
    MOCK_EXIT_CODE="${MOCK_EXIT_CODE-0}" \
    OPENAI_API_KEY="${OPENAI_API_KEY-}" \
    GITHUB_TOKEN="${GITHUB_TOKEN-}" \
    bash "${ROOT_DIR}/scripts/github-action.sh"
}

# Disabled/default path: deterministic check behavior remains unchanged and
# provider credentials are never forwarded to the container.
CHECK_SUMMARY="${TEMP_DIR}/check-summary.md"
OPENAI_API_KEY=must-not-be-forwarded \
  ADR_GUARD_COMMAND=check \
  assert_exit_code 0 run_action "${CHECK_SUMMARY}"
grep -Fxq -- "--network=none" "${DOCKER_CAPTURE}"
if grep -Fxq -- "--env" "${DOCKER_CAPTURE}" || grep -Fq "OPENAI_API_KEY" "${DOCKER_CAPTURE}"; then
  echo "Deterministic check must not forward provider credentials." >&2
  exit 1
fi
grep -Fq '### ADR Guard — Success' "${CHECK_SUMMARY}"

# Enabled path: a trusted same-repository PR may opt into review. The fake
# Docker runtime represents a deterministic mock provider/CLI response.
REVIEW_SUMMARY="${TEMP_DIR}/review-summary.md"
REVIEW_LOG="${TEMP_DIR}/review.log"
before_hash="$(sha256sum "${WORKSPACE}/docs/adr/0001-review.md")"
OPENAI_API_KEY=mock-provider-secret \
  GITHUB_TOKEN=must-never-enter-review-container \
  ADR_GUARD_COMMAND=review \
  ADR_GUARD_REVIEW_TARGET=docs/adr/0001-review.md \
  ADR_GUARD_REVIEW_PROVIDER=openai \
  ADR_GUARD_REVIEW_MODEL=mock-model \
  ADR_GUARD_REVIEW_CONTEXT_FILES=docs/context/security.md \
  ADR_GUARD_EVENT_NAME=pull_request \
  ADR_GUARD_REPOSITORY=acme/example \
  ADR_GUARD_PR_HEAD_REPOSITORY=acme/example \
  MOCK_STDOUT_FILE="${MOCK_STDOUT}" \
  MOCK_STDERR_FILE="${MOCK_STDERR}" \
  MOCK_EXIT_CODE=0 \
  assert_exit_code 0 run_action "${REVIEW_SUMMARY}" >"${REVIEW_LOG}" 2>&1
after_hash="$(sha256sum "${WORKSPACE}/docs/adr/0001-review.md")"
test "${before_hash}" = "${after_hash}"

grep -Fxq "review" "${DOCKER_CAPTURE}"
grep -Fxq -- "--format" "${DOCKER_CAPTURE}"
grep -Fxq "json" "${DOCKER_CAPTURE}"
grep -Fxq -- "--provider" "${DOCKER_CAPTURE}"
grep -Fxq "openai" "${DOCKER_CAPTURE}"
grep -Fxq -- "--env" "${DOCKER_CAPTURE}"
grep -Fxq "OPENAI_API_KEY" "${DOCKER_CAPTURE}"
if grep -Fxq -- "--network=none" "${DOCKER_CAPTURE}"; then
  echo "Provider-backed review requires network access and must not inherit the deterministic network=none path." >&2
  exit 1
fi
if grep -Fq "mock-provider-secret" "${DOCKER_CAPTURE}" || grep -Fq "GITHUB_TOKEN" "${DOCKER_CAPTURE}"; then
  echo "Review must forward only the selected provider variable name, never secret values or GitHub tokens." >&2
  exit 1
fi
grep -Fq '::warning file=docs/adr/0001-review.md,title=ADR Guard AI review::' "${REVIEW_LOG}"
grep -Fq '### ADR Guard — AI review (advisory)' "${REVIEW_SUMMARY}"
grep -Fq '| Outcome | <code>follow-up-suggested</code> |' "${REVIEW_SUMMARY}"
grep -Fq 'human verification required' "${REVIEW_SUMMARY}"
grep -Fq '\!\[result\]' "${REVIEW_SUMMARY}"
if grep -Fq '![result](' "${REVIEW_SUMMARY}" || grep -Fq '[the workflow event](' "${REVIEW_SUMMARY}"; then
  echo "Provider-controlled Markdown must render as inert summary text." >&2
  exit 1
fi

# Fork PRs and pull_request_target are rejected before Docker/provider execution.
rm -f "${DOCKER_CAPTURE}"
FORK_LOG="${TEMP_DIR}/fork.log"
ADR_GUARD_COMMAND=review \
  ADR_GUARD_REVIEW_TARGET=docs/adr/0001-review.md \
  ADR_GUARD_REVIEW_PROVIDER=openai \
  ADR_GUARD_REVIEW_MODEL=mock-model \
  ADR_GUARD_EVENT_NAME=pull_request \
  ADR_GUARD_REPOSITORY=acme/example \
  ADR_GUARD_PR_HEAD_REPOSITORY=attacker/fork \
  OPENAI_API_KEY=must-not-be-exposed \
  assert_exit_code 2 run_action "${TEMP_DIR}/fork-summary.md" >"${FORK_LOG}" 2>&1
test ! -e "${DOCKER_CAPTURE}"
grep -Fiq 'disabled for fork or untrusted pull_request' "${FORK_LOG}"

rm -f "${DOCKER_CAPTURE}"
TARGET_LOG="${TEMP_DIR}/target.log"
ADR_GUARD_COMMAND=review \
  ADR_GUARD_REVIEW_TARGET=docs/adr/0001-review.md \
  ADR_GUARD_REVIEW_PROVIDER=openai \
  ADR_GUARD_REVIEW_MODEL=mock-model \
  ADR_GUARD_EVENT_NAME=pull_request_target \
  OPENAI_API_KEY=must-not-be-exposed \
  assert_exit_code 2 run_action "${TEMP_DIR}/target-summary.md" >"${TARGET_LOG}" 2>&1
test ! -e "${DOCKER_CAPTURE}"
grep -Fiq 'disabled for pull_request_target' "${TARGET_LOG}"

# Events outside the explicit trusted allowlist are rejected before provider execution.
rm -f "${DOCKER_CAPTURE}"
UNSUPPORTED_EVENT_LOG="${TEMP_DIR}/unsupported-event.log"
ADR_GUARD_COMMAND=review \
  ADR_GUARD_REVIEW_TARGET=docs/adr/0001-review.md \
  ADR_GUARD_REVIEW_PROVIDER=openai \
  ADR_GUARD_REVIEW_MODEL=mock-model \
  ADR_GUARD_EVENT_NAME=workflow_run \
  OPENAI_API_KEY=must-not-be-exposed \
  assert_exit_code 2 run_action "${TEMP_DIR}/unsupported-event-summary.md" >"${UNSUPPORTED_EVENT_LOG}" 2>&1
test ! -e "${DOCKER_CAPTURE}"
grep -Fiq "AI review is not supported for event 'workflow_run'" "${UNSUPPORTED_EVENT_LOG}"

# A successful provider-backed review requires Python 3 for safe summary rendering.
NO_PYTHON_BIN="${TEMP_DIR}/no-python-bin"
mkdir -p "${NO_PYTHON_BIN}"
ln -s "$(command -v realpath)" "${NO_PYTHON_BIN}/realpath"
NO_PYTHON_LOG="${TEMP_DIR}/no-python.log"
set +e
PATH="${NO_PYTHON_BIN}" \
  GITHUB_WORKSPACE="${WORKSPACE}" \
  GITHUB_STEP_SUMMARY="${TEMP_DIR}/no-python-summary.md" \
  RUNNER_OS=Linux \
  ADR_GUARD_ACTION_REF=review-test \
  ADR_GUARD_VERSION=1.1.0 \
  ADR_GUARD_COMMAND=review \
  ADR_GUARD_REVIEW_TARGET=docs/adr/0001-review.md \
  ADR_GUARD_REVIEW_PROVIDER=openai \
  ADR_GUARD_REVIEW_MODEL=mock-model \
  ADR_GUARD_EVENT_NAME=push \
  ADR_GUARD_REPOSITORY=acme/example \
  /bin/bash "${ROOT_DIR}/scripts/github-action.sh" >"${NO_PYTHON_LOG}" 2>&1
no_python_status=$?
set -e
if [[ "${no_python_status}" -ne 3 ]]; then
  echo "Expected missing Python 3 to return exit code 3, got ${no_python_status}." >&2
  exit 1
fi
grep -Fiq 'Python 3 is required for safe AI review summary and annotation rendering' "${NO_PYTHON_LOG}"

# Deterministic enforcement failures and provider/transport failures remain
# distinct in both exit status and summary.
POLICY_SUMMARY="${TEMP_DIR}/policy-summary.md"
: >"${MOCK_STDOUT}"
printf '%s\n' 'Review policy outcome: policy-failed. The review provider was not invoked.' >"${MOCK_STDERR}"
ADR_GUARD_COMMAND=review \
  ADR_GUARD_REVIEW_TARGET=docs/adr/0001-review.md \
  ADR_GUARD_REVIEW_PROVIDER=openai \
  ADR_GUARD_REVIEW_MODEL=mock-model \
  ADR_GUARD_REVIEW_POLICY=enforce \
  ADR_GUARD_REVIEW_POLICY_FILE=docs/review-policy.json \
  MOCK_STDOUT_FILE="${MOCK_STDOUT}" \
  MOCK_STDERR_FILE="${MOCK_STDERR}" \
  MOCK_EXIT_CODE=4 \
  assert_exit_code 4 run_action "${POLICY_SUMMARY}" >/dev/null 2>&1
grep -Fq '| Exit code | `4` |' "${POLICY_SUMMARY}"
grep -Fq 'Deterministic review policy failed' "${POLICY_SUMMARY}"
grep -Fiq 'before provider-backed review completed' "${POLICY_SUMMARY}"

OPERATIONAL_SUMMARY="${TEMP_DIR}/operational-summary.md"
printf '%s\n' 'ADR review provider failed: mock transport unavailable.' >"${MOCK_STDERR}"
ADR_GUARD_COMMAND=review \
  ADR_GUARD_REVIEW_TARGET=docs/adr/0001-review.md \
  ADR_GUARD_REVIEW_PROVIDER=openai \
  ADR_GUARD_REVIEW_MODEL=mock-model \
  MOCK_STDOUT_FILE="${MOCK_STDOUT}" \
  MOCK_STDERR_FILE="${MOCK_STDERR}" \
  MOCK_EXIT_CODE=3 \
  assert_exit_code 3 run_action "${OPERATIONAL_SUMMARY}" >/dev/null 2>&1
grep -Fq '| Exit code | `3` |' "${OPERATIONAL_SUMMARY}"
grep -Fiq 'Provider, transport, cancellation, or operational failure' "${OPERATIONAL_SUMMARY}"
grep -Fiq 'No clean-review conclusion' "${OPERATIONAL_SUMMARY}"

echo "GitHub Action opt-in AI review tests passed with deterministic mock-provider output."
