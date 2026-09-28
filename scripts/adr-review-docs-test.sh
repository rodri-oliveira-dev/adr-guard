#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TOOL="${1:-}"

EN="${ROOT_DIR}/docs/adr-review.md"
PT="${ROOT_DIR}/docs/adr-review.pt-BR.md"
README_EN="${ROOT_DIR}/README.md"
README_PT="${ROOT_DIR}/README.pt-BR.md"
RELEASE_EN="${ROOT_DIR}/docs/releases/ai-review.md"
RELEASE_PT="${ROOT_DIR}/docs/releases/ai-review.pt-BR.md"
ACTION_REVIEW_EN="${ROOT_DIR}/docs/github-action-review.md"
ACTION_REVIEW_PT="${ROOT_DIR}/docs/github-action-review.pt-BR.md"
POLICY_EN="${ROOT_DIR}/docs/adr-review-policy-v1.md"
POLICY_PT="${ROOT_DIR}/docs/adr-review-policy-v1.pt-BR.md"
SECURITY_EN="${ROOT_DIR}/docs/adr-review-security.md"
SECURITY_PT="${ROOT_DIR}/docs/adr-review-security.pt-BR.md"
ACTION_TEST="${ROOT_DIR}/scripts/github-action-review-test.sh"
REGRESSION_TEST="${ROOT_DIR}/tests/AdrGuard.Tests/Review/AdrReviewRegressionFixtureTests.cs"
CI="${ROOT_DIR}/.github/workflows/ci.yml"

for file in   "${EN}" "${PT}" "${README_EN}" "${README_PT}"   "${RELEASE_EN}" "${RELEASE_PT}"   "${ACTION_REVIEW_EN}" "${ACTION_REVIEW_PT}"   "${POLICY_EN}" "${POLICY_PT}" "${SECURITY_EN}" "${SECURITY_PT}"   "${ACTION_TEST}" "${REGRESSION_TEST}" "${CI}"; do
  test -s "${file}" || {
    echo "Required ADR review documentation/test artifact is missing: ${file}" >&2
    exit 1
  }
done

# Both language guides must describe the same stable machine-facing contract.
for token in   'adr-guard review'   '--provider'   '--model'   '--endpoint'   '--context-file'   '--include-existing-adrs'   '--policy advisory|enforce'   '--policy-file'   '--format text|json'   '--output <path>'   '--overwrite'   'OPENAI_API_KEY'   'ANTHROPIC_API_KEY'   'GEMINI_API_KEY'   'ADR_GUARD_OPENAI_COMPATIBLE_API_KEY'   'clarity-and-rationale'   'considered-alternatives'   'nonfunctional-requirements'   'risks-and-consequences'   'architectural-consistency'   'security-and-compliance'   'implementation-and-operational-feasibility'   'measurable-verification-criteria'   'observed-evidence'   'potential-risk'   'missing-context'   'recommendation-for-human-investigation'   'not-applicable'   'required-section-content'   'required-context-file'   'schema `1.0`'   'pull_request_target'   'contents: read'   'GITHUB_STEP_SUMMARY'   'v1.1.2'   'v1.1.3'   'v1.1.4'   'v1.1.6'; do
  grep -Fq -- "${token}" "${EN}" || {
    echo "English ADR review guide is missing contract token: ${token}" >&2
    exit 1
  }
  grep -Fq -- "${token}" "${PT}" || {
    echo "pt-BR ADR review guide is missing contract token: ${token}" >&2
    exit 1
  }
done

# Numeric limits use locale-appropriate thousands separators.
for token in '50,000' '150,000' '100,000' '300,000' '12,000' '120,000'; do
  grep -Fq -- "${token}" "${EN}" || {
    echo "English ADR review guide is missing limit: ${token}" >&2
    exit 1
  }
done
for token in '50.000' '150.000' '100.000' '300.000' '12.000' '120.000'; do
  grep -Fq -- "${token}" "${PT}" || {
    echo "pt-BR ADR review guide is missing limit: ${token}" >&2
    exit 1
  }
done

# Markdown table option values must escape literal pipe separators.
for guide in "${EN}" "${PT}"; do
  grep -Fq -- '`--policy advisory\|enforce`' "${guide}"
  grep -Fq -- '`--format text\|json`' "${guide}"
done

# Action review runtime and event boundaries must stay synchronized in both languages.
for guide in "${ACTION_REVIEW_EN}" "${ACTION_REVIEW_PT}"; do
  grep -Fq 'Python 3' "${guide}"
  grep -Fq 'workflow_dispatch' "${guide}"
  grep -Fq 'schedule' "${guide}"
  grep -Fq 'pull_request_target' "${guide}"
done

