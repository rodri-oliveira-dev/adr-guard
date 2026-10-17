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
output="" method="" max_bytes="" config_stdin=false request_body=""
while (( $# > 0 )); do
  case "$1" in
    --output|--request|--max-filesize|--config|--data-raw)
      option="$1" value="$2"
      case "$option" in
        --output) output="$value" ;;
        --request) method="$value" ;;
        --max-filesize) max_bytes="$value" ;;
        --config) [[ "$value" == - ]] && config_stdin=true ;;
        --data-raw) request_body="$value" ;;
      esac
      shift 2
      ;;
    *)
      if [[ "$1" == *"$GH_TOKEN"* ]]; then
        echo 'Sensitive token appeared in curl argv!' >&2
        exit 90
      fi
      shift
      ;;
  esac
done
[[ -n "$output" && "$config_stdin" == true && "$max_bytes" == 65536 ]]
header="$(cat)"
[[ "$header" == "header = \"Authorization: Bearer $GH_TOKEN\"" ]] || {
  echo 'Credential must arrive over curl stdin config, not process arguments.' >&2
  exit 91
}
printf '%s\n' "$method" >> "$MOCK_CURL_CALLS"
if [[ "$MOCK_TRANSPORT_FAIL" == true ]]; then exit 7; fi
if [[ "$method" == GET ]]; then
  status="$MOCK_GET_STATUS"
  body="$MOCK_GET_BODY"
elif [[ "$method" == POST ]]; then
  jq -e --arg ref "refs/tags/$RESERVATION_TAG" --arg sha "$VALIDATED_SHA" \
    '.ref == $ref and .sha == $sha' <<< "$request_body" > /dev/null
  status="$MOCK_POST_STATUS"
  body="$MOCK_POST_BODY"
  if [[ "$MOCK_POST_FAIL" == true ]]; then exit 7; fi
else
  exit 92
fi
# An oversized body with no Content-Length must still be rejected.
if (( ${#body} > max_bytes )); then exit 63; fi
printf '%s' "$body" > "$output"
printf '%s' "$status"
MOCK_CURL
chmod +x "$fixture/bin/curl"

export PATH="$fixture/bin:$PATH"
export GH_TOKEN='placeholder-test-token'
export GITHUB_REPOSITORY='rodri-oliveira-dev/adr-guard'
export RESERVATION_TAG='mcp-release-reservation/v1.0.0'
export VALIDATED_SHA='941e05dfb739f52a64e87d990b91e3e399fd0041'
export MOCK_CURL_CALLS="$fixture/curl-calls"
export MOCK_GET_STATUS=404 MOCK_GET_BODY='{"message":"Not Found","status":"404"}'
export MOCK_POST_STATUS=201 MOCK_POST_FAIL=false MOCK_TRANSPORT_FAIL=false
export MOCK_POST_BODY="{\"ref\":\"refs/tags/$RESERVATION_TAG\",\"object\":{\"type\":\"commit\",\"sha\":\"$VALIDATED_SHA\"}}"

run_case() {
  local label="$1" expected="$2" expected_calls="$3"
  : > "$MOCK_CURL_CALLS"
  if bash "$RESERVE" > "$fixture/out" 2>&1; then
    [[ "$expected" == success ]] || {
      echo "Unexpected success: $label" >&2
      cat "$fixture/out" >&2
      exit 1
    }
  else
    [[ "$expected" == failure ]] || {
      echo "Unexpected failure: $label" >&2
      cat "$fixture/out" >&2
      exit 1
    }
  fi
  [[ "$(wc -l < "$MOCK_CURL_CALLS" | tr -d ' ')" == "$expected_calls" ]] || {
    echo "Unexpected HTTP call count for $label" >&2
    cat "$fixture/out" >&2
    exit 1
  }
}

run_case 'missing reservation creates tag' success 2
grep -Fxq GET "$MOCK_CURL_CALLS"
grep -Fxq POST "$MOCK_CURL_CALLS"

export MOCK_GET_STATUS=200
export MOCK_GET_BODY="{\"object\":{\"type\":\"commit\",\"sha\":\"$VALIDATED_SHA\"}}"
run_case 'same commit is idempotent' success 1

export MOCK_GET_BODY='{"object":{"type":"commit","sha":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"}}'
run_case 'different commit is rejected' failure 1

export MOCK_GET_BODY='{"message":"Not Found","status":"404"}'
run_case 'error JSON with HTTP 200 is not a SHA' failure 1

export MOCK_GET_BODY="{\"object\":{\"type\":\"tag\",\"sha\":\"$VALIDATED_SHA\"}}"
run_case 'annotated tag cannot impersonate lightweight reservation' failure 1

for status in 401 403 429 500 503; do
  export MOCK_GET_STATUS="$status"
  run_case "HTTP $status fails closed" failure 1
done

export MOCK_GET_STATUS=404 MOCK_GET_BODY='{"message":"Not Found"}'
export MOCK_POST_STATUS=403
run_case 'failed POST authorization is rejected' failure 2
export MOCK_POST_STATUS=201

export MOCK_POST_FAIL=true
run_case 'failed POST transport is rejected' failure 2
export MOCK_POST_FAIL=false

export MOCK_POST_BODY='{"ref":"refs/tags/mcp-release-reservation/v1.0.0","object":{"type":"commit","sha":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"}}'
run_case 'inconsistent tag creation response is rejected' failure 2

export MOCK_POST_BODY="{\"ref\":\"refs/tags/$RESERVATION_TAG\",\"object\":{\"type\":\"commit\",\"sha\":\"$VALIDATED_SHA\"}}"
export MOCK_TRANSPORT_FAIL=true
run_case 'GitHub GET transport failure is rejected' failure 1
export MOCK_TRANSPORT_FAIL=false

# Enforce byte limits for responses without Content-Length, both methods.
export MOCK_GET_STATUS=200
export MOCK_GET_BODY="$(printf '%065537d' 0)"
run_case 'oversized GET response is rejected' failure 1

export MOCK_GET_STATUS=404 MOCK_GET_BODY='{"message":"Not Found"}'
export MOCK_POST_BODY="$(printf '%065537d' 0)"
run_case 'oversized POST response is rejected' failure 2

# Reject untrusted token characters before they become curl config syntax.
export GH_TOKEN=$'bad-token\nheader = "X-Injected: true"'
run_case 'untrusted token characters are rejected' failure 0

echo 'MCP reservation security and HTTP-status regression tests passed.'
