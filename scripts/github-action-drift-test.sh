#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TEMP_DIR="$(mktemp -d)"
trap 'rm -rf -- "${TEMP_DIR}"' EXIT

bash -n "${ROOT_DIR}/scripts/github-action.sh"
python3 -m py_compile "${ROOT_DIR}/scripts/github-action-drift-report.py"

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
SUMMARY="${TEMP_DIR}/summary.md"
PASS_REPORT="${TEMP_DIR}/pass.json"
VIOLATION_REPORT="${TEMP_DIR}/violation.json"
UNKNOWN_REPORT="${TEMP_DIR}/unknown.json"
mkdir -p "${WORKSPACE}/docs/adr" "${WORKSPACE}/evidence" "${FAKE_BIN}"

printf '{}\n' >"${WORKSPACE}/.adrguard-fitness.json"
printf '{}\n' >"${WORKSPACE}/drift-baseline.json"
printf '{}\n' >"${WORKSPACE}/evidence/one.json"
printf '{}\n' >"${WORKSPACE}/evidence/two.json"

cat >"${PASS_REPORT}" <<'EOF'
{
  "schemaVersion": "1.0",
  "policy": "advisory",
  "repository": {"rulesManifest": ".adrguard-fitness.json", "commit": null},
  "coverage": {"totalRules": 1, "pass": 1, "violations": 0, "unknown": 0, "notApplicable": 0},
  "findings": [{
    "fingerprint": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
    "ruleId": "domain-boundary",
    "adrStableId": "ADR-001",
    "outcome": "pass",
    "checkType": "forbid-project-reference",
    "confidence": "deterministic-static",
    "baselineState": "current",
    "explanation": "No forbidden reference was found.",
    "evidence": []
  }]
}
EOF

cat >"${VIOLATION_REPORT}" <<'EOF'
{
  "schemaVersion": "1.0",
  "policy": "enforce",
  "repository": {"rulesManifest": ".adrguard-fitness.json", "commit": "0123456789abcdef"},
  "coverage": {"totalRules": 1, "pass": 0, "violations": 1, "unknown": 0, "notApplicable": 0},
  "findings": [{
    "fingerprint": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
    "ruleId": "`rule|<img src=x>`",
    "adrStableId": "ADR`|<script>alert(1)</script>",
    "outcome": "violation",
    "checkType": "forbid-project-reference",
    "confidence": "deterministic-static",
    "baselineState": "new",
    "explanation": "A forbidden reference was found.",
    "evidence": []
  }]
}
EOF

cat >"${UNKNOWN_REPORT}" <<'EOF'
{
  "schemaVersion": "1.0",
  "policy": "advisory",
  "repository": {"rulesManifest": ".adrguard-fitness.json", "commit": null},
  "coverage": {"totalRules": 1, "pass": 0, "violations": 0, "unknown": 1, "notApplicable": 0},
  "findings": [{
    "fingerprint": "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
    "ruleId": "external-contract",
    "adrStableId": "ADR-002",
    "outcome": "unknown",
    "checkType": "external-test",
    "confidence": "none",
    "baselineState": "current",
    "explanation": "No external evidence was supplied.",
    "evidence": []
  }]
}
EOF

cat >"${FAKE_BIN}/docker" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
case "${1:-}" in
  info) exit 0 ;;
  pull) exit "${MOCK_PULL_EXIT:-0}" ;;
  run)
    if [[ " $* " == *" drift --help "* ]]; then
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

run_drift() {
  env \
    PATH="${FAKE_BIN}:${PATH}" \
    GITHUB_WORKSPACE="${WORKSPACE}" \
    GITHUB_STEP_SUMMARY="${SUMMARY}" \
    RUNNER_OS=Linux \
    ADR_GUARD_PATH=docs/adr \
    ADR_GUARD_COMMAND=drift \
    ADR_GUARD_VERSION=9.9.9 \
    ADR_GUARD_ACTION_REF=test \
    ADR_GUARD_DRIFT_RULES="${ADR_GUARD_DRIFT_RULES-.adrguard-fitness.json}" \
    ADR_GUARD_DRIFT_POLICY="${ADR_GUARD_DRIFT_POLICY-advisory}" \
    ADR_GUARD_DRIFT_BASELINE="${ADR_GUARD_DRIFT_BASELINE-}" \
    ADR_GUARD_DRIFT_EVIDENCE="${ADR_GUARD_DRIFT_EVIDENCE-}" \
    DOCKER_CAPTURE="${DOCKER_CAPTURE}" \
    MOCK_STDOUT_FILE="${MOCK_STDOUT_FILE-${PASS_REPORT}}" \
    MOCK_STDERR_FILE="${MOCK_STDERR_FILE-}" \
    MOCK_PROBE_EXIT="${MOCK_PROBE_EXIT-0}" \
    MOCK_RUN_EXIT="${MOCK_RUN_EXIT-0}" \
    bash "${ROOT_DIR}/scripts/github-action.sh"
}

