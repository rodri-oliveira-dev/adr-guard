"""Regression coverage for source-version to bilingual-documentation consistency."""

import importlib.util
import unittest
from pathlib import Path
from unittest.mock import patch


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "scripts" / "validate-version-documentation.py"
spec = importlib.util.spec_from_file_location("version_documentation_validator", SCRIPT)
validator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(validator)


class VersionDocumentationValidationTests(unittest.TestCase):
    def test_current_versions_are_documented(self):
        self.assertEqual(validator.validate_version_documentation(ROOT), [])

    def test_version_match_requires_a_complete_semver_token(self):
        self.assertTrue(validator.contains_version("CLI version 1.12.0 is available", "1.12.0"))
        self.assertFalse(validator.contains_version("CLI version 1.12.01 is available", "1.12.0"))
        self.assertFalse(validator.contains_version("CLI version 11.12.0 is available", "1.12.0"))

    def test_empty_required_documents_are_rejected(self):
        original_read = validator.read_text
        for required_path in (
            "docs/cli-reference.pt-BR.md",
            "docs/mcp.pt-BR.md",
            "extensions/vscode/README.pt-BR.md",
        ):
            for empty_text in ("", "   \\n"):
                with self.subTest(path=required_path, content=repr(empty_text)):
                    def empty_reference(root, relative, errors):
                        if relative == required_path:
                            return empty_text
                        return original_read(root, relative, errors)

                    with patch.object(validator, "read_text", side_effect=empty_reference):
                        errors = validator.validate_version_documentation(ROOT)

                    self.assertTrue(
                        any(required_path in error and "not documented" in error
                            for error in errors),
                        errors,
                    )

    def test_missing_reference_is_detected_even_when_other_documents_are_current(self):
        version = validator.xml_version(ROOT, "src/AdrGuard/AdrGuard.csproj", [])
        original_read = validator.read_text

        def stale_reference(root, relative, errors):
            value = original_read(root, relative, errors)
            if relative == "docs/cli-reference.pt-BR.md":
                return value.replace(version, "0.0.0")
            return value

        with patch.object(validator, "read_text", side_effect=stale_reference):
            errors = validator.validate_version_documentation(ROOT)

        self.assertTrue(
            any("docs/cli-reference.pt-BR.md" in error and "not documented" in error
                for error in errors),
            errors,
        )


if __name__ == "__main__":
    unittest.main()
