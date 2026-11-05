#!/usr/bin/env python3
"""Render a bounded summary from ADR Guard drift JSON without reevaluating policy."""

from __future__ import annotations

import json
import os
import re
import sys
from pathlib import Path
from typing import Any

MAX_REPORT_BYTES = 4 * 1024 * 1024
MAX_JSON_DEPTH = 128
MAX_RULES = 1_000
MAX_FINDINGS = 2_000
MAX_EVIDENCE = 10_000
SUMMARY_FINDING_LIMIT = 50
FINGERPRINT = re.compile(r"^[a-f0-9]{64}$")


def fail(message: str) -> None:
    raise ValueError(message)


def exact_keys(value: dict[str, Any], required: set[str], optional: set[str], label: str) -> None:
    keys = set(value)
    if not required <= keys or keys - required - optional:
        fail(f"{label} has an unsupported property set")


def bounded_text(value: Any, label: str, maximum: int = 2_048) -> str:
    if (
        not isinstance(value, str)
        or not value
        or len(value) > maximum
        or any(ord(character) < 32 or ord(character) == 127 for character in value)
    ):
        fail(f"{label} is invalid or exceeds its limit")
    return value


def markdown(value: str) -> str:
    return (
        value.replace("&", "&amp;")
        .replace("\\", "\\\\")
        .replace("|", "\\|")
        .replace("<", "&lt;")
        .replace(">", "&gt;")
    )


def code_span(value: str) -> str:
    longest_run = max((len(run) for run in re.findall(r"`+", value)), default=0)
    fence = "`" * (longest_run + 1)
    padding = " " if value.startswith("`") or value.endswith("`") else ""
    return f"{fence}{padding}{markdown(value)}{padding}{fence}"


def ensure_json_depth(value: str) -> None:
    depth = 0
    in_string = False
    escaped = False
    for character in value:
        if in_string:
            if escaped:
                escaped = False
            elif character == "\\":
                escaped = True
            elif character == '"':
                in_string = False
        elif character == '"':
            in_string = True
        elif character in "[{":
            depth += 1
            if depth > MAX_JSON_DEPTH:
                fail("drift JSON nesting exceeds the parser depth limit")
        elif character in "]}":
            depth -= 1