# Advisory pass uses the existing CLI contract with a read-only, offline mount.
: >"${SUMMARY}"
assert_exit_code 0 run_drift
grep -Fxq -- '--read-only' "${DOCKER_CAPTURE}"
grep -Fxq -- '--network=none' "${DOCKER_CAPTURE}"
grep -Fxq -- 'drift' "${DOCKER_CAPTURE}"
grep -Fxq -- '/workspace' "${DOCKER_CAPTURE}"
grep -Fxq -- '--rules' "${DOCKER_CAPTURE}"
grep -Fxq -- '.adrguard-fitness.json' "${DOCKER_CAPTURE}"
grep -Fxq -- '--policy' "${DOCKER_CAPTURE}"
grep -Fxq -- 'advisory' "${DOCKER_CAPTURE}"
grep -Fxq -- '--format' "${DOCKER_CAPTURE}"
grep -Fxq -- 'json' "${DOCKER_CAPTURE}"
grep -Fq '| Outcome | Pass |' "${SUMMARY}"
if grep -Fxq -- '--write-baseline' "${DOCKER_CAPTURE}"; then
  echo "The Action must not expose drift baseline writes." >&2
  exit 1
fi
if grep -Eq 'OPENAI_API_KEY|ANTHROPIC_API_KEY|GEMINI_API_KEY|GITHUB_TOKEN|GH_TOKEN|GIT_CONFIG_' "${DOCKER_CAPTURE}"; then
  echo "Drift mode must not forward credentials or Git configuration." >&2
  exit 1
fi

# Existing baseline and multiple explicit evidence files remain discrete arguments.
: >"${SUMMARY}"
ADR_GUARD_DRIFT_BASELINE=drift-baseline.json \
ADR_GUARD_DRIFT_EVIDENCE=$'evidence/one.json\nevidence/two.json' \
  assert_exit_code 0 run_drift
grep -Fxq -- '--baseline' "${DOCKER_CAPTURE}"
grep -Fxq -- 'drift-baseline.json' "${DOCKER_CAPTURE}"
test "$(grep -Fxc -- '--evidence' "${DOCKER_CAPTURE}")" -eq 2
grep -Fxq -- 'evidence/one.json' "${DOCKER_CAPTURE}"
grep -Fxq -- 'evidence/two.json' "${DOCKER_CAPTURE}"

# Enforced violations and inconclusive evidence preserve CLI exits 4 and 3.
: >"${SUMMARY}"
ADR_GUARD_DRIFT_POLICY=enforce MOCK_STDOUT_FILE="${VIOLATION_REPORT}" MOCK_RUN_EXIT=4 \
  assert_exit_code 4 run_drift
grep -Fq '| Outcome | Violation (policy failed) |' "${SUMMARY}"
grep -Fq '``ADR`\|&lt;script&gt;alert(1)&lt;/script&gt;``' "${SUMMARY}"
grep -Fq '`` `rule\|&lt;img src=x&gt;` ``' "${SUMMARY}"
if grep -Fq '<script>' "${SUMMARY}"; then
  echo "Drift summary rendered unescaped HTML." >&2
  exit 1
fi

: >"${SUMMARY}"
MOCK_STDOUT_FILE="${UNKNOWN_REPORT}" MOCK_RUN_EXIT=3 assert_exit_code 3 run_drift
grep -Fq '| Outcome | Unknown |' "${SUMMARY}"
grep -Fq '| Unknown | 1 |' "${SUMMARY}"

