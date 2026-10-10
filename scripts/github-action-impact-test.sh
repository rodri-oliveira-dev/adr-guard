#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TEMP_DIR="$(mktemp -d)"
trap 'rm -rf -- "${TEMP_DIR}"' EXIT

bash -n "${ROOT_DIR}/scripts/github-action.sh"
python3 -m py_compile "${ROOT_DIR}/scripts/github-action-impact-report.py"

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
FAKE_BIN="${TEMP_DIR}/fake-bin"
DOCKER_CAPTURE="${TEMP_DIR}/docker-args.txt"
MOCK_STDOUT="${TEMP_DIR}/impact.json"
MOCK_STDERR="${TEMP_DIR}/impact.stderr"
SUMMARY="${TEMP_DIR}/summary.md"
mkdir -p "${WORKSPACE}/docs/adr" "${WORKSPACE}/src" "${FAKE_BIN}"
git -C "${WORKSPACE}" init -b main >/dev/null
git -C "${WORKSPACE}" config user.email tests@example.com
git -C "${WORKSPACE}" config user.name "ADR Guard Tests"

cat >"${WORKSPACE}/docs/adr/0001-service.md" <<'EOF'
# Service boundary

## Status
Accepted

## Context
Context.

## Decision
Decision.

## Consequences
Consequences.
EOF
cat >"${WORKSPACE}/.adrguard-impact.json" <<'EOF'
{"schemaVersion":"1.0","mappings":[{"decision":{"stableId":"ADR-1","path":"docs/adr/0001-service.md"},"patterns":["src/**"],"relationship":"governs","reason":"Explicit mapping."}]}
EOF
printf 'initial\n' >"${WORKSPACE}/src/service.cs"
git -C "${WORKSPACE}" add .
git -C "${WORKSPACE}" commit -m initial >/dev/null
printf 'changed\n' >>"${WORKSPACE}/src/service.cs"

cat >"${MOCK_STDOUT}" <<'EOF'
{
  "schemaVersion": "1.0",
  "advisory": true,
  "summary": {"totalChanges": 1, "affectedDecisions": 1, "unknownDecisions": 0, "notMatchedDecisions": 0},
  "coverage": {"affectedChanges": 1, "unknownChanges": 0, "notMatchedChanges": 0},
  "changedFiles": [{"newPath": "src/service.cs"}],
  "decisions": [{"stableId": "ADR|<img src=x>", "status": "affected", "evidence": [{}]}]
}
EOF
: >"${MOCK_STDERR}"

cat >"${FAKE_BIN}/docker" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
case "${1:-}" in
  info) exit 0 ;;
  pull) exit "${MOCK_PULL_EXIT:-0}" ;;
  run)
    if [[ " $* " == *" impact --help "* ]]; then
      exit "${MOCK_PROBE_EXIT:-0}"
    fi
    printf '%s\n' "$@" >"${DOCKER_CAPTURE:?}"
    [[ -z "${MOCK_STDOUT_FILE:-}" ]] || cat -- "${MOCK_STDOUT_FILE}"
    [[ -z "${MOCK_STDERR_FILE:-}" ]] || cat -- "${MOCK_STDERR_FILE}" >&2
    exit "${MOCK_RUN_EXIT:-0}"
    ;;
esac
exit 1
EOF
chmod +x "${FAKE_BIN}/docker"

run_impact() {
  env \
    PATH="${FAKE_BIN}:${PATH}" \
    GITHUB_WORKSPACE="${WORKSPACE}" \
    GITHUB_STEP_SUMMARY="${SUMMARY}" \
    RUNNER_OS=Linux \
    ADR_GUARD_PATH="docs/adr" \
    ADR_GUARD_COMMAND=impact \
    ADR_GUARD_VERSION=9.9.9 \
    ADR_GUARD_ACTION_REF=test \
    ADR_GUARD_IMPACT_BASE_REF="${ADR_GUARD_IMPACT_BASE_REF-HEAD}" \
    ADR_GUARD_IMPACT_MAP="${ADR_GUARD_IMPACT_MAP-.adrguard-impact.json}" \
    ADR_GUARD_EVENT_NAME="${ADR_GUARD_EVENT_NAME-pull_request}" \
    ADR_GUARD_REPOSITORY=owner/repository \
    ADR_GUARD_PR_HEAD_REPOSITORY="${ADR_GUARD_PR_HEAD_REPOSITORY-owner/repository}" \
    DOCKER_CAPTURE="${DOCKER_CAPTURE}" \
    MOCK_STDOUT_FILE="${MOCK_STDOUT}" \
    MOCK_STDERR_FILE="${MOCK_STDERR}" \
    MOCK_PROBE_EXIT="${MOCK_PROBE_EXIT-0}" \
    MOCK_RUN_EXIT="${MOCK_RUN_EXIT-0}" \
    bash "${ROOT_DIR}/scripts/github-action.sh"
}

