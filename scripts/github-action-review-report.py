#!/usr/bin/env python3
"""Render ADR Guard AI review JSON into safe GitHub annotations and step summary."""

from __future__ import annotations

import html
import json
import os
from pathlib import Path
import sys


ANNOTATION_LIMIT = 50
SUMMARY_FINDING_LIMIT = 50


def workflow_escape(value: str, *, property_value: bool = False) -> str:
    escaped = value.replace("%", "%25").replace("\r", "%0D").replace("\n", "%0A")
    if property_value:
        escaped = escaped.replace(":", "%3A").replace(",", "%2C")
    return escaped


def normalized_text(value: object, limit: int = 4000) -> str:
    text = str(value if value is not None else "")
    text = " ".join(text.split())
    return text[:limit]


def markdown_text(value: object, limit: int = 4000) -> str:
    text = html.escape(normalized_text(value, limit), quote=False)
    for character in (
        "\\", "`", "*", "_", "{", "}", "[", "]", "(", ")",
        "#", "+", "-", ".", "!", "|", ">",
    ):
        text = text.replace(character, "\\" + character)
    return text


def markdown_code(value: object, limit: int = 4000) -> str:
    text = html.escape(normalized_text(value, limit), quote=True)
    text = text.replace("|", "&#124;")
    return "<code>" + text + "</code>"


def is_safe_filename(value: object) -> bool:
    if not isinstance(value, str) or not value.strip():
        return False
    return "/" not in value and "\\" not in value and Path(value).name == value


def inside(path: Path, root: Path) -> bool:
    try:
        path.relative_to(root)
        return True
    except ValueError:
        return False


def relative_display(path: Path, workspace: Path) -> str:
    return path.relative_to(workspace).as_posix()


def read_manifest(path: Path, workspace: Path) -> dict[str, Path]:
    sources: dict[str, Path] = {}
    if not path.is_file():
        return sources

    for raw_line in path.read_text(encoding="utf-8").splitlines():
        if not raw_line:
            continue
        source_id, separator, raw_path = raw_line.partition("\t")
        if not separator or not source_id or not raw_path:
            continue

        candidate = Path(raw_path).resolve()
        if candidate.is_file() and inside(candidate, workspace):
            sources[source_id] = candidate

    return sources


def resolve_existing_sources(
    report: dict[str, object],
    source_map: dict[str, Path],
    target: Path,
    workspace: Path,
) -> None:
    input_scope = report.get("inputScope")
    if not isinstance(input_scope, dict):
        return

    existing = input_scope.get("existingAdrs")
    if not isinstance(existing, list):
        return

    search_root = target.parent.resolve()

    for source in existing:
        if not isinstance(source, dict):
            continue
        source_id = source.get("sourceId")
        source_path = source.get("path")
        if not isinstance(source_id, str) or source_id in source_map:
            continue
        if not is_safe_filename(source_path):
            continue

        matches: list[Path] = []
        for candidate in search_root.rglob(source_path):
            resolved = candidate.resolve()
            if (
                candidate.is_file()
                and resolved.suffix.lower() == ".md"
                and inside(resolved, search_root)
                and inside(resolved, workspace)
            ):
                matches.append(resolved)

        unique = list(dict.fromkeys(matches))
        if len(unique) == 1:
            source_map[source_id] = unique[0]


def render_non_success(
    status: int,
    provider: str,
    model: str,
) -> tuple[str, list[str]]:
    outcomes = {
        1: "Selected ADR failed structural validation",
        2: "Review configuration or CLI usage error",
        3: "Provider, transport, cancellation, or operational failure",
        4: "Deterministic review policy failed",
    }
    outcome = outcomes.get(status, f"Container execution failed (exit {status})")

    notes = [
        "AI findings are advisory and never become an architectural approval or rejection.",
    ]

    if status == 4:
        notes.append(
            "A deterministic local policy rule failed before provider-backed review completed."
        )
    elif status == 3:
        notes.append(
            "No clean-review conclusion is inferred from provider or operational failure."
        )

    summary = (
        f"### ADR Guard — AI review\n\n"
        f"| Item | Result |\n"
        f"| --- | --- |\n"
        f"| Provider | {markdown_code(provider)} |\n"
        f"| Model | {markdown_code(model)} |\n"
        f"| Exit code | `{status}` |\n"
        f"| Outcome | {markdown_text(outcome)} |\n"
    )
    return summary, notes


