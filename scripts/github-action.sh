#!/usr/bin/env bash
set -uo pipefail

adr_path="${ADR_GUARD_PATH:-}"
command="${ADR_GUARD_COMMAND:-}"
requested_version="${ADR_GUARD_VERSION:-}"
action_ref="${ADR_GUARD_ACTION_REF:-}"
review_target="${ADR_GUARD_REVIEW_TARGET:-}"
review_provider="${ADR_GUARD_REVIEW_PROVIDER:-}"
review_model="${ADR_GUARD_REVIEW_MODEL:-}"
review_endpoint="${ADR_GUARD_REVIEW_ENDPOINT:-}"
review_context_files="${ADR_GUARD_REVIEW_CONTEXT_FILES:-}"
review_include_existing="${ADR_GUARD_REVIEW_INCLUDE_EXISTING_ADRS:-false}"
review_policy="${ADR_GUARD_REVIEW_POLICY:-advisory}"
review_policy_file="${ADR_GUARD_REVIEW_POLICY_FILE:-}"
event_name="${ADR_GUARD_EVENT_NAME:-${GITHUB_EVENT_NAME:-}}"
repository="${ADR_GUARD_REPOSITORY:-${GITHUB_REPOSITORY:-}}"
pr_head_repository="${ADR_GUARD_PR_HEAD_REPOSITORY:-}"

escape_workflow_data() {
  local value="${1-}"
  value="${value//%/%25}"
  value="${value//$'\r'/%0D}"
  value="${value//$'\n'/%0A}"
  printf '%s' "${value}"
}

emit_error() {
  printf '::error::%s\n' "$(escape_workflow_data "${1-}")" >&2
}

usage_error() {
  emit_error "$1"
  exit 2
}

operational_error() {
  emit_error "$1"
  exit 3
}

