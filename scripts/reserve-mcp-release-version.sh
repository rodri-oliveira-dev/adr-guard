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

# Do not use `gh api --jq ... || true` for existence: some 404 responses
# are emitted as a JSON error body and can be mistaken for a tag SHA.
if ! http_status="$(curl --proto '=https' --tlsv1.2 --silent --show-error \
  --connect-timeout 10 --max-time 30 --retry 2 \
  --header "Authorization: Bearer ${GH_TOKEN}" \
  --header 'Accept: application/vnd.github+json' \
  --output "$response_file" --write-out '%{http_code}' \
  "https://api.github.com/repos/${GITHUB_REPOSITORY}/git/ref/tags/${RESERVATION_TAG}")"
then
  echo '::error::Unable to query the MCP reservation tag.' >&2
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
    created="$(gh api --method POST "repos/${GITHUB_REPOSITORY}/git/refs" \
      -f "ref=refs/tags/${RESERVATION_TAG}" -f "sha=${VALIDATED_SHA}")" || {
      echo '::error::Unable to create MCP reservation (possible concurrent release or permissions error).' >&2
      exit 1
    }
    if ! jq -e --arg ref "refs/tags/${RESERVATION_TAG}" --arg sha "$VALIDATED_SHA" \
      '.ref == $ref and .object.type == "commit" and .object.sha == $sha' <<< "$created" > /dev/null
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
