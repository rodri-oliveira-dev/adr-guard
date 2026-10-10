"""Regression tests for the dependency-free Agent Skills catalog validator."""

import importlib.util
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "scripts" / "validate-agent-skills.py"
spec = importlib.util.spec_from_file_location("adr_skill_validator", SCRIPT)
validator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(validator)


class AgentSkillsValidationTests(unittest.TestCase):
    def test_p0_catalog_is_valid(self):
        self.assertEqual(validator.validate_catalog(ROOT), [])

    def test_rejects_mismatched_skill_name(self):
        with tempfile.TemporaryDirectory() as directory:
            skill = Path(directory) / "expected" / "SKILL.md"
            skill.parent.mkdir()
            skill.write_text(
                "---\nname: different\ndescription: A sample. Use when testing.\n---\n# Test\n",
                encoding="utf-8",
            )
            self.assertTrue(any("mismatched directory" in e for e in validator.validate_skill(skill)))

    def test_rejects_reference_that_escapes_installed_skill(self):
        with tempfile.TemporaryDirectory() as directory:
            skill = Path(directory) / "skill-name" / "SKILL.md"
            skill.parent.mkdir()
            skill.write_text(
                "---\nname: skill-name\ndescription: A sample. Use when testing.\n---\n# Test\n"
                "[Repository only](../../docs/anything.md)\n",
                encoding="utf-8",
            )
            self.assertTrue(any("non-portable link" in e for e in validator.validate_skill(skill)))

    def test_rejects_missing_frontmatter(self):
        with tempfile.TemporaryDirectory() as directory:
            skill = Path(directory) / "skill-name" / "SKILL.md"
            skill.parent.mkdir()
            skill.write_text("# No frontmatter\n", encoding="utf-8")
            self.assertTrue(any("opening YAML" in e for e in validator.validate_skill(skill)))


if __name__ == "__main__":
    unittest.main()
