"""Regression tests for the development Agent Skills validator."""

import importlib.util
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "scripts" / "validate-development-skills.py"
spec = importlib.util.spec_from_file_location("development_skill_validator", SCRIPT)
validator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(validator)


class DevelopmentSkillsValidationTests(unittest.TestCase):
    def test_installed_development_skills_are_valid(self):
        self.assertEqual(validator.validate_development_skills(), [])

    def test_multiline_description_is_parsed(self):
        fields, _, errors = validator.parse_frontmatter(
            ROOT / ".agents" / "skills" / "run-tests" / "SKILL.md"
        )
        self.assertEqual(errors, [])
        self.assertEqual(fields["name"], "run-tests")
        self.assertIn("ALWAYS USE", fields["description"])

    def test_overlay_contract_is_valid(self):
        self.assertEqual(validator.validate_overlay(), [])


if __name__ == "__main__":
    unittest.main()
