#!/usr/bin/env bash
set -euo pipefail

# End-to-end regression of the INSTALLED .NET Tool (not only its test assembly).
# Templates live outside the ADR output directory and no live AI credentials are used.
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BIN="${ADR_GUARD_BIN:-${ROOT}/.tools/adr-guard}"
TMP="$(mktemp -d)"
trap 'rm -rf -- "${TMP}"' EXIT
ADR_DIR="${TMP}/decision records"
INPUT_DIR="${TMP}/template inputs"
mkdir -p "${ADR_DIR}" "${INPUT_DIR}"
cp "${ROOT}/tests/AdrGuard.Tests/Fixtures/Templates/custom-template.en-US.md" "${INPUT_DIR}/custom.md"

assert_code() {
  local expected="$1"
  shift
  local actual=0
  "$@" >"${TMP}/command.stdout" 2>"${TMP}/command.stderr" || actual=$?
  if [[ "${actual}" -ne "${expected}" ]]; then
    echo "Expected exit ${expected}, got ${actual}: $*" >&2
    cat "${TMP}/command.stdout" "${TMP}/command.stderr" >&2
    exit 1
  fi
}

# Four real, offline creation modes in one directory: allocation must be monotonic
# and all drafts must remain canonical and independently validator-compliant.
assert_code 0 "${BIN}" new "${ADR_DIR}" --title "Adopt Redis" --template minimal --culture en-US
assert_code 0 "${BIN}" new "${ADR_DIR}" --title "Adopt Kafka" --template extended --culture pt-BR
assert_code 0 "${BIN}" new "${ADR_DIR}" --title "Adopt Cache" --template-file "${INPUT_DIR}/custom.md"
assert_code 0 "${BIN}" new "${ADR_DIR}" --title "Adotar Mensageria" --culture pt-BR

test -s "${ADR_DIR}/0001-adopt-redis.md"
test -s "${ADR_DIR}/0002-adopt-kafka.md"
test -s "${ADR_DIR}/0003-adopt-cache.md"
test -s "${ADR_DIR}/0004-adotar-mensageria.md"
grep -Fq 'ADR 0003' "${ADR_DIR}/0003-adopt-cache.md"
grep -Fq '## Decision Drivers' "${ADR_DIR}/0002-adopt-kafka.md"
grep -Fq '[EDITAR:' "${ADR_DIR}/0004-adotar-mensageria.md"
grep -Fq 'Proposed' "${ADR_DIR}/0003-adopt-cache.md"
assert_code 0 "${BIN}" check "${ADR_DIR}"
test "$(find "${ADR_DIR}" -maxdepth 1 -name '*.md' | wc -l)" -eq 4

# Preview must not allocate/reserve an ID, write README or create temp files.
snapshot_before="$(find "${ADR_DIR}" -type f -print0 | sort -z | xargs -0 sha256sum)"
assert_code 0 "${BIN}" new "${ADR_DIR}" --title "Try Preview" --template extended --preview
grep -Fq '0005-try-preview.md' "${TMP}/command.stdout"
grep -Fq '## Decision Drivers' "${TMP}/command.stdout"
snapshot_after="$(find "${ADR_DIR}" -type f -print0 | sort -z | xargs -0 sha256sum)"
test "${snapshot_before}" = "${snapshot_after}"
test ! -e "${ADR_DIR}/0005-try-preview.md"
test ! -e "${ADR_DIR}/README.md"

# Existing index/check contracts, determinism, and read-only check behavior.
assert_code 0 "${BIN}" index "${ADR_DIR}"
test -s "${ADR_DIR}/README.md"
grep -Fq '[0003](0003-adopt-cache.md)' "${ADR_DIR}/README.md"
index_hash="$(sha256sum "${ADR_DIR}/README.md")"
assert_code 0 "${BIN}" index "${ADR_DIR}"
test "${index_hash}" = "$(sha256sum "${ADR_DIR}/README.md")"
assert_code 0 "${BIN}" check "${ADR_DIR}"

# Error codes must stay stable, and bad inputs never create an ADR.
assert_code 2 "${BIN}" new "${ADR_DIR}" --title "Bad" --template unknown
assert_code 2 "${BIN}" new "${ADR_DIR}" --title "Bad" --template minimal --template-file "${INPUT_DIR}/custom.md"
assert_code 2 "${BIN}" new "${ADR_DIR}" --title "Bad" --culture fr-FR
assert_code 3 "${BIN}" new "${ADR_DIR}" --title "Bad" --template-file "${INPUT_DIR}/missing.md"
printf '# Invalid template\n' >"${INPUT_DIR}/malformed.md"
assert_code 3 "${BIN}" new "${ADR_DIR}" --title "Bad" --template-file "${INPUT_DIR}/malformed.md"
test ! -e "${ADR_DIR}/0005-bad.md"
test ! -e "${ADR_DIR}/0005-try-preview.md"
test "$(find "${ADR_DIR}" -maxdepth 1 -name '[0-9]*.md' | wc -l)" -eq 4
test -z "$(find "${ADR_DIR}" -type f \( -name '*.tmp' -o -iname '*reservation*' -o -iname '*lock*' \) -print -quit)"

mkdir -p "${TMP}/invalid"
printf '# Incomplete\n\n## Status\n\nProposed\n' >"${TMP}/invalid/0001-incomplete.md"
assert_code 1 "${BIN}" check "${TMP}/invalid"
assert_code 1 "${BIN}" index "${TMP}/invalid"
test ! -e "${TMP}/invalid/README.md"

echo 'Installed .NET Tool template/CLI/check/index regression passed (no AI credentials).'
