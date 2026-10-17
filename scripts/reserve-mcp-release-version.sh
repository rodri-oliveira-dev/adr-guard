#!/usr/bin/env bash
set -euo pipefail

: "${GH_TOKEN:?GitHub token is required}"
: "${GITHUB_REPOSITORY:?Repository is required}"
: "${RESERVATION_TAG:?Reservation tag is required}"
: "${VALIDATED_SHA:?Validated SHA is required}"

if [[ ! "$GITHUB_REPOSITORY" =~ ^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$ ]]
then
  echo '::error::Invalid repository identifier.' >&2
  exit 1
fi
if [[ ! "$RESERVATION_TAG" =~ ^mcp-release-reservation/v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$ ]]
then
  echo '::error::Invalid MCP reservation tag.' >&2
  exit 1
fi
if [[ ! "$VALIDATED_SHA" =~ ^[0-9a-f]{40}$ ]]
then
  echo '::error::Invalid validated commit SHA.' >&2
  exit 1
fi

response_file="$(mktemp)"
trap 'rm -f "$response_file"' EXIT

# GitHub access tokens contain token-safe ASCII characters. Reject config
# syntax before writing the secret to curl's stdin (never process argv).
if [[ ! "$GH_TOKEN" =~ ^[A-Za-z0-9_.-]+$ ]]; then
  echo '::error::Invalid GitHub token format.' >&2
  exit 1
fi

# HTTP status, not jq output, determines whether a reservation exists.
# Curl receives its Authorization header through stdin configuration, so
# credentials are absent from argv. Both responses have a 64 KiB byte limit.
github_request() {
  local method="$1" output="$2" url="$3"
  shift 3
  printf 'header = "Authorization: Bearer %s"\n' "$GH_TOKEN" |
    curl --config - --proto '=https' --tlsv1.2 --silent --show-error \
      --connect-timeout 10 --max-time 30 --retry 2 --max-filesize 65536 \
      --header 'Accept: application/vnd.github+json' \
      --request "$method" --output "$output" --write-out '%{http_code}' "$@" "$url"
}

api_url="https://api.github.com/repos/$GITHUB_REPOSITORY"
if ! http_status="$(github_request GET "$response_file" "$api_url/git/ref/tags/$RESERVATION_TAG")"; then
  echo '::error::Unable to query the MCP reservation tag or response exceeds 65536 bytes.' >&2
  exit 1
fi
case "$http_status" in
  200)
    existing_sha="$(jq -er '.object | select(.type == "commit") | .sha | select(type == "string")' "$response_file")" || {
      echo '::error::Existing reservation has invalid Git reference metadata.' >&2
      exit 1
    }
    if [[ ! "$existing_sha" =~ ^[0-9a-f]{40}$ || "$existing_sha" != "$VALIDATED_SHA" ]]
    then
      echo "::error::Reservation belongs to a different commit or is invalid: ${existing_sha}." >&2
      exit 1
    fi
    echo 'Existing MCP reservation matches the validated commit.'
    ;;
  404)
    request_body="$(jq -nc --arg ref "refs/tags/$RESERVATION_TAG" --arg sha "$VALIDATED_SHA" '{ref: $ref, sha: $sha}')"
    if ! create_status="$(github_request POST "$response_file" "$api_url/git/refs" \
      --header 'Content-Type: application/json' --data-raw "$request_body")"; then
      echo '::error::Unable to create MCP reservation or response exceeds 65536 bytes.' >&2
      exit 1
    fi
    if [[ "$create_status" != 201 ]]; then
      echo "::error::MCP reservation creation returned HTTP $create_status; refusing to publish." >&2
      exit 1
    fi
    if ! jq -e --arg ref "refs/tags/$RESERVATION_TAG" --arg sha "$VALIDATED_SHA" \
      '.ref == $ref and .object.type == "commit" and .object.sha == $sha' "$response_file" > /dev/null
    then
      echo '::error::Created reservation did not match the requested tag and commit.' >&2
      exit 1
    fi
    echo 'Created immutable MCP version reservation.'
    ;;
  *)
    echo "::error::GitHub reservation lookup failed with HTTP ${http_status}; refusing to publish." >&2
    exit 1
    ;;
esac
