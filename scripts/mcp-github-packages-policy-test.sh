#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WORKFLOW="$ROOT_DIR/.github/workflows/mcp-release.yml"
BACKFILL="$ROOT_DIR/.github/workflows/mcp-github-packages-backfill.yml"
VERIFY="$ROOT_DIR/scripts/verify-mcp-github-package.sh"
CI="$ROOT_DIR/.github/workflows/ci.yml"

job_block() {
  local job="$1"
  awk -v header="  ${job}:" '
    $0 == header { capture=1 }
    capture && $0 ~ /^  [A-Za-z0-9_-]+:$/ && $0 != header { exit }
    capture { print }
  ' "$WORKFLOW"
}

future="$(job_block publish-github-packages)"
[[ -n "$future" ]] || { echo 'Missing MCP GitHub Packages job.' >&2; exit 1; }
for expected in \
  'needs: [validate-release, build-test-pack, reserve-version, verify-public-package]' \
  'environment: mcp-release' \
  'packages: write' \
  'contents: read' \
  'ref: ${{ needs.validate-release.outputs.sha }}' \
  'bash scripts/verify-mcp-github-package.sh' \
  'bash scripts/publish-mcp-github-package.sh' \
  'NUGET_API_KEY: ${{ github.token }}'
do
  grep -Fq -- "$expected" <<< "$future" || {
    echo "Missing MCP package distribution policy: $expected" >&2
    exit 1
  }
done

summary="$(job_block release-summary)"
grep -Fq 'publish-github-packages' <<< "$summary" || {
  echo 'MCP release summary must wait for GitHub Packages.' >&2
  exit 1
}
grep -Fq 'workflow_dispatch:' "$BACKFILL"
grep -Fq "github.ref == 'refs/heads/main'" "$BACKFILL"
grep -Fq 'environment: mcp-release' "$BACKFILL"
grep -Fq 'packages: write' "$BACKFILL"
grep -Fq 'gh release download "$RELEASE_TAG"' "$BACKFILL"
grep -Fq 'scripts/verify-mcp-github-package.sh' "$BACKFILL"
grep -Fq 'https://api.nuget.org/v3-flatcontainer/' "$BACKFILL"
grep -Fq 'NUGET_API_KEY: ${{ github.token }}' "$BACKFILL"
grep -Fq 'scripts/publish-mcp-github-package.sh' "$BACKFILL"
grep -Fq '  validate-main-ci:' "$BACKFILL"
grep -Fq 'needs: validate-main-ci' "$BACKFILL"
grep -Fq 'actions: read' "$BACKFILL"
grep -Fq 'github.sha' "$BACKFILL"
grep -Fq 'head_sha=$VALIDATED_SHA&event=push&status=completed' "$BACKFILL"
grep -Fq 'select(.head_sha == $sha and .head_branch == "main" and .event == "push"' "$BACKFILL"
grep -Fq 'if: ${{ always() }}' <<< "$summary"
grep -Fq 'needs.publish-github-packages.result' <<< "$summary"
if grep -Fq -- '--skip-duplicate' "$BACKFILL" || grep -Fq -- '--skip-duplicate' <<< "$future"; then
  echo 'GitHub Packages publication must never silently skip an unverified conflict.' >&2
  exit 1
fi
if grep -Fq -- '--api-key' "$BACKFILL" || grep -Fq -- '--api-key' <<< "$future"; then
  echo 'GitHub Packages token must not appear in dotnet argv.' >&2
  exit 1
fi
if grep -Eq '^  (push|pull_request|workflow_run):' "$BACKFILL"; then
  echo 'Backfill must not publish automatically.' >&2
  exit 1
fi
grep -Fq 'bash scripts/mcp-github-packages-policy-test.sh' "$CI"

bash -n "$VERIFY"
bash -n "$ROOT_DIR/scripts/publish-mcp-github-package.sh"
bash -n "$0"

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
id=RodriOliveira.AdrGuard.Mcp
version=1.0.0
commit=19f2af215287ecb90cec510d74d0a43c73dec700
mkdir -p "$tmp/pack" "$tmp/work/artifacts/mcp"
package="$tmp/work/artifacts/mcp/$id.$version.nupkg"
create_package() {
  local sha="$1"
  cat > "$tmp/pack/$id.nuspec" <<EOF
<?xml version="1.0"?>
<package>
  <metadata>
    <id>$id</id>
    <version>$version</version>
    <repository type="git" url="https://github.com/rodri-oliveira-dev/adr-guard" commit="$sha" />
  </metadata>
</package>
EOF
  rm -f "$package"
  (cd "$tmp/pack" && zip -q "$package" "$id.nuspec")
  (cd "$tmp/work" && sha256sum "artifacts/mcp/$id.$version.nupkg" > artifacts/mcp/SHA256SUMS)
}
verify_case() {
  local expected="$1" sha="$2"
  if (cd "$tmp/work" && MCP_PACKAGE_ID="$id" VERSION="$version" VALIDATED_SHA="$sha" bash "$VERIFY" > "$tmp/verify.log" 2>&1); then
    [[ "$expected" == success ]] || { cat "$tmp/verify.log" >&2; exit 1; }
  else
    [[ "$expected" == failure ]] || { cat "$tmp/verify.log" >&2; exit 1; }
  fi
}

create_package "$commit"
verify_case success "$commit"
verify_case failure aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa

printf 'tamper' >> "$package"
verify_case failure "$commit"

create_package aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
verify_case failure "$commit"

create_package "$commit"
sed -i 's/^[0-9a-f]\{64\}/0000000000000000000000000000000000000000000000000000000000000000/' "$tmp/work/artifacts/mcp/SHA256SUMS"
verify_case failure "$commit"

bash "$ROOT_DIR/scripts/mcp-github-packages-publish-test.sh"

echo 'MCP GitHub Packages workflow and package provenance tests passed.'
