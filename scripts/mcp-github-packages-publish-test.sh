#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
mkdir -p "$work/bin" "$work/pack" "$work/project/artifacts/mcp"
export MOCK_CALL_LOG="$work/requests.log"
export MOCK_PUSH_LOG="$work/push.log"
export MOCK_PACKAGE="$work/project/artifacts/mcp/RodriOliveira.AdrGuard.Mcp.1.0.0.nupkg"
export MCP_PACKAGE_ID=RodriOliveira.AdrGuard.Mcp
export VERSION=1.0.0
export VALIDATED_SHA=19f2af215287ecb90cec510d74d0a43c73dec700
export NUGET_API_KEY='placeholder-test-token'
export GITHUB_ACTOR='test-bot'
export GITHUB_REPOSITORY_OWNER='rodri-oliveira-dev'
export GITHUB_PACKAGE_SOURCE="https://nuget.pkg.github.com/$GITHUB_REPOSITORY_OWNER/index.json"
cat > "$work/pack/$MCP_PACKAGE_ID.nuspec" <<EOF
<?xml version="1.0"?>
<package><metadata>
<id>$MCP_PACKAGE_ID</id><version>$VERSION</version>
<repository type="git" url="https://github.com/rodri-oliveira-dev/adr-guard" commit="$VALIDATED_SHA" />
</metadata></package>
EOF
(cd "$work/pack" && zip -q "$MOCK_PACKAGE" "$MCP_PACKAGE_ID.nuspec")
(cd "$work/project" && sha256sum "artifacts/mcp/$MCP_PACKAGE_ID.$VERSION.nupkg" > artifacts/mcp/SHA256SUMS)
printf altered > "$work/different.nupkg"

cat > "$work/bin/curl" <<'MOCK_CURL'
#!/usr/bin/env bash
set -euo pipefail
output='' url='' config=false
expected="$(printf '%s:%s' "$GITHUB_ACTOR" "$NUGET_API_KEY" | base64 | tr -d '\r\n')"
for arg in "$@"; do
  [[ "$arg" != *"$NUGET_API_KEY"* ]] || {
    echo 'Token leaked into curl process args.' >&2; exit 98;
  }
done
while (( $# > 0 )); do
  case "$1" in
    --output) output="$2"; shift 2 ;;
    --config) [[ "$2" == '-' ]] && config=true; shift 2 ;;
    --max-filesize) [[ "$2" == 65536 || "$2" == 25000000 ]] || exit 97; shift 2 ;;
    --connect-timeout|--max-time|--retry|--write-out|--proto)
      shift 2 ;;
    --silent|--show-error|--location|--tlsv1.2) shift ;;
    *) url="$1"; shift ;;
  esac
done
[[ "$config" == true && -n "$output" ]] || exit 96
header="$(cat)"
[[ "$header" == "header = \"Authorization: Basic $expected\"" ]] || {
  echo 'Credential not provided through curl stdin.' >&2; exit 95;
}
printf '%s\n' "$url" >> "$MOCK_CALL_LOG"
case "$url" in
  "$GITHUB_PACKAGE_SOURCE")
    cat > "$output" <<EOF
{"resources":[{"@type":"PackageBaseAddress/3.0.0","@id":"https://nuget.pkg.github.com/$GITHUB_REPOSITORY_OWNER/download/"}]}
EOF
    printf '200'
    ;;
  "https://nuget.pkg.github.com/$GITHUB_REPOSITORY_OWNER/download/rodrioliveira.adrguard.mcp/1.0.0/rodrioliveira.adrguard.mcp.1.0.0.nupkg")
    case "$MOCK_MODE" in
      matching) cp "$MOCK_PACKAGE" "$output"; printf '200' ;;
      different) cp "$MOCK_DIFFERENT" "$output"; printf '200' ;;
      missing) printf '404' ;;
      error) printf '403' ;;
      publish-then-match)
        if [[ -s "$MOCK_PUSH_LOG" ]]; then
          cp "$MOCK_PACKAGE" "$output"; printf '200'
        else
          printf '404'
        fi ;;
      publish-then-different)
        if [[ -s "$MOCK_PUSH_LOG" ]]; then
          cp "$MOCK_DIFFERENT" "$output"; printf '200'
        else
          printf '404'
        fi ;;
      *) exit 93 ;;
    esac
    ;;
  *) echo 'Unexpected request.' >&2; exit 94 ;;
esac
MOCK_CURL

cat > "$work/bin/dotnet" <<'MOCK_DOTNET'
#!/usr/bin/env bash
set -euo pipefail
[[ "$1" == nuget && "$2" == push ]] || exit 1
for arg in "$@"; do
  [[ "$arg" != *"$NUGET_API_KEY"* ]] || {
    echo 'Token leaked into dotnet process args.' >&2; exit 98;
  }
done
[[ "$*" != *"--skip-duplicate"* && "$*" != *"--api-key"* ]] || exit 97
[[ -n "$NUGET_API_KEY" ]] || exit 96
echo push >> "$MOCK_PUSH_LOG"
MOCK_DOTNET
chmod +x "$work/bin/curl" "$work/bin/dotnet"
export PATH="$work/bin:$PATH"
export MOCK_DIFFERENT="$work/different.nupkg"

run_case() {
  local label="$1" mode="$2" expected="$3" pushed="$4"
  export MOCK_MODE="$mode"
  : > "$MOCK_PUSH_LOG"
  : > "$MOCK_CALL_LOG"
  if (cd "$work/project" && bash "$ROOT/scripts/publish-mcp-github-package.sh" > "$work/output" 2>&1); then
    [[ "$expected" == success ]] || {
      echo "Unexpected success in $label" >&2; exit 1;
    }
  else
    [[ "$expected" == failure ]] || {
      echo "Unexpected failure in $label" >&2
      cat "$work/output" >&2
      exit 1
    }
  fi
  if [[ "$pushed" == yes ]]; then
    [[ -s "$MOCK_PUSH_LOG" ]] || { echo "Missing push in $label" >&2; exit 1; }
  else
    [[ ! -s "$MOCK_PUSH_LOG" ]] || { echo "Unexpected push in $label" >&2; exit 1; }
  fi
}

run_case 'same version same bytes is idempotent' matching success no
run_case 'same version different bytes fails closed' different failure no
run_case 'absent version publishes and verifies bytes' publish-then-match success yes
run_case 'published version diverges fails closed' publish-then-different failure yes
run_case 'authorization error blocks publication' error failure no

echo 'MCP GitHub Packages authenticated download, hash comparison, and push tests passed.'
