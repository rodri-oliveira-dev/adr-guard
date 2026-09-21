#!/usr/bin/env bash
# Converts the CLI's existing diagnostics to GitHub file annotations without
# changing validation logic or the caller's exit code.
set -uo pipefail

status="${1:?CLI status is required}"
workspace="${2:?Workspace path is required}"
adr_directory="${3:?ADR directory path is required}"
stderr_log="${4:?CLI stderr log is required}"
command="${5:?CLI command is required}"
annotation_limit=50

escape_data() {
  local value="$1"
  value="${value//%/%25}"
  value="${value//$'\r'/%0D}"
  value="${value//$'\n'/%0A}"
  printf '%s' "$value"
}

escape_property() {
  local value
  value="$(escape_data "$1")"
  value="${value//:/%3A}"
  value="${value//,/%2C}"
  printf '%s' "$value"
}

declare -A counts=()
declare -a annotation_paths=()
declare -a annotation_codes=()
declare -a annotation_messages=()
diagnostic_count=0
expected_count=""
valid_format=true
seen_summary=false

if [[ "$status" == 1 ]]; then
  while IFS= read -r line || [[ -n "$line" ]]; do
    if [[ "$seen_summary" == true ]]; then
      valid_format=false
      break
    fi

    if [[ "$line" =~ ^Validation\ failed\ with\ ([1-9][0-9]*)\ issue\(s\)\.$ ]]; then
      expected_count="${BASH_REMATCH[1]}"
      seen_summary=true
      continue
    fi

    if [[ ! "$line" =~ ^(.+):\ (ADR00[1-9])\ (.+)$ ]]; then
      valid_format=false
      break
    fi

    cli_path="${BASH_REMATCH[1]}"
    code="${BASH_REMATCH[2]}"
    message="${BASH_REMATCH[3]}"

    # The CLI runs inside a container, with its checkout mounted at /workspace.
    # Only real Markdown files under the validated directory can be annotated.
    if [[ "$cli_path" != /workspace/* || "$cli_path" == *$'\r'* ]]; then
      valid_format=false
      break
    fi

    suffix="${cli_path#/workspace/}"
    resolved_file="$(realpath -e -- "$workspace/$suffix" 2>/dev/null)" || {
      valid_format=false
      break
    }
    if [[ ! -f "$resolved_file" || "${resolved_file,,}" != *.md ]]; then
      valid_format=false
      break
    fi

    case "$resolved_file" in
      "$adr_directory"/*) ;;
      *) valid_format=false; break ;;
    esac

    diagnostic_count=$((diagnostic_count + 1))
    counts["$code"]=$(( ${counts["$code"]:-0} + 1 ))

    if (( ${#annotation_paths[@]} < annotation_limit )); then
      annotation_paths+=("${resolved_file#"$workspace"/}")
      annotation_codes+=("$code")
      annotation_messages+=("$message")
    fi
  done < "$stderr_log"
fi

trusted=false
if [[ "$status" == 1 && "$valid_format" == true && "$seen_summary" == true && "$expected_count" == "$diagnostic_count" ]]; then
  trusted=true
  for (( i=0; i<${#annotation_paths[@]}; i++ )); do
    printf '::error file=%s,title=%s::%s\n' \
      "$(escape_property "${annotation_paths[i]}")" \
      "${annotation_codes[i]}" \
      "$(escape_data "${annotation_codes[i]} ${annotation_messages[i]}")"
  done
fi

case "$status" in
  0) outcome="Success" ;;
  1) outcome="ADR validation failed" ;;
  2) outcome="CLI usage error" ;;
  3) outcome="Operational error" ;;
  *) outcome="Container execution failed (exit $status)" ;;
esac

if [[ -n "${GITHUB_STEP_SUMMARY:-}" ]]; then
  {
    printf '### ADR Guard — %s\n\n' "$outcome"
    printf '| Item | Result |\n| --- | --- |\n'
    printf '| Command | \`%s\` |\n' "$command"
    printf '| Exit code | \`%s\` |\n' "$status"
    if [[ "$trusted" == true ]]; then
      printf '| Validated diagnostic count | %s |\n' "$diagnostic_count"
      printf '| File annotations | %s (cap: %s) |\n' "${#annotation_paths[@]}" "$annotation_limit"
      for code in ADR001 ADR002 ADR003 ADR004 ADR005 ADR006 ADR007 ADR008 ADR009; do
        if (( ${counts["$code"]:-0} > 0 )); then
          printf '| %s | %s |\n' "$code" "${counts["$code"]}"
        fi
      done
      if (( diagnostic_count > annotation_limit )); then
        printf '\nOnly the first %s diagnostics were annotated; consult the raw CLI logs for all %s issues.\n' \
          "$annotation_limit" "$diagnostic_count"
      fi
    elif [[ "$status" == 1 ]]; then
      printf '| Parsed diagnostics | Unavailable (unexpected CLI output; see raw logs) |\n'
    else
      printf '| ADR rule annotations | 0 (CLI did not report validation failure) |\n'
    fi
  } >> "$GITHUB_STEP_SUMMARY" || echo "ADR Guard: unable to write step summary." >&2
fi

if [[ "$status" == 1 && "$trusted" != true ]]; then
  echo "ADR Guard: no file annotations emitted because the CLI diagnostics did not match the expected format; inspect the raw logs." >&2
fi
