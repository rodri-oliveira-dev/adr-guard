#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CLI_PROJECT="$ROOT_DIR/src/AdrGuard/AdrGuard.csproj"
MCP_PROJECT="$ROOT_DIR/src/AdrGuard.Mcp/AdrGuard.Mcp.csproj"
EXTENSION_MANIFEST="$ROOT_DIR/extensions/vscode/package.json"
EXTENSION_CI="$ROOT_DIR/.github/workflows/vscode-extension.yml"
EXTENSION_PACKAGE="$ROOT_DIR/.github/workflows/vscode-extension-release.yml"
WORKFLOW_DIR="$ROOT_DIR/.github/workflows"
CI="$ROOT_DIR/.github/workflows/ci.yml"

xml_value() {
  local file="$1" element="$2"
  sed -n "s:.*<${element}>\([^<]*\)</${element}>.*:\1:p" "$file" | head -n 1
}

json_string() {
  local file="$1" property="$2"
  sed -n "s/^[[:space:]]*\"${property}\":[[:space:]]*\"\([^\"]*\)\".*/\1/p" "$file" | head -n 1
}

assert_semver() {
  local component="$1" version="$2"
  [[ "$version" =~ ^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$ ]] || {
    echo "$component version must use stable SemVer; found '${version:-<empty>}'." >&2
    exit 1
  }
}