def main() -> int:
    if len(sys.argv) != 9:
        print("ADR Guard AI review reporter received invalid arguments.", file=sys.stderr)
        return 1

    status = int(sys.argv[1])
    workspace = Path(sys.argv[2]).resolve()
    target = Path(sys.argv[3]).resolve()
    stdout_log = Path(sys.argv[4])
    _stderr_log = Path(sys.argv[5])
    manifest = Path(sys.argv[6])
    provider = sys.argv[7]
    model = sys.argv[8]

    summary_path_value = os.environ.get("GITHUB_STEP_SUMMARY", "")
    summary_path = Path(summary_path_value) if summary_path_value else None

    if status != 0:
        summary, notes = render_non_success(status, provider, model)
        if summary_path is not None:
            with summary_path.open("a", encoding="utf-8") as stream:
                stream.write(summary)
                stream.write("\n")
                for note in notes:
                    stream.write(f"- {note}\n")
        return 0

    try:
        report = json.loads(stdout_log.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as exc:
        print(f"ADR Guard AI review report JSON could not be parsed: {exc}", file=sys.stderr)
        return 1

    if not isinstance(report, dict) or report.get("schemaVersion") != "1.0":
        print("ADR Guard AI review report has an unsupported schema.", file=sys.stderr)
        return 1

    findings = report.get("findings")
    target_report = report.get("target")
    if not isinstance(findings, list) or not isinstance(target_report, dict):
        print("ADR Guard AI review report is incomplete.", file=sys.stderr)
        return 1

    report_target_path = target_report.get("path")
    if not is_safe_filename(report_target_path) or target.name != report_target_path:
        print("ADR Guard AI review target could not be verified.", file=sys.stderr)
        return 1

    source_map = read_manifest(manifest, workspace)
    source_map["target"] = target
    resolve_existing_sources(report, source_map, target, workspace)

    outcome = report.get("outcome", "unknown")
    annotations: list[tuple[Path, str]] = []
    summary_findings: list[str] = []

    for finding in findings[:SUMMARY_FINDING_LIMIT]:
        if not isinstance(finding, dict):
            continue

        dimension = normalized_text(finding.get("dimension", "unknown"), 200)
        classification = normalized_text(finding.get("classification", "unknown"), 200)
        priority = normalized_text(finding.get("followUpPriority", "advisory"), 200)
        explanation = normalized_text(finding.get("explanation", ""), 2000)
        guidance = normalized_text(finding.get("guidance", ""), 2000)
        uncertainty = normalized_text(finding.get("uncertainty", ""), 1000)

        evidence = finding.get("evidence")
        verified_path: Path | None = None
        if isinstance(evidence, list):
            for item in evidence:
                if not isinstance(item, dict):
                    continue
                source_id = item.get("sourceId")
                evidence_path = item.get("path")
                if not isinstance(source_id, str) or not is_safe_filename(evidence_path):
                    continue

                candidate = source_map.get(source_id)
                if candidate is None:
                    continue

                candidate = candidate.resolve()
                if (
                    candidate.is_file()
                    and inside(candidate, workspace)
                    and candidate.name == evidence_path
                ):
                    verified_path = candidate
                    break

        details = f"{dimension} [{classification}] — {priority}: {explanation}"
        if guidance:
            details += f" Guidance: {guidance}"
        if uncertainty:
            details += f" Uncertainty: {uncertainty}"

        if verified_path is not None and len(annotations) < ANNOTATION_LIMIT:
            annotations.append((verified_path, details))

        evidence_text = (
            f"verified path {markdown_code(relative_display(verified_path, workspace), 1000)}"
            if verified_path is not None
            else "no verified local path; no file annotation emitted"
        )
        uncertainty_text = (
            f" Uncertainty: {markdown_text(uncertainty)}."
            if uncertainty
            else ""
        )
        summary_findings.append(
            f"- **{markdown_text(dimension)}** — "
            f"{markdown_code(classification)} / {markdown_code(priority)}: "
            f"{markdown_text(explanation)} Evidence: {evidence_text}.{uncertainty_text}"
        )

    for path, message in annotations:
        relative = relative_display(path, workspace)
        print(
            "::warning file="
            + workflow_escape(relative, property_value=True)
            + ",title=ADR Guard AI review::"
            + workflow_escape(message[:4000])
        )

    if summary_path is not None:
        with summary_path.open("a", encoding="utf-8") as stream:
            stream.write("### ADR Guard — AI review (advisory)\n\n")
            stream.write("| Item | Result |\n| --- | --- |\n")
            stream.write(f"| Provider | {markdown_code(provider)} |\n")
            stream.write(f"| Model | {markdown_code(model)} |\n")
            stream.write(f"| Target | {markdown_code(relative_display(target, workspace), 1000)} |\n")
            stream.write(f"| Exit code | `0` |\n")
            stream.write(f"| Outcome | {markdown_code(outcome, 200)} |\n")
            stream.write(f"| Follow-up findings | {len(findings)} |\n")
            stream.write(f"| File annotations | {len(annotations)} (cap: {ANNOTATION_LIMIT}) |\n")
            stream.write("\n")
            stream.write(
                "> AI-assisted review is advisory. Human review remains authoritative; "
                "warnings do not approve, reject, or automatically fail the ADR.\n\n"
            )

            if summary_findings:
                stream.write("#### Follow-up findings\n\n")
                stream.write("\n".join(summary_findings))
                stream.write("\n")
                if len(findings) > SUMMARY_FINDING_LIMIT:
                    stream.write(
                        f"\nOnly the first {SUMMARY_FINDING_LIMIT} findings are shown in the summary.\n"
                    )
            else:
                stream.write(
                    "No follow-up findings were reported. This is not an architectural approval.\n"
                )

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
