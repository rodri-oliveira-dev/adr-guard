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

# Documentation examples are canonical, actual CLI output — not hand-waved snippets.
# Each sample has its own directory because every example starts at ID 0001.
run_documented_example() {
  local name="$1"
  local title="$2"
  local fixture="$3"
  shift 3
  local output_dir="${TMP}/documented-${name}"
  mkdir -p "${output_dir}"
  assert_code 0 "${BIN}" new "${output_dir}" --title "${title}" "$@"
  cmp "${output_dir}/${fixture}" "${ROOT}/docs/examples/generated/${name}/${fixture}"
  assert_code 0 "${BIN}" check "${output_dir}"
  assert_code 0 "${BIN}" check "${ROOT}/docs/examples/generated/${name}"
}
run_documented_example minimal "Adopt Redis" 0001-adopt-redis.md --template minimal --culture en-US
run_documented_example extended "Adotar Redis" 0001-adotar-redis.md --template extended --culture pt-BR
run_documented_example custom "Adopt Cache" 0001-adopt-cache.md --template-file "${ROOT}/docs/examples/templates/team.en-US.md" --culture en-US
run_documented_example custom-pt-BR "Adotar Cache" 0001-adotar-cache.md --template-file "${ROOT}/docs/examples/templates/team.pt-BR.md" --culture pt-BR

# Documentation parity and live-reference guard: both entrypoints must link the
# runnable guides/examples without claiming new/draft are Action commands.
for guide in "${ROOT}/README.md" "${ROOT}/README.pt-BR.md"; do
  grep -Fq 'docs/examples/generated/minimal/0001-adopt-redis.md' "${guide}"
  grep -Fq 'docs/examples/generated/extended/0001-adotar-redis.md' "${guide}"
  grep -Fq 'docs/examples/generated/custom/0001-adopt-cache.md' "${guide}"
  grep -Fq 'docs/examples/generated/custom-pt-BR/0001-adotar-cache.md' "${guide}"
  grep -Fq -- '--template-file' "${guide}"
  grep -Fq -- '--culture' "${guide}"
  grep -Fq -- '--preview' "${guide}"
  grep -Fq -- '--dry-run' "${guide}"
  grep -Fq -- '--include-existing-adrs' "${guide}"
  grep -Fq 'adr-guard check docs/adr' "${guide}"
  grep -Fq 'adr-guard index docs/adr' "${guide}"
done
for guide in "${ROOT}/docs/creation.md" "${ROOT}/docs/creation.pt-BR.md"; do
  grep -Fq -- '--template-file' "${guide}"
  grep -Fq -- '--dry-run' "${guide}"
  grep -Fq -- '--preview' "${guide}"
  grep -Fq '65,536' "${guide}" || grep -Fq '65.536' "${guide}"
  grep -Fq 'docs/examples/generated/minimal' "${guide}" || grep -Fq 'examples/generated/minimal' "${guide}"
  grep -Fq 'docs/examples/generated/custom' "${guide}" || grep -Fq 'examples/generated/custom' "${guide}"
done

echo 'Documented EN/pt-BR Minimal, Extended and Custom outputs match installed CLI exactly and pass check.'

echo 'Installed .NET Tool template/CLI/check/index regression passed (no AI credentials).'