# Invalid and unsafe inputs fail before consumer data reaches Docker.
ADR_GUARD_DRIFT_RULES='' assert_exit_code 2 run_drift
ADR_GUARD_DRIFT_POLICY=blocking assert_exit_code 2 run_drift
ADR_GUARD_DRIFT_RULES='../outside.json' assert_exit_code 2 run_drift
ADR_GUARD_DRIFT_BASELINE='../outside.json' assert_exit_code 2 run_drift
ADR_GUARD_DRIFT_EVIDENCE='../outside.json' assert_exit_code 2 run_drift
ADR_GUARD_DRIFT_EVIDENCE=$'evidence/one.json\nevidence/one.json' assert_exit_code 2 run_drift
printf '{}\n' >"${WORKSPACE}/rules.txt"
ADR_GUARD_DRIFT_RULES=rules.txt assert_exit_code 2 run_drift
ADR_GUARD_DRIFT_RULES=missing.json assert_exit_code 2 run_drift
ADR_GUARD_DRIFT_BASELINE=missing.json assert_exit_code 2 run_drift
ADR_GUARD_DRIFT_EVIDENCE=evidence/missing.json assert_exit_code 2 run_drift

# Root files whose names begin with dashes remain values rather than CLI options.
printf '{}\n' >"${WORKSPACE}/--fitness.json"
printf '{}\n' >"${WORKSPACE}/--baseline.json"
printf '{}\n' >"${WORKSPACE}/--evidence.json"
ADR_GUARD_DRIFT_RULES=--fitness.json \
ADR_GUARD_DRIFT_BASELINE=--baseline.json \
ADR_GUARD_DRIFT_EVIDENCE=--evidence.json \
  assert_exit_code 0 run_drift
grep -Fxq -- './--fitness.json' "${DOCKER_CAPTURE}"
grep -Fxq -- './--baseline.json' "${DOCKER_CAPTURE}"
grep -Fxq -- './--evidence.json' "${DOCKER_CAPTURE}"

mkdir -p "${TEMP_DIR}/outside"
printf '{}\n' >"${TEMP_DIR}/outside/rules.json"
ln -s "${TEMP_DIR}/outside/rules.json" "${WORKSPACE}/escaped-rules.json"
if [[ -L "${WORKSPACE}/escaped-rules.json" ]]; then
  ADR_GUARD_DRIFT_RULES=escaped-rules.json assert_exit_code 2 run_drift
fi

evidence_list=""
for index in $(seq 1 21); do
  evidence_file="evidence/item-${index}.json"
  printf '{}\n' >"${WORKSPACE}/${evidence_file}"
  evidence_list+="${evidence_file}"$'\n'
done
ADR_GUARD_DRIFT_EVIDENCE="${evidence_list}" assert_exit_code 2 run_drift

# Unsupported runtimes fail closed, and reporting failures never replace CLI status.
MOCK_PROBE_EXIT=2 assert_exit_code 3 run_drift
: >"${SUMMARY}"
printf 'not-json\n' >"${TEMP_DIR}/invalid.json"
INVALID_REPORT_LOG="${TEMP_DIR}/invalid-report.log"
ADR_GUARD_DRIFT_POLICY=enforce MOCK_STDOUT_FILE="${TEMP_DIR}/invalid.json" MOCK_RUN_EXIT=4 \
  assert_exit_code 4 run_drift >"${INVALID_REPORT_LOG}" 2>&1
grep -Fq 'architecture drift reporting failed' "${INVALID_REPORT_LOG}"

NESTED_REPORT="${TEMP_DIR}/nested-report.json"
python3 - "${NESTED_REPORT}" <<'PY'
import sys
from pathlib import Path

depth = 5_000
Path(sys.argv[1]).write_text('{"nested":' * depth + 'null' + '}' * depth, encoding='utf-8')
PY
NESTED_REPORT_LOG="${TEMP_DIR}/nested-report.log"
GITHUB_STEP_SUMMARY="${SUMMARY}" \
  assert_exit_code 1 python3 "${ROOT_DIR}/scripts/github-action-drift-report.py" 0 "${NESTED_REPORT}" \
  >"${NESTED_REPORT_LOG}" 2>&1
grep -Fq 'drift JSON nesting exceeds the parser depth limit' "${NESTED_REPORT_LOG}"
if grep -Fq 'Traceback' "${NESTED_REPORT_LOG}"; then
  echo "Deeply nested drift JSON must fail without a traceback." >&2
  exit 1
fi

echo "GitHub Action architectural drift tests passed."
