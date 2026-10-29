"""Reject unpinned bootstrap inputs and invalid host configurations."""
import copy
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import agent_tooling as tooling
from agent_clients import json_files


class TeamConfigurationTests(unittest.TestCase):
    def setUp(self):
        self.lock = json.loads((tooling.ROOT / ".ai/tooling/bootstrap.lock.json").read_text(encoding="utf-8"))

    def test_reviewed_runtime_pins_accepted(self):
        self.assertEqual([], tooling.check_bootstrap_lock(self.lock))

    def test_mutable_runtime_version_or_source_rejected(self):
        for key in ("version", "commit"):
            value = copy.deepcopy(self.lock)
            value["uv"][key] = "latest"
            self.assertTrue(tooling.check_bootstrap_lock(value))

    def test_missing_hash_or_outside_archive_path_rejected(self):
        value = copy.deepcopy(self.lock)
        value["uv"]["assets"]["win32-x64"]["sha256"] = ""
        self.assertTrue(tooling.check_bootstrap_lock(value))
        value = copy.deepcopy(self.lock)
        value["uv"]["assets"]["win32-x64"]["executable"] = "../../outside.exe"
        self.assertTrue(tooling.check_bootstrap_lock(value))

    def test_index_task_is_never_scheduled_on_folder_open(self):
        tasks = json_files()[".vscode/tasks.json"]["tasks"]
        automatic = [task for task in tasks if task.get("runOptions", {}).get("runOn") == "folderOpen"]
        self.assertEqual(1, len(automatic))
        self.assertEqual("setup", automatic[0]["args"][-1])
        self.assertFalse(any(task.get("runOptions") for task in tasks if task["args"][-1] == "index"))

    def test_lifecycle_inventory_excludes_personal_client_settings(self):
        with tempfile.TemporaryDirectory(prefix="tct private settings ") as directory:
            root = Path(directory)
            for relative in (".claude/settings.local.json", ".cursor/personal.json", "CLAUDE.local.md"):
                target = root / relative
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_text("PRIVATE_SENTINEL", encoding="utf-8")
            self.assertEqual(set(), tooling.RepositoryFiles(root).paths())


if __name__ == "__main__":
    unittest.main()