validate_relative_input_path() {
  local input_path="$1"
  local label="$2"

  if [[ -z "${input_path}" ]]; then
    usage_error "${label} must not be empty."
  fi

  if [[ "${input_path}" = /* ]]; then
    usage_error "${label} must be relative to GITHUB_WORKSPACE."
  fi

  local segment
  IFS='/' read -r -a input_segments <<< "${input_path}"
  for segment in "${input_segments[@]}"; do
    if [[ "${segment}" == ".." ]]; then
      usage_error "${label} must not contain '..' traversal segments."
    fi
  done
}

resolve_inside_workspace() {
  local input_path="$1"
  local label="$2"

  validate_relative_input_path "${input_path}" "${label}"

  RESOLVED_INPUT_PATH="$(realpath -e -- "${workspace}/${input_path}" 2>/dev/null)" ||
    usage_error "${label} '${input_path}' does not exist inside GITHUB_WORKSPACE."

  case "${RESOLVED_INPUT_PATH}" in
    "${workspace}"|"${workspace}/"*)
      ;;
    *)
      usage_error "${label} '${input_path}' resolves outside GITHUB_WORKSPACE."
      ;;
  esac
}

if [[ "${RUNNER_OS:-Linux}" != "Linux" ]]; then
  operational_error "ADR Guard GitHub Action supports Linux runners with Docker only."
fi

if [[ -z "${GITHUB_WORKSPACE:-}" || ! -d "${GITHUB_WORKSPACE}" ]]; then
  operational_error "GITHUB_WORKSPACE must point to the checked-out repository. Run actions/checkout before ADR Guard."
fi

if [[ -z "${command}" ]]; then
  usage_error "The 'command' input must not be empty. Allowed values: check, index, review."
fi

case "${command}" in
  check|index|review)
    ;;
  *)
    usage_error "Unsupported command '${command}'. Allowed values: check, index, review."
    ;;
esac

workspace="$(realpath -e "${GITHUB_WORKSPACE}")" ||
  operational_error "Unable to resolve GITHUB_WORKSPACE."

resolved_path=""
resolved_review_target=""
resolved_review_policy_file=""
declare -a resolved_review_context_files=()

if [[ "${command}" == "review" ]]; then
  case "${event_name}" in
    pull_request_target)
      usage_error "AI review is disabled for pull_request_target because privileged execution must not process untrusted PR content."
      ;;
    pull_request)
      if [[ -z "${repository}" || -z "${pr_head_repository}" || "${repository}" != "${pr_head_repository}" ]]; then
        usage_error "AI review is disabled for fork or untrusted pull_request events. Run deterministic check on the PR and perform provider-backed review only in a trusted workflow."
      fi
      ;;
    push|workflow_dispatch|schedule)
      ;;
    *)
      usage_error "AI review is not supported for event '${event_name:-<empty>}'. Allowed events: push, workflow_dispatch, schedule, same-repository pull_request."
      ;;
  esac

  if [[ -z "${review_target}" ]]; then
    usage_error "The 'review-target' input is required when command is 'review'."
  fi

  if [[ -z "${review_provider}" || -z "${review_model}" ]]; then
    usage_error "The 'provider' and 'model' inputs are required when command is 'review'."
  fi

  case "${review_provider}" in
    openai|anthropic|gemini|openai-compatible)
      ;;
    *)
      usage_error "Unsupported review provider '${review_provider}'. Allowed values: openai, anthropic, gemini, openai-compatible."
      ;;
  esac

  case "${review_include_existing}" in
    true|false)
      ;;
    *)
      usage_error "The 'include-existing-adrs' input must be 'true' or 'false'."
      ;;
  esac

  case "${review_policy}" in
    advisory|enforce)
      ;;
    *)
      usage_error "The 'policy' input must be 'advisory' or 'enforce'."
      ;;
  esac

  if [[ "${review_policy}" == "enforce" && -z "${review_policy_file}" ]]; then
    usage_error "The 'policy-file' input is required when policy is 'enforce'."
  fi

  resolve_inside_workspace "${review_target}" "The 'review-target' input"
  resolved_review_target="${RESOLVED_INPUT_PATH}"

  if [[ ! -f "${resolved_review_target}" || "${resolved_review_target,,}" != *.md ]]; then
    usage_error "The 'review-target' input must resolve to a Markdown file."
  fi

  if [[ -n "${review_policy_file}" ]]; then
    resolve_inside_workspace "${review_policy_file}" "The 'policy-file' input"
    resolved_review_policy_file="${RESOLVED_INPUT_PATH}"

    if [[ ! -f "${resolved_review_policy_file}" ]]; then
      usage_error "The 'policy-file' input must resolve to a regular file."
    fi
  fi

  if [[ -n "${review_context_files}" ]]; then
    while IFS= read -r context_path || [[ -n "${context_path}" ]]; do
      context_path="${context_path%$'\r'}"
      [[ -z "${context_path}" ]] && continue

      resolve_inside_workspace "${context_path}" "Each 'context-files' entry"

      if [[ ! -f "${RESOLVED_INPUT_PATH}" ]]; then
        usage_error "Context file '${context_path}' must resolve to a regular file."
      fi

      case "${RESOLVED_INPUT_PATH,,}" in
        *.md|*.txt)
          ;;
        *)
          usage_error "Context file '${context_path}' must use a .md or .txt extension."
          ;;
      esac

      resolved_review_context_files+=("${RESOLVED_INPUT_PATH}")
    done <<< "${review_context_files}"
  fi

  if ! command -v python3 >/dev/null 2>&1; then
    operational_error "Python 3 is required for safe AI review summary and annotation rendering."
  fi
else
  if [[ -z "${adr_path}" ]]; then
    usage_error "The 'path' input must not be empty."
  fi

  resolve_inside_workspace "${adr_path}" "The 'path' input"
  resolved_path="${RESOLVED_INPUT_PATH}"

  if [[ ! -d "${resolved_path}" ]]; then
    usage_error "ADR path '${adr_path}' must resolve to a directory."
  fi
fi

if ! command -v docker >/dev/null 2>&1; then
  operational_error "Docker is required. Use a Linux runner with Docker available, such as ubuntu-latest."
fi

if ! docker info >/dev/null 2>&1; then
  operational_error "Docker is installed but the daemon is not available."
fi

version=""
if [[ -n "${requested_version}" ]]; then
  if [[ "${requested_version}" =~ ^v?([0-9]+\.[0-9]+\.[0-9]+)$ ]]; then
    version="${BASH_REMATCH[1]}"
  else
    usage_error "Invalid 'version' input '${requested_version}'. Use an exact version such as 1.2.3 or v1.2.3."
  fi
elif [[ "${action_ref}" =~ ^v([0-9]+\.[0-9]+\.[0-9]+)$ ]]; then
  version="${BASH_REMATCH[1]}"
elif [[ "${action_ref}" =~ ^v([0-9]+)$ ]]; then
  version="${BASH_REMATCH[1]}"
else
  usage_error "Unable to select an image version from action ref '${action_ref:-<empty>}'. Reference ADR Guard with @vX.Y.Z or @vX, or set the exact 'version' input when pinning by commit SHA."
fi

image="ghcr.io/rodri-oliveira-dev/adr-guard:${version}"

echo "Pulling ADR Guard runtime ${image}."
if ! docker pull "${image}"; then
  operational_error "Unable to pull ADR Guard image '${image}'. The requested version is not replaced with 'latest'."
fi

echo "Running ADR Guard ${version}: ${command} using ${image}."

docker_args=(
  run
  --rm
  --pull=never
  --read-only
  --cap-drop=ALL
  --security-opt=no-new-privileges
  --workdir /workspace
  --mount "type=bind,src=${workspace},dst=/workspace,readonly"
)

declare -a cli_args=()

if [[ "${command}" == "index" ]]; then
  container_path="/workspace${resolved_path#"${workspace}"}"
  host_uid="$(id -u)"
  host_gid="$(id -g)"

  if [[ "${host_uid}" == "0" ]]; then
    operational_error "The 'index' command refuses to run the container as root. Use a non-root Linux runner."
  fi

  docker_args+=(
    --network=none
    --user "${host_uid}:${host_gid}"
    --mount "type=bind,src=${resolved_path},dst=${container_path}"
  )
  cli_args=(
    index
    "${container_path}"
  )
elif [[ "${command}" == "check" ]]; then
  container_path="/workspace${resolved_path#"${workspace}"}"
  docker_args+=(--network=none)
  cli_args=(
    check
    "${container_path}"
  )
else
  container_target="/workspace${resolved_review_target#"${workspace}"}"

  credential_name=""
  case "${review_provider}" in
    openai) credential_name="OPENAI_API_KEY" ;;
    anthropic) credential_name="ANTHROPIC_API_KEY" ;;
    gemini) credential_name="GEMINI_API_KEY" ;;
    openai-compatible) credential_name="ADR_GUARD_OPENAI_COMPATIBLE_API_KEY" ;;
  esac

  if [[ -n "${credential_name}" && -n "${!credential_name:-}" ]]; then
    docker_args+=(--env "${credential_name}")
  fi

  cli_args=(
    review
    "${container_target}"
    --provider "${review_provider}"
    --model "${review_model}"
    --policy "${review_policy}"
    --format json
  )

  if [[ -n "${review_endpoint}" ]]; then
    cli_args+=(--endpoint "${review_endpoint}")
  fi

  for context_file in "${resolved_review_context_files[@]}"; do
    cli_args+=(
      --context-file
      "/workspace${context_file#"${workspace}"}"
    )
  done

  if [[ "${review_include_existing}" == "true" ]]; then
    cli_args+=(--include-existing-adrs)
  fi

  if [[ -n "${resolved_review_policy_file}" ]]; then
    cli_args+=(
      --policy-file
      "/workspace${resolved_review_policy_file#"${workspace}"}"
    )
  fi
fi

docker_args+=(
  "${image}"
  "${cli_args[@]}"
)

# Capture both streams before replaying them so hostile ADR/provider diagnostics
# cannot issue arbitrary GitHub workflow commands through the raw CLI log.
log_directory="$(mktemp -d)" ||
  operational_error "Unable to create temporary log directory."
trap 'rm -rf -- "${log_directory}"' EXIT
stdout_log="${log_directory}/stdout"
stderr_log="${log_directory}/stderr"
review_source_manifest="${log_directory}/review-sources.tsv"

if [[ "${command}" == "review" ]]; then
  printf 'target\t%s\n' "${resolved_review_target}" >"${review_source_manifest}"
  for (( index=0; index<${#resolved_review_context_files[@]}; index++ )); do
    printf 'context-%d\t%s\n' "$((index + 1))" "${resolved_review_context_files[index]}" >>"${review_source_manifest}"
  done
fi

docker "${docker_args[@]}" >"${stdout_log}" 2>"${stderr_log}"
status=$?

# The stop token is unpredictable and printed only outside the untrusted CLI
# output. Replay the original output for troubleshooting without interpreting
# any embedded workflow commands.
stop_token="$(od -An -N16 -tx1 /dev/urandom | tr -d '[:space:]')"
if [[ ! "${stop_token}" =~ ^[0-9a-f]{32}$ ]]; then
  operational_error "Unable to generate a safe workflow-command suspension token."
fi
printf '::stop-commands::%s\n' "${stop_token}"
cat -- "${stdout_log}" "${stderr_log}"
printf '::%s::\n' "${stop_token}"

# Docker reserves 125-127 for engine/invocation failures. Normalize those to
# ADR Guard's operational-error contract while preserving CLI exit codes 0-4.
if (( status >= 125 )); then
  emit_error "ADR Guard image '${image}' could not be executed (Docker exit code ${status}). The requested version is not replaced with 'latest'."
  status=3
fi

# Reporting is best-effort: never replace the original ADR Guard exit code.
if [[ "${command}" == "review" ]]; then
  if ! python3 "$(dirname "${BASH_SOURCE[0]}")/github-action-review-report.py"     "${status}" "${workspace}" "${resolved_review_target}" "${stdout_log}"     "${stderr_log}" "${review_source_manifest}" "${review_provider}" "${review_model}"; then
    echo "::warning::ADR Guard AI review reporting failed; inspect the raw CLI log." >&2
  fi
else
  if ! bash "$(dirname "${BASH_SOURCE[0]}")/github-action-report.sh"     "${status}" "${workspace}" "${resolved_path}" "${stderr_log}" "${command}"; then
    echo "::warning::ADR Guard annotation reporting failed; inspect the raw CLI log." >&2
  fi
fi

exit "${status}"
