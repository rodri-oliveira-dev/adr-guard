#!/usr/bin/env python3
"""Render a bounded advisory summary from ADR Guard impact JSON."""

from __future__ import annotations

import json
import os
import sys
from pathlib import Path
from typing import Any

MAX_REPORT_BYTES = 2_000_000
MAX_DECISIONS = 1_000
MAX_CHANGES = 10_000
MAX_EVIDENCE = 5_000
SUMMARY_DECISION_LIMIT = 50


def fail(message: str) -> None:
    raise ValueError(message)


def bounded_text(value: Any, label: str, maximum: int = 2_048) -> str:
    if not isinstance(value, str) or not value or len(value) > maximum:
        fail(f"{label} is invalid or exceeds its limit")
    return value


def markdown(value: str) -> str:
    return (
        value.replace("\\", "\\\\")
        .replace("|", "\\|")
        .replace("<", "&lt;")
        .replace(">", "&gt;")
        .replace("\r", " ")
        .replace("\n", " ")
    )


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


def validate_report(report: Any) -> tuple[dict[str, int], list[tuple[str, str, int]]]:
    if not isinstance(report, dict) or report.get("schemaVersion") != "1.0" or report.get("advisory") is not True:
        fail("impact report is not the advisory schemaVersion 1.0 contract")

    summary = report.get("summary")
    coverage = report.get("coverage")
    decisions = report.get("decisions")
    changes = report.get("changedFiles")
    if not isinstance(summary, dict) or not isinstance(coverage, dict):
        fail("impact report summary or coverage is invalid")
    if not isinstance(decisions, list) or len(decisions) > MAX_DECISIONS:
        fail("impact report decisions exceed the bound")
    if not isinstance(changes, list) or len(changes) > MAX_CHANGES:
        fail("impact report changed files exceed the bound")

    counts = {
        "totalChanges": require_count(summary, "totalChanges", MAX_CHANGES),
        "affectedChanges": require_count(coverage, "affectedChanges", MAX_CHANGES),
        "unknownChanges": require_count(coverage, "unknownChanges", MAX_CHANGES),
        "notMatchedChanges": require_count(coverage, "notMatchedChanges", MAX_CHANGES),
    }
    if counts["totalChanges"] != len(changes) or sum(counts[key] for key in ("affectedChanges", "unknownChanges", "notMatchedChanges")) != len(changes):
        fail("impact report coverage counts are inconsistent")

    rows: list[tuple[str, str, int]] = []
    evidence_total = 0
    for decision in decisions:
        if not isinstance(decision, dict):
            fail("impact decision is invalid")
        stable_id = bounded_text(decision.get("stableId"), "decision stableId", 256)
        status = decision.get("status")
        evidence = decision.get("evidence")
        if status not in ("affected", "not-matched", "unknown") or not isinstance(evidence, list):
            fail("impact decision status or evidence is invalid")
        evidence_total += len(evidence)
        if evidence_total > MAX_EVIDENCE:
            fail("impact report evidence exceeds the bound")
        if status != "not-matched":
            rows.append((stable_id, status, len(evidence)))
    return counts, rows


def main() -> int:
    if len(sys.argv) != 3:
        print("usage: github-action-impact-report.py <status> <stdout-log>", file=sys.stderr)
        return 2

    status = int(sys.argv[1])
    report_path = Path(sys.argv[2])
    summary_path = os.environ.get("GITHUB_STEP_SUMMARY")
    if not summary_path:
        return 0

    lines = ["### ADR Guard — Architecture impact (advisory)", "", "This report is a review aid, not a compliance decision.", ""]
    if status != 0:
        lines.extend(("| Item | Result |", "| --- | --- |", f"| Exit code | `{status}` |", "| Report | Unavailable; inspect the raw CLI log. |"))
    else:
        if report_path.stat().st_size > MAX_REPORT_BYTES:
            fail("impact JSON exceeds the reporting byte limit")
        report = json.loads(report_path.read_text(encoding="utf-8"), object_pairs_hook=unique_object)
        counts, rows = validate_report(report)
        lines.extend((
            "| Coverage | Count |",
            "| --- | ---: |",
            f"| Changed files | {counts['totalChanges']} |",
            f"| Affected | {counts['affectedChanges']} |",
            f"| Unknown | {counts['unknownChanges']} |",
            f"| Not mapped | {counts['notMatchedChanges']} |",
        ))
        if rows:
            lines.extend(("", "| Decision | Assessment | Evidence |", "| --- | --- | ---: |"))
            for stable_id, assessment, evidence_count in rows[:SUMMARY_DECISION_LIMIT]:
                lines.append(f"| `{markdown(stable_id)}` | {assessment} | {evidence_count} |")
            if len(rows) > SUMMARY_DECISION_LIMIT:
                lines.extend(("", f"Only the first {SUMMARY_DECISION_LIMIT} affected/unknown decisions are shown; inspect the bounded JSON log for all results."))

    with Path(summary_path).open("a", encoding="utf-8", newline="\n") as stream:
        stream.write("\n".join(lines) + "\n")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError, json.JSONDecodeError) as error:
        print(f"ADR Guard: unable to render architecture impact summary: {error}", file=sys.stderr)
        raise SystemExit(1)