# Same-repository execution is advisory, read-only, offline, and argument-array safe.
: >"${SUMMARY}"
before="$(git -C "${WORKSPACE}" status --porcelain=v1)"
assert_exit_code 0 run_impact
after="$(git -C "${WORKSPACE}" status --porcelain=v1)"
test "${before}" = "${after}"
grep -Fxq -- '--read-only' "${DOCKER_CAPTURE}"
grep -Fxq -- '--network=none' "${DOCKER_CAPTURE}"
grep -Fxq -- 'impact' "${DOCKER_CAPTURE}"
grep -Fxq -- '--base-ref' "${DOCKER_CAPTURE}"
grep -Fxq -- 'HEAD' "${DOCKER_CAPTURE}"
grep -Fxq -- '--map' "${DOCKER_CAPTURE}"
grep -Fxq -- '.adrguard-impact.json' "${DOCKER_CAPTURE}"
grep -Fxq -- '--user' "${DOCKER_CAPTURE}"
grep -Fq 'Architecture impact (advisory)' "${SUMMARY}"
grep -Fq 'ADR\|&lt;img src=x&gt;' "${SUMMARY}"
if grep -Fq '<img src=x>' "${SUMMARY}"; then
  echo "Impact summary rendered unescaped HTML." >&2
  exit 1
fi
if grep -Eq 'OPENAI_API_KEY|ANTHROPIC_API_KEY|GEMINI_API_KEY|GITHUB_TOKEN|GH_TOKEN|GIT_CONFIG_' "${DOCKER_CAPTURE}"; then
  echo "Impact mode must not forward credentials." >&2
  exit 1
fi

# Fork-style pull requests remain secretless and use the same read-only path.
: >"${SUMMARY}"
ADR_GUARD_PR_HEAD_REPOSITORY=fork/repository assert_exit_code 0 run_impact

# Hostile references and map traversal are rejected before Docker execution.
ADR_GUARD_IMPACT_BASE_REF=$'HEAD\n::warning::forged' assert_exit_code 2 run_impact
ADR_GUARD_IMPACT_BASE_REF='--upload-pack=evil' assert_exit_code 2 run_impact
ADR_GUARD_IMPACT_MAP='../outside.json' assert_exit_code 2 run_impact

mkdir -p "${TEMP_DIR}/outside"
printf '{}\n' >"${TEMP_DIR}/outside/map.json"
ln -s "${TEMP_DIR}/outside/map.json" "${WORKSPACE}/escaped-map.json"
if [[ -L "${WORKSPACE}/escaped-map.json" ]]; then
  ADR_GUARD_IMPACT_MAP=escaped-map.json assert_exit_code 2 run_impact
fi

# An image without the impact command is rejected before consumer data is mounted.
MOCK_PROBE_EXIT=2 assert_exit_code 3 run_impact

# Missing Git history remains an operational result and produces no fabricated findings.
: >"${SUMMARY}"
printf 'Git exited with code 128: no merge base\n' >"${MOCK_STDERR}"
MOCK_RUN_EXIT=3 assert_exit_code 3 run_impact
grep -Fq 'Report | Unavailable' "${SUMMARY}"
if grep -Fq '| Decision |' "${SUMMARY}"; then
  echo "Operational failure must not fabricate impact decisions." >&2
  exit 1
fi

echo "GitHub Action architecture impact tests passed."
