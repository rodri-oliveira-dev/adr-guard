#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TEMP_DIR="$(mktemp -d)"
trap 'rm -rf -- "${TEMP_DIR}"' EXIT

WORKSPACE="${TEMP_DIR}/workspace with spaces"
ADR_DIR="${WORKSPACE}/docs/adr"
SUMMARY="${TEMP_DIR}/summary.md"
STDERR_LOG="${TEMP_DIR}/validator.stderr"
OUTPUT="${TEMP_DIR}/report.stdout"
mkdir -p "$ADR_DIR"
touch "$ADR_DIR/0001-invalid.md" "$ADR_DIR/0002-other.md"
REPORT="${ROOT_DIR}/scripts/github-action-report.sh"

report() {
  GITHUB_STEP_SUMMARY="$SUMMARY" bash "$REPORT" "$1" "$WORKSPACE" "$ADR_DIR" "$STDERR_LOG" check >"$OUTPUT"
}

assert_annotations() {
  local expected="$1"
  local actual
  actual="$(grep -c '^::error file=' "$OUTPUT" || true)"
  if [[ "$actual" != "$expected" ]]; then
    echo "Expected $expected file annotations; got $actual" >&2
    cat "$OUTPUT" >&2
    exit 1
  fi
}

# Known-invalid ADR: real file association, no invented line, escaped message.
printf '/workspace/docs/adr/0001-invalid.md: ADR005 Missing %% section\r::warning::forged\nValidation failed with 1 issue(s).\n' >"$STDERR_LOG"
report 1
assert_annotations 1
grep -Fq '::error file=docs/adr/0001-invalid.md,title=ADR005::ADR005 Missing %25 section%0D::warning::forged' "$OUTPUT"
if grep -q 'line=' "$OUTPUT"; then
  echo "File-level diagnostics must not fabricate a line number." >&2
  exit 1
fi
grep -Fq '| ADR005 | 1 |' "$SUMMARY"

# No annotations on a successful run even when stdout/stderr contains ADR-looking text.
: >"$SUMMARY"
report 0
assert_annotations 0
grep -Fq '### ADR Guard — Success' "$SUMMARY"

# Neither operational failure nor arbitrary output may be attributed to ADR rules.
: >"$SUMMARY"
report 3
assert_annotations 0
grep -Fq 'Operational error' "$SUMMARY"

printf 'Unexpected runtime output\n/workspace/docs/adr/0001-invalid.md: ADR005 Looks valid\nValidation failed with 1 issue(s).\n' >"$STDERR_LOG"
: >"$SUMMARY"
report 1
assert_annotations 0
grep -Fq 'unexpected CLI output' "$SUMMARY"

# A crafted relative path, an outside file, or a forged count must not annotate.
printf '/workspace/docs/adr/../../outside.md: ADR005 Bad path\nValidation failed with 1 issue(s).\n' >"$STDERR_LOG"
report 1
assert_annotations 0

printf '/workspace/docs/adr/0001-invalid.md: ADR005 Missing section\nValidation failed with 2 issue(s).\n' >"$STDERR_LOG"
report 1
assert_annotations 0

# A new line attempting to smuggle a workflow command invalidates the batch.
printf '/workspace/docs/adr/0001-invalid.md: ADR005 Missing section\n::error::injected\nValidation failed with 1 issue(s).\n' >"$STDERR_LOG"
report 1
assert_annotations 0

# Repeated diagnostics are counted, while the annotation cap prevents log floods.
: >"$STDERR_LOG"
for ((i=0; i<55; i++)); do
  printf '/workspace/docs/adr/0001-invalid.md: ADR005 Missing section %s\n' "$i" >>"$STDERR_LOG"
done
printf 'Validation failed with 55 issue(s).\n' >>"$STDERR_LOG"
: >"$SUMMARY"
report 1
assert_annotations 50
grep -Fq '| Validated diagnostic count | 55 |' "$SUMMARY"
grep -Fq '| File annotations | 50 (cap: 50) |' "$SUMMARY"
grep -Fq '| ADR005 | 55 |' "$SUMMARY"

echo "GitHub Action reporting tests passed."
