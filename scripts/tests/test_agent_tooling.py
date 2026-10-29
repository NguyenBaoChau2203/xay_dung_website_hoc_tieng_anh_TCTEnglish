"""Behavior checks for repository tooling, including the actual Git index."""
import importlib.util
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

SPEC = importlib.util.spec_from_file_location(
    "agent_tooling", Path(__file__).resolve().parents[1] / "agent_tooling.py"
)
tooling = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = tooling
SPEC.loader.exec_module(tooling)


class SkillMetadataTests(unittest.TestCase):
    def test_slug_and_description_required(self):
        self.assertEqual([], tooling.check_skill("demo", "---\nname: demo\ndescription: Use when reviewing UI.\n---\nBody"))
        self.assertTrue(tooling.check_skill("demo", "---\nname: UI Component\n---\nBody"))

    def test_duplicate_metadata_rejected(self):
        self.assertTrue(tooling.check_skill("demo", "---\nname: demo\nname: other\ndescription: Review.\n---\n"))

    def test_lock_cannot_escape_repository(self):
        for path in ("../secret", "C:/secret", "/secret", "a/../../secret", "a\\secret"):
            with self.subTest(path=path):
                self.assertFalse(tooling.safe_relative(path))
        self.assertTrue(tooling.safe_relative(".ai/vendor/demo/LICENSE"))

    def test_hash_detects_modified_vendor_file(self):
        self.assertEqual([], tooling.check_digest("sample", b"abc", tooling.digest(b"abc")))
        self.assertTrue(tooling.check_digest("sample", b"changed", tooling.digest(b"abc")))

    def test_unpinned_mcp_dependency_rejected(self):
        self.assertTrue(tooling.check_dependencies(["mcp-server-git>=2026.10.19"]))
        self.assertTrue(tooling.check_dependencies(["mcp-server-git==2026.*"]))
        self.assertEqual([], tooling.check_dependencies(["mcp-server-git==2026.10.19"]))


class GitRepositoryCase(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="tct tooling spaces ")
        self.root = Path(self.temp.name)
        self.git("init", "--quiet")

    def tearDown(self):
        self.temp.cleanup()

    def git(self, *args):
        return subprocess.run(["git", *args], cwd=self.root, check=True, capture_output=True)


class GitIndexTests(GitRepositoryCase):
    def test_staged_reader_ignores_unstaged_fix(self):
        path = self.root / "example.txt"
        path.write_text("staged failure", encoding="utf-8")
        self.git("add", "example.txt")
        path.write_text("unstaged correction", encoding="utf-8")
        self.assertEqual(b"staged failure", tooling.RepositoryFiles(self.root, staged=True).read("example.txt"))
        self.assertEqual(b"unstaged correction", tooling.RepositoryFiles(self.root).read("example.txt"))

    def test_staged_removal_is_missing_even_if_working_file_reappears(self):
        path = self.root / "reference.md"
        path.write_text("reference", encoding="utf-8")
        self.git("add", "reference.md")
        self.git("rm", "--cached", "reference.md")
        self.assertTrue(path.exists())
        view = tooling.RepositoryFiles(self.root, staged=True)
        self.assertNotIn("reference.md", view.paths())
        with self.assertRaises(FileNotFoundError):
            view.read("reference.md")


class FullValidationTests(GitRepositoryCase):
    def write(self, path, content):
        target = self.root / path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(content, encoding="utf-8", newline="\n")

    def fixture(self):
        self.write(".agents/skills/tct-demo/SKILL.md", "---\nname: tct-demo\ndescription: Use when checking a demo.\n---\n")
        self.write(".agent/skills/demo/SKILL.md", "Read .agents/skills/tct-demo/SKILL.md")
        self.write(".ai/vendor/demo/reference.md", "reviewed source")
        lock = {
            "schema_version": 1,
            "installed_skills": ["tct-demo"],
            "compatibility_skills": ["demo"],
            "sources": [{"repository": "example/demo", "commit": "a" * 40,
                         "files": [{"path": ".ai/vendor/demo/reference.md", "sha256": tooling.digest(b"reviewed source")}]}],
        }
        self.write(".ai/tooling/sources.lock.json", json.dumps(lock))
        self.write(".codex/config.toml", '[features]\nmulti_agent=false\n[mcp_servers.tct_git]\nenabled_tools=' + json.dumps(sorted(tooling.READ_TOOLS)))
        self.write("tooling/mcp/pyproject.toml", '[project]\ndependencies=["mcp-server-git==2026.10.19"]')
        self.write("tooling/mcp/uv.lock", "version=1")
        self.write("DESIGN.md", "\n".join("## " + title for title in ("Overview", "Colors", "Typography", "Spacing", "Components")))
        for role in ("implementer", "ui-reviewer", "security-reviewer"):
            self.write(f".ai/roles/{role}.md", "Use the same agent.")

    def test_complete_tooling_snapshot_validates(self):
        self.fixture()
        self.assertEqual([], tooling.validate(tooling.RepositoryFiles(self.root)))

    def test_staged_vendor_corruption_not_hidden_by_unstaged_repair(self):
        self.fixture()
        self.write(".ai/vendor/demo/reference.md", "corrupted source")
        self.git("add", ".")
        self.write(".ai/vendor/demo/reference.md", "reviewed source")
        self.assertEqual([], tooling.validate(tooling.RepositoryFiles(self.root)))
        errors = tooling.validate(tooling.RepositoryFiles(self.root, staged=True))
        self.assertTrue(any("source hash changed" in error for error in errors), errors)

    def test_reference_must_be_staged_to_resolve_from_index(self):
        self.fixture()
        self.write(".agents/skills/tct-demo/SKILL.md", "---\nname: tct-demo\ndescription: Demo.\n---\n[details](details.md)")
        self.git("add", ".")
        self.write(".agents/skills/tct-demo/details.md", "exists only in working tree")
        self.assertEqual([], tooling.validate(tooling.RepositoryFiles(self.root)))
        errors = tooling.validate(tooling.RepositoryFiles(self.root, staged=True))
        self.assertTrue(any("missing/outside reference" in error for error in errors), errors)


if __name__ == "__main__":
    unittest.main()
