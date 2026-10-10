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
    def test_p0_p1_catalog_is_valid(self):
        self.assertEqual(validator.validate_catalog(ROOT), [])

    def test_expected_skill_set_has_six_p0_and_five_p1(self):
        self.assertEqual(len(validator.EXPECTED_P0), 6)
        self.assertEqual(len(validator.EXPECTED_P1), 5)
        self.assertEqual(len(validator.EXPECTED_SKILLS), 11)
        self.assertFalse(validator.EXPECTED_P0 & validator.EXPECTED_P1)

    def test_missing_p1_skill_is_detected(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "skills").mkdir()
            self.assertIn(
                "missing required Agent Skill: adr-guard-audit",
                validator.validate_catalog(root),
            )

    def test_code_fence_examples_are_not_local_dependencies(self):
        with tempfile.TemporaryDirectory() as directory:
            skill = Path(directory) / "skill-name" / "SKILL.md"
            skill.parent.mkdir()
            skill.write_text(
                "---\nname: skill-name\ndescription: A sample. Use when testing.\n---\n# Test\n"
                "```markdown\n"
                "[Old ADR](0007-old.md)\n"
                "```\n"
                "~~~markdown\n"
                "[New ADR](0012-new.md)\n"
                "~~~\n",
                encoding="utf-8",
            )
            self.assertEqual(validator.validate_skill(skill), [])

    def test_missing_local_reference_is_detected(self):
        with tempfile.TemporaryDirectory() as directory:
            skill = Path(directory) / "skill-name" / "SKILL.md"
            skill.parent.mkdir()
            skill.write_text(
                "---\nname: skill-name\ndescription: A sample. Use when testing.\n---\n# Test\n"
                "[missing](references/nope.md)\n",
                encoding="utf-8",
            )
            self.assertTrue(any("missing local reference" in e for e in validator.validate_skill(skill)))

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