# Human ownership, third-party processing and opt-out language must be explicit.
grep -Fiq 'not proof' "${EN}"
grep -Fiq 'não prova' "${PT}"
grep -Fiq 'third-party processing' "${EN}"
grep -Fiq 'processamento por terceiros' "${PT}"
grep -Fiq 'do not invoke `review`' "${EN}"
grep -Fiq 'não execute `review`' "${PT}"
grep -Fiq 'formal security/compliance certification' "${EN}"
grep -Fiq 'certificação formal de segurança/compliance' "${PT}"

# Both guides include a real human-readable report sample and troubleshooting.
for guide in "${EN}" "${PT}"; do
  grep -Fq '# ADR Technical Review' "${guide}"
  grep -Fq 'Outcome: needs-context' "${guide}"
  grep -Fq 'not-enough-information' "${guide}"
  grep -Fiq 'timeout' "${guide}"
  grep -Fiq 'rate limit' "${guide}"
  grep -Fiq 'malformed' "${guide}" || grep -Fiq 'malformada' "${guide}"
  grep -Fiq 'contradiction' "${guide}" || grep -Fiq 'contradição' "${guide}"
done

# READMEs expose the public review entry point and link to the detailed guides.
grep -Fq '[AI review guide](docs/adr-review.md)' "${README_EN}"
grep -Fq '[guia de review por IA](docs/adr-review.pt-BR.md)' "${README_PT}"
for readme in "${README_EN}" "${README_PT}"; do
  grep -Fq 'adr-guard review' "${readme}"
  grep -Fq 'v1.1.2' "${readme}"
  grep -Fq 'v1.1.6' "${readme}"
  grep -Fq 'command: review' "${readme}"
  grep -Fq '`4`' "${readme}"
done

# Release availability is explicit and must not claim pre-release Action support.
for release_note in "${RELEASE_EN}" "${RELEASE_PT}"; do
  for version in v1.1.2 v1.1.3 v1.1.4 v1.1.6; do
    grep -Fq "${version}" "${release_note}"
  done
done
grep -Fq 'Published in the moving `@v1` Action' "${RELEASE_EN}"
grep -Fq 'Publicado na Action móvel `@v1`' "${RELEASE_PT}"

# Policy and security guidance must exist in both languages and cross-link correctly.
grep -Fq '[Português (Brasil)](adr-review-policy-v1.pt-BR.md)' "${POLICY_EN}"
grep -Fq '[English](adr-review-policy-v1.md)' "${POLICY_PT}"
grep -Fq '[Português (Brasil)](adr-review-security.pt-BR.md)' "${SECURITY_EN}"
grep -Fq '[English](adr-review-security.md)' "${SECURITY_PT}"
for doc in "${POLICY_EN}" "${POLICY_PT}"; do
  grep -Fq 'required-section-content' "${doc}"
  grep -Fq 'required-context-file' "${doc}"
  grep -Fq 'exit' "${doc}"
done
for doc in "${SECURITY_EN}" "${SECURITY_PT}"; do
  grep -Fq 'pull_request_target' "${doc}"
  grep -Fq 'GITHUB_TOKEN' "${doc}"
  grep -Fq 'contents: read' "${doc}"
done

# Every documented execution surface is tied to deterministic test coverage.
grep -Fq 'Verify opt-in AI review paths with mock provider' "${CI}"
grep -Fq 'github-action-review-test.sh' "${CI}"
grep -Fq 'CompleteFixtureProducesVersionedEightDimensionReportWithoutMutation' "${REGRESSION_TEST}"
grep -Fq 'AdvisoryPolicyViolationRunsMockProviderAndRemainsSuccessful' "${REGRESSION_TEST}"
grep -Fq 'DeterministicEnforcementIsIndependentOfMockProviderLanguage' "${REGRESSION_TEST}"

# When the packaged tool is available, smoke the actual public help contract.
if [[ -n "${TOOL}" ]]; then
  test -x "${TOOL}" || {
    echo "ADR Guard tool is not executable: ${TOOL}" >&2
    exit 1
  }

  HELP="$(mktemp)"
  trap 'rm -f -- "${HELP}"' EXIT
  "${TOOL}" review --help >"${HELP}"

  for option in     '--provider <provider>'     '--model <model>'     '--endpoint <uri>'     '--context-file <path>'     '--include-existing-adrs'     '--policy advisory|enforce'     '--policy-file <path>'     '--format text|json'     '--output <path>'     '--overwrite'; do
    grep -Fq -- "${option}" "${HELP}" || {
      echo "Packaged review help is missing documented option: ${option}" >&2
      exit 1
    }
  done

  grep -Fq '0  Review completed' "${HELP}"
  grep -Fq '4  Named deterministic enforcement rule violated' "${HELP}"
fi

echo "Bilingual ADR review documentation contract checks passed."