def unique_object(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            fail(f"duplicate JSON property: {key}")
        result[key] = value
    return result


def require_count(container: dict[str, Any], key: str, maximum: int) -> int:
    value = container.get(key)
    if not isinstance(value, int) or isinstance(value, bool) or not 0 <= value <= maximum:
        fail(f"{key} is not a bounded count")
    return value


def validate_evidence(value: Any) -> None:
    if not isinstance(value, dict):
        fail("drift evidence is invalid")
    exact_keys(value, {"kind", "source", "detail"}, {"subject", "target", "line"}, "drift evidence")
    bounded_text(value.get("kind"), "evidence kind", 128)
    bounded_text(value.get("source"), "evidence source", 1_024)
    bounded_text(value.get("detail"), "evidence detail", 4_096)
    for key in ("subject", "target"):
        if value.get(key) is not None:
            bounded_text(value.get(key), f"evidence {key}", 1_024)
    line = value.get("line")
    if line is not None and (not isinstance(line, int) or isinstance(line, bool) or line < 1):
        fail("evidence line is invalid")


def validate_report(report: Any) -> tuple[str, dict[str, int], list[tuple[str, str, str, str, int]], bool]:
    if not isinstance(report, dict):
        fail("drift report is not an object")
    exact_keys(report, {"schemaVersion", "policy", "repository", "coverage", "findings"}, set(), "drift report")
    if report.get("schemaVersion") != "1.0" or report.get("policy") not in ("advisory", "enforce"):
        fail("drift report is not the schemaVersion 1.0 contract")

    repository = report.get("repository")
    coverage = report.get("coverage")
    findings = report.get("findings")
    if not isinstance(repository, dict) or not isinstance(coverage, dict):
        fail("drift repository or coverage is invalid")
    exact_keys(repository, {"rulesManifest"}, {"commit"}, "drift repository")
    bounded_text(repository.get("rulesManifest"), "rules manifest", 1_024)
    commit = repository.get("commit")
    if commit is not None:
        bounded_text(commit, "repository commit", 128)

    coverage_keys = {"totalRules", "pass", "violations", "unknown", "notApplicable"}
    exact_keys(coverage, coverage_keys, set(), "drift coverage")
    counts = {key: require_count(coverage, key, MAX_RULES) for key in coverage_keys}
    if counts["totalRules"] != sum(counts[key] for key in ("pass", "violations", "unknown", "notApplicable")):
        fail("drift coverage counts are inconsistent")

    if not isinstance(findings, list) or len(findings) > MAX_FINDINGS:
        fail("drift report findings exceed the bound")

    rows: list[tuple[str, str, str, str, int]] = []
    evidence_total = 0
    unresolved_baseline = False
    finding_keys = {
        "fingerprint",
        "ruleId",
        "adrStableId",
        "outcome",
        "checkType",
        "confidence",
        "baselineState",
        "explanation",
        "evidence",
    }
    for finding in findings:
        if not isinstance(finding, dict):
            fail("drift finding is invalid")
        exact_keys(finding, finding_keys, set(), "drift finding")
        fingerprint = bounded_text(finding.get("fingerprint"), "finding fingerprint", 64)
        if FINGERPRINT.fullmatch(fingerprint) is None:
            fail("finding fingerprint is invalid")
        rule_id = bounded_text(finding.get("ruleId"), "finding ruleId", 128)
        adr_id = bounded_text(finding.get("adrStableId"), "finding adrStableId", 128)
        outcome = finding.get("outcome")
        check_type = finding.get("checkType")
        baseline_state = finding.get("baselineState")
        if outcome not in ("pass", "violation", "unknown", "not-applicable"):
            fail("finding outcome is invalid")
        if check_type not in ("forbid-project-reference", "external-test"):
            fail("finding checkType is invalid")
        if baseline_state not in ("current", "new", "existing", "resolved", "unresolved-baseline"):
            fail("finding baselineState is invalid")
        bounded_text(finding.get("confidence"), "finding confidence", 128)
        bounded_text(finding.get("explanation"), "finding explanation", 4_096)
        evidence = finding.get("evidence")
        if not isinstance(evidence, list):
            fail("finding evidence is invalid")
        evidence_total += len(evidence)
        if evidence_total > MAX_EVIDENCE:
            fail("drift report evidence exceeds the bound")
        for item in evidence:
            validate_evidence(item)
        unresolved_baseline |= baseline_state == "unresolved-baseline"
        if outcome != "pass" or baseline_state in ("resolved", "unresolved-baseline"):
            rows.append((adr_id, rule_id, outcome, baseline_state, len(evidence)))

    return report["policy"], counts, rows, unresolved_baseline


def outcome_for(status: int, counts: dict[str, int], unresolved_baseline: bool) -> str:
    if counts["unknown"] > 0 or unresolved_baseline:
        return "Unknown"
    if status == 4:
        return "Violation (policy failed)"
    if counts["violations"] > 0:
        return "Violation (advisory or baselined)"
    return "Pass"


def unavailable_lines(status: int) -> list[str]:
    return [
        "### ADR Guard — Architecture drift",
        "",
        "| Item | Result |",
        "| --- | --- |",
        f"| Exit code | `{status}` |",
        "| Report | Unavailable; inspect the raw CLI log. |",
    ]


def main() -> int:
    if len(sys.argv) != 3:
        print("usage: github-action-drift-report.py <status> <stdout-log>", file=sys.stderr)
        return 2

    status = int(sys.argv[1])
    report_path = Path(sys.argv[2])
    summary_path = os.environ.get("GITHUB_STEP_SUMMARY")
    if not summary_path:
        return 0

    if status not in (0, 3, 4) or not report_path.is_file() or report_path.stat().st_size == 0:
        lines = unavailable_lines(status)
    else:
        if report_path.stat().st_size > MAX_REPORT_BYTES:
            fail("drift JSON exceeds the reporting byte limit")
        report_text = report_path.read_text(encoding="utf-8")
        ensure_json_depth(report_text)
        try:
            report = json.loads(report_text, object_pairs_hook=unique_object)
        except RecursionError:
            fail("drift JSON nesting exceeds the parser depth limit")
        policy, counts, rows, unresolved_baseline = validate_report(report)
        inconclusive = counts["unknown"] > 0 or unresolved_baseline
        if status == 3 and not inconclusive:
            fail("exit 3 drift report is not inconclusive")
        if status in (0, 4) and inconclusive:
            fail("conclusive drift exit contains unknown evidence")
        if status == 4 and (policy != "enforce" or counts["violations"] == 0):
            fail("exit 4 drift report is not an enforced violation")

        lines = [
            "### ADR Guard — Architecture drift",
            "",
            "The CLI result is preserved; this summary does not recalculate policy or establish architectural approval.",
            "",
            "| Item | Result |",
            "| --- | --- |",
            f"| Outcome | {outcome_for(status, counts, unresolved_baseline)} |",
            f"| Policy | `{policy}` |",
            f"| Exit code | `{status}` |",
            f"| Rules | {counts['totalRules']} |",
            f"| Pass | {counts['pass']} |",
            f"| Violations | {counts['violations']} |",
            f"| Unknown | {counts['unknown']} |",
            f"| Not applicable | {counts['notApplicable']} |",
        ]
        if rows:
            lines.extend(("", "| ADR | Rule | Outcome | Baseline | Evidence |", "| --- | --- | --- | --- | ---: |"))
            for adr_id, rule_id, outcome, baseline_state, evidence_count in rows[:SUMMARY_FINDING_LIMIT]:
                lines.append(
                    f"| {code_span(adr_id)} | {code_span(rule_id)} | {outcome} | {baseline_state} | {evidence_count} |"
                )
            if len(rows) > SUMMARY_FINDING_LIMIT:
                lines.extend(("", f"Only the first {SUMMARY_FINDING_LIMIT} non-pass/baseline findings are shown; inspect the bounded JSON log for all results."))

    with Path(summary_path).open("a", encoding="utf-8", newline="\n") as stream:
        stream.write("\n".join(lines) + "\n")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError, json.JSONDecodeError) as error:
        print(f"ADR Guard: unable to render architecture drift summary: {error}", file=sys.stderr)
        raise SystemExit(1)
