#!/usr/bin/env bash
set -euo pipefail

: "${MCP_PACKAGE_ID:?MCP package identifier is required}"
: "${VERSION:?MCP version is required}"
: "${VALIDATED_SHA:?Validated commit SHA is required}"

[[ "$MCP_PACKAGE_ID" == "RodriOliveira.AdrGuard.Mcp" ]] || {
  echo '::error::Unexpected MCP package ID.' >&2
  exit 1
}
[[ "$VERSION" =~ ^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$ ]] || {
  echo '::error::MCP version must be stable SemVer.' >&2
  exit 1
}
[[ "$VALIDATED_SHA" =~ ^[0-9a-f]{40}$ ]] || {
  echo '::error::Invalid MCP release commit SHA.' >&2
  exit 1
}

package="artifacts/mcp/${MCP_PACKAGE_ID}.${VERSION}.nupkg"
sums="artifacts/mcp/SHA256SUMS"
[[ -s "$package" && -s "$sums" ]] || {
  echo '::error::Expected immutable MCP .nupkg and SHA256SUMS artifacts.' >&2
  exit 1
}

# Verify the exact .nupkg preserved by the release/candidate checksum manifest.
# Avoid running sha256sum -c on the whole manifest: it also contains the
# separate Registry manifest, which need not be present for GitHub Packages.
matching="$(awk -v path="$package" '$2 == path { print }' "$sums")"
[[ "$(printf '%s\n' "$matching" | grep -c .)" == 1 ]] || {
  echo '::error::Expected exactly one checksum for the MCP package.' >&2
  exit 1
}
printf '%s\n' "$matching" | sha256sum --check --status - || {
  echo '::error::MCP package does not match the published release checksum.' >&2
  exit 1
}

nuspec="$(unzip -p "$package" "${MCP_PACKAGE_ID}.nuspec")" || {
  echo '::error::Unable to read MCP NuGet metadata.' >&2
  exit 1
}
grep -Fq "<id>${MCP_PACKAGE_ID}</id>" <<< "$nuspec" &&
  grep -Fq "<version>${VERSION}</version>" <<< "$nuspec" &&
  grep -Fq "commit=\"${VALIDATED_SHA}\"" <<< "$nuspec" || {
    echo '::error::MCP package ID, version, or commit provenance differs.' >&2
    exit 1
  }

echo "Verified MCP ${VERSION} package checksum and commit provenance."
