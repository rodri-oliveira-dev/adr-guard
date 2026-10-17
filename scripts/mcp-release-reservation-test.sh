#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WORKFLOW="${ROOT_DIR}/.github/workflows/mcp-release.yml"
RESERVE="${ROOT_DIR}/scripts/reserve-mcp-release-version.sh"

grep -Fq 'bash scripts/reserve-mcp-release-version.sh' "$WORKFLOW" || {
  echo 'MCP Release workflow must invoke the tested reservation script.' >&2
  exit 1
}
grep -Fq 'needs: [validate-release, build-test-pack, install-smoke]' "$WORKFLOW" || {
  echo 'MCP reservation must be gated by validation, build, and platform smoke tests.' >&2
  exit 1
}

fixture="$(mktemp -d)"
trap 'rm -rf "$fixture"' EXIT
mkdir -p "$fixture/bin"
cat > "$fixture/bin/curl" <<'MOCK_CURL'
#!/usr/bin/env bash
set -euo pipefail
output=""
while (( $# > 0 )); do
  if [[ "$1" == '--output' ]]; then output="$2"; shift 2; continue; fi
  shift
done
[[ -n "$output" ]]
if [[ "${MOCK_TRANSPORT_FAIL:-false}" == true ]]; then exit 7; fi
printf '%s' "${MOCK_GET_BODY:-}" > "$output"
printf '%s' "$MOCK_GET_STATUS"
MOCK_CURL
cat > "$fixture/bin/gh" <<'MOCK_GH'
#!/usr/bin/env bash
set -euo pipefail
[[ "$1" == api && "$2" == --method && "$3" == POST ]]
printf '%s\n' "$@" >> "$MOCK_GH_CALLS"
if [[ "${MOCK_POST_FAIL:-false}" == true ]]; then exit 1; fi
printf '{"ref":"refs/tags/mcp-release-reservation/v1.0.0","object":{"type":"commit","sha":"%s"}}\n' "${MOCK_CREATED_SHA:-$VALIDATED_SHA}"
MOCK_GH
chmod +x "$fixture/bin/curl" "$fixture/bin/gh"

export PATH="$fixture/bin:$PATH"
export GH_TOKEN='placeholder-test-token'
export GITHUB_REPOSITORY='rodri-oliveira-dev/adr-guard'
export RESERVATION_TAG='mcp-release-reservation/v1.0.0'
export VALIDATED_SHA='941e05dfb739f52a64e87d990b91e3e399fd0041'
export MOCK_GH_CALLS="$fixture/gh-calls"

run_case() {
  local label="$1" expected="$2"
  : > "$MOCK_GH_CALLS"
  if bash "$RESERVE" > "$fixture/out" 2>&1; then
    [[ "$expected" == success ]] || { echo "Unexpected success: $label" >&2; cat "$fixture/out" >&2; exit 1; }
  else
    [[ "$expected" == failure ]] || { echo "Unexpected failure: $label" >&2; cat "$fixture/out" >&2; exit 1; }
  fi
}

export MOCK_GET_STATUS=404 MOCK_GET_BODY='{"message":"Not Found","status":"404"}'
run_case 'missing reservation creates tag' success
grep -Fq "sha=${VALIDATED_SHA}" "$MOCK_GH_CALLS"
grep -Fq "ref=refs/tags/${RESERVATION_TAG}" "$MOCK_GH_CALLS"

export MOCK_GET_STATUS=200
export MOCK_GET_BODY="{\"object\":{\"type\":\"commit\",\"sha\":\"${VALIDATED_SHA}\"}}"
run_case 'same commit is idempotent' success
test ! -s "$MOCK_GH_CALLS"

export MOCK_GET_BODY='{"object":{"type":"commit","sha":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"}}'
run_case 'different commit is rejected' failure
test ! -s "$MOCK_GH_CALLS"

export MOCK_GET_BODY='{"message":"Not Found","status":"404"}'
run_case 'error JSON with HTTP 200 is not a SHA' failure
test ! -s "$MOCK_GH_CALLS"

export MOCK_GET_BODY="{\"object\":{\"type\":\"tag\",\"sha\":\"${VALIDATED_SHA}\"}}"
run_case 'annotated tag cannot impersonate lightweight reservation' failure
test ! -s "$MOCK_GH_CALLS"

for status in 401 403 429 500 503; do
  export MOCK_GET_STATUS="$status"
  run_case "HTTP $status fails closed" failure
  test ! -s "$MOCK_GH_CALLS"
done

export MOCK_GET_STATUS=404 MOCK_GET_BODY='{"message":"Not Found"}'
export MOCK_POST_FAIL=true
run_case 'failed reservation creation is rejected' failure
unset MOCK_POST_FAIL

export MOCK_CREATED_SHA='aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'
run_case 'inconsistent tag creation response is rejected' failure
unset MOCK_CREATED_SHA

export MOCK_TRANSPORT_FAIL=true
run_case 'GitHub transport failure is rejected' failure
unset MOCK_TRANSPORT_FAIL

echo 'MCP reservation HTTP-status and immutability regression tests passed.'