assert_vsix_upload_retention() {
  local workflow="$1"
  local require_upload="${2:-false}"
  awk -v workflow="$workflow" -v require_upload="$require_upload" '
    function is_upload_action(value) {
      sub(/^[[:space:]]*(-[[:space:]]+)?uses:[[:space:]]*/, "", value)
      sub(/[[:space:]]*#.*$/, "", value)
      sub(/[[:space:]]+$/, "", value)
      gsub(/^["\047]|["\047]$/, "", value)
      return value ~ /^actions\/upload-artifact@/
    }
    function evaluate_step() {
      if (!upload || !vsix) return
      uploads++
      if (!retention) {
        print "VSIX upload step must retain its artifact for 21 days: " workflow > "/dev/stderr"
        invalid = 1
      }
    }
    /^[[:space:]]*-[[:space:]]+[[:alnum:]_-]+:/ {
      evaluate_step()
      upload = 0
      vsix = 0
      retention = 0
      path = 0
      if ($0 ~ /^[[:space:]]*-[[:space:]]+uses:/ && is_upload_action($0)) {
        upload = 1
      }
      next
    }
    /^[[:space:]]*uses:/ {
      if (is_upload_action($0)) {
        upload = 1
      }
    }
    upload && /path:[[:space:]]*/ {
      value = $0
      sub(/^.*path:[[:space:]]*/, "", value)
      sub(/[[:space:]]*[,}].*$/, "", value)
      sub(/[[:space:]]*#.*$/, "", value)
      sub(/[[:space:]]+$/, "", value)
      gsub(/^["\047]|["\047]$/, "", value)
      path = 1
      if (tolower(value) ~ /\.vsix/) {
        vsix = 1
      }
    }
    upload && path && tolower($0) ~ /\.vsix/ {
      vsix = 1
    }
    upload && /retention-days:[[:space:]]*/ {
      value = $0
      sub(/^.*retention-days:[[:space:]]*/, "", value)
      sub(/[[:space:]]*[,}].*$/, "", value)
      sub(/[[:space:]]*#.*$/, "", value)
      sub(/[[:space:]]+$/, "", value)
      gsub(/^["\047]|["\047]$/, "", value)
      if (value == "21") {
        retention = 1
      }
    }
    END {
      evaluate_step()
      if (require_upload == "true" && uploads == 0) {
        print "No VSIX upload-artifact step found: " workflow > "/dev/stderr"
        invalid = 1
      }
      exit invalid
    }
  ' "$workflow"
}

assert_no_marketplace_publication() {
  local workflow="$1"
  local normalized
  normalized="$(awk '
    {
      sub(/\r$/, "")

      if (folded) {
        indentation = match($0, /[^ ]/) - 1
        if ($0 ~ /^[[:space:]]*$/ || indentation > folded_indentation) {
          sub(/^[[:space:]]*/, "")
          buffer = buffer $0 " "
          next
        }
        print buffer
        buffer = ""
        folded = 0
      }

      if ($0 ~ /^[[:space:]]*(-[[:space:]]+)?run:[[:space:]]*>[[:digit:]+-]*[[:space:]]*(#.*)?$/) {
        folded_indentation = match($0, /[^ ]/) - 1
        folded = 1
        next
      }

      buffer = buffer $0
      if (sub(/\\[[:space:]]*$/, " ", buffer)) {
        next
      }
      print buffer
      buffer = ""
    }
    END {
      if (buffer != "") print buffer
    }
  ' "$workflow")"
  if grep -Eiq '(^|[[:space:]])vsce[[:space:]]+publish|@vscode/vsce([^[:space:]]*)?([[:space:]]+[^[:space:]]+)*[[:space:]]+publish|uses:[[:space:]]*[^[:space:]]*publish-vscode-extension|VSCE_PAT|AZURE_ACCESS_TOKEN|environment:[[:space:]]*vscode-marketplace' <<< "$normalized"; then
    echo "VS Code workflow must not publish to the Marketplace: $workflow" >&2
    return 1
  fi
}

cli_id="$(xml_value "$CLI_PROJECT" PackageId)"
cli_version="$(xml_value "$CLI_PROJECT" VersionPrefix)"
mcp_id="$(xml_value "$MCP_PROJECT" PackageId)"
mcp_version="$(xml_value "$MCP_PROJECT" VersionPrefix)"
extension_name="$(json_string "$EXTENSION_MANIFEST" name)"
extension_publisher="$(json_string "$EXTENSION_MANIFEST" publisher)"
extension_version="$(json_string "$EXTENSION_MANIFEST" version)"

[[ "$cli_id" == RodriOliveira.AdrGuard ]]
[[ "$mcp_id" == RodriOliveira.AdrGuard.Mcp ]]
[[ "$cli_id" != "$mcp_id" ]]
[[ "$extension_publisher.$extension_name" == rodrioliveira.adr-guard ]]
assert_semver CLI "$cli_version"
assert_semver MCP "$mcp_version"
assert_semver 'VS Code extension' "$extension_version"

grep -Fq '<ProjectReference Include="../AdrGuard/AdrGuard.csproj" />' "$MCP_PROJECT"
grep -Fq '<PackAsTool>true</PackAsTool>' "$CLI_PROJECT"
grep -Fq '<PackAsTool>true</PackAsTool>' "$MCP_PROJECT"

for workflow in "$EXTENSION_CI" "$EXTENSION_PACKAGE"; do
  assert_vsix_upload_retention "$workflow" true
done

while IFS= read -r -d '' workflow; do
  assert_vsix_upload_retention "$workflow"
  assert_no_marketplace_publication "$workflow"
done < <(find "$WORKFLOW_DIR" -maxdepth 1 -type f \( -name '*.yml' -o -name '*.yaml' \) -print0)

grep -Fq 'workflow_dispatch:' "$EXTENSION_PACKAGE"
grep -Fq "if: github.ref == 'refs/heads/main'" "$EXTENSION_PACKAGE"
grep -Fq 'Marketplace publishing: disabled' "$EXTENSION_PACKAGE"
grep -Fq 'bash scripts/component-distribution-policy-test.sh' "$CI"

for document in "$ROOT_DIR/docs/compatibility.md" "$ROOT_DIR/docs/compatibility.pt-BR.md"; do
  grep -Fq "\`$cli_version\`" "$document"
  grep -Fq "\`$mcp_version\`" "$document"
  grep -Fq "\`$extension_version\`" "$document"
  grep -Fq '21' "$document"
done

bash -n "$0"
echo "Component distribution policy passed: CLI $cli_version, MCP $mcp_version, VS Code $extension_version; VSIX artifacts retained for 21 days without Marketplace publication."
