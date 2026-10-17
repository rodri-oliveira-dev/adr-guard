#!/usr/bin/env bash
set -euo pipefail

: "${NUGET_API_KEY:?GitHub Packages token is required}"
: "${GITHUB_ACTOR:?GitHub actor is required}"
: "${GITHUB_REPOSITORY_OWNER:?GitHub package owner is required}"
: "${GITHUB_PACKAGE_SOURCE:?GitHub package source is required}"

# The shared package verification checks the original checksum, Package ID,
# release version and commit before any registry operation.
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
bash "$script_dir/verify-mcp-github-package.sh"

[[ "$GITHUB_REPOSITORY_OWNER" =~ ^[A-Za-z0-9_.-]+$ ]] || exit 1
[[ "$GITHUB_ACTOR" =~ ^[A-Za-z0-9_.-]+$ ]] || exit 1
[[ "$NUGET_API_KEY" =~ ^[A-Za-z0-9_.-]+$ ]] || {
  echo '::error::Invalid GitHub token format.' >&2
  exit 1
}
[[ "$GITHUB_PACKAGE_SOURCE" == "https://nuget.pkg.github.com/$GITHUB_REPOSITORY_OWNER/index.json" ]] || {
  echo '::error::Unexpected GitHub NuGet feed.' >&2
  exit 1
}

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
local_package="artifacts/mcp/$MCP_PACKAGE_ID.$VERSION.nupkg"
expected_sha="$(sha256sum "$local_package" | cut -d ' ' -f 1)"

# Pass credentials on stdin, NEVER argv, even for read-only downloads.
# Use HTTPS and bound response sizes and network time.
fetch_package_resource() {
  local url="$1" output="$2" maxbytes="$3"
  local authorization
  authorization="$(printf '%s:%s' "$GITHUB_ACTOR" "$NUGET_API_KEY" | base64 | tr -d '\r\n')"
  printf 'header = "Authorization: Basic %s"\n' "$authorization" |
    curl --config - --proto '=https' --tlsv1.2 \
      --silent --show-error --location --connect-timeout 10 --max-time 90 \
      --retry 2 --max-filesize "$maxbytes" \
      --output "$output" --write-out '%{http_code}' "$url"
}

if ! status="$(fetch_package_resource "$GITHUB_PACKAGE_SOURCE" "$tmp/index.json" 65536)"; then
  echo '::error::Unable to read bounded GitHub Packages service index.' >&2
  exit 1
fi
[[ "$status" == 200 ]] || {
  echo "::error::GitHub Packages service index returned HTTP $status." >&2
  exit 1
}
base="$(jq -er '[.resources[] | select((.["@type"] | tostring) | contains("PackageBaseAddress/3.0.0")) | .["@id"] | select(type == "string")][0]' "$tmp/index.json")" || {
  echo '::error::GitHub NuGet service index lacks PackageBaseAddress.' >&2
  exit 1
}
# Prevent a compromised feed index from redirecting the credential to
# another host or account.
[[ "$base" == "https://nuget.pkg.github.com/$GITHUB_REPOSITORY_OWNER/"* ]] || {
  echo '::error::Unexpected GitHub Packages content endpoint.' >&2
  exit 1
}
lower_id="$(printf '%s' "$MCP_PACKAGE_ID" | tr '[:upper:]' '[:lower:]')"
package_url="${base%/}/$lower_id/$VERSION/$lower_id.$VERSION.nupkg"

verify_existing() {
  local status
  if ! status="$(fetch_package_resource "$package_url" "$tmp/published.nupkg" 25000000)"; then
    echo '::error::Unable to verify bounded GitHub Packages payload.' >&2
    return 2
  fi
  case "$status" in
    200)
      local actual
      actual="$(sha256sum "$tmp/published.nupkg" | cut -d ' ' -f 1)"
      if [[ "$actual" != "$expected_sha" ]]; then
        echo "::error::Immutable GitHub Packages version $VERSION has a different SHA-256." >&2
        return 2
      fi
      echo "Verified existing GitHub Packages MCP $VERSION matches immutable release SHA-256."
      return 0
      ;;
    404) return 1 ;;
    *)
      echo "::error::GitHub Packages lookup failed with HTTP $status." >&2
      return 2
      ;;
  esac
}

if verify_existing; then
  exit 0
else
  status=$?
  [[ "$status" == 1 ]] || exit 1
fi

# Deliberately omit --skip-duplicate: a concurrent 409 must fail rather than
# silently accepting an unverified immutable package.
# SDK 10.0.402 supports NUGET_API_KEY via environment, avoiding token argv.
dotnet nuget push "$local_package" --source "$GITHUB_PACKAGE_SOURCE"

# Account for short registry propagation, but never accept a missing or
# non-matching version as proof of successful publication.
for attempt in {1..12}; do
  if verify_existing; then
    echo "Published MCP $VERSION to GitHub Packages and verified original bytes."
    exit 0
  else
    result=$?
    [[ "$result" == 1 ]] || exit 1
  fi
  [[ "$attempt" != 12 ]] || break
  sleep 10
done
echo '::error::Published MCP package was not retrievable from GitHub Packages.' >&2
exit 1
