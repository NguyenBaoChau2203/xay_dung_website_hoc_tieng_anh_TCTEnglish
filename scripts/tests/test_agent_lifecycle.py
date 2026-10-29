"""Lifecycle behavior without starting another model or executing event content."""
import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from agent_clients import hook_config
SPEC = importlib.util.spec_from_file_location("agent_lifecycle", Path(__file__).resolve().parents[1] / "agent_lifecycle.py")
hooks = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = hooks
SPEC.loader.exec_module(hooks)


class HookConfigurationTests(unittest.TestCase):
    def fixture(self):
        return hook_config("codex")

    def test_reviewed_events_and_local_launchers_accepted(self):
        self.assertEqual([], hooks.check_hook_config(self.fixture()))

    def test_missing_event_rejected(self):
        config = self.fixture()
        del config["hooks"]["Stop"]
        self.assertTrue(hooks.check_hook_config(config))

    def test_agent_handler_or_mutable_download_not_accepted(self):
        config = self.fixture()
        config["hooks"]["Stop"][0]["hooks"][0]["type"] = "agent"
        self.assertTrue(hooks.check_hook_config(config))
        config = self.fixture()
        config["hooks"]["Stop"][0]["hooks"][0]["command"] = "npx unreviewed@latest"
        self.assertTrue(hooks.check_hook_config(config))


class LifecycleTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="tct lifecycle spaces ")
        self.root = Path(self.temp.name)
        (self.root / ".codex").mkdir()
        (self.root / ".codex/config.toml").write_text("[features]\nmulti_agent=false\n", encoding="utf-8")
        self.event = {"session_id": "../../escape", "tool_name": "apply_patch"}

    def tearDown(self):
        self.temp.cleanup()

    def handle(self, name, **extra):
        return hooks.handle(name, dict(self.event, **extra), self.root)

    def test_session_context_does_not_echo_payload_or_transcript(self):
        with patch.object(hooks, "fingerprint", return_value="baseline"):
            result = self.handle("SessionStart", prompt="PRIVATE_SENTINEL", transcript_path="C:/private/transcript")
        output = json.dumps(result)
        self.assertIn("AGENTS.md", output)
        self.assertNotIn("PRIVATE_SENTINEL", output)
        self.assertNotIn("C:/private", output)
        files = list((self.root / ".tmp/agent-hooks").glob("*.json"))
        self.assertEqual(1, len(files))
        self.assertTrue(files[0].resolve().is_relative_to(self.root))

    def test_direct_delegation_denied_without_approving_other_tools(self):
        for name in ("spawn_agent", "Agent", "collaboration.spawn_agent", "followup_task"):
            with self.subTest(name=name):
                result = self.handle("PreToolUse", tool_name=name)
                self.assertEqual("deny", result["hookSpecificOutput"]["permissionDecision"])
        self.assertEqual({}, self.handle("PreToolUse", tool_name="mcp__mail__send_message"))
        self.assertEqual({}, self.handle("PreToolUse", tool_name="Bash", tool_input={"command": "echo hello"}))

    def test_owner_can_change_the_project_single_agent_setting(self):
        (self.root / ".codex/config.toml").write_text("[features]\nmulti_agent=true\n", encoding="utf-8")
        self.assertEqual({}, self.handle("PreToolUse", tool_name="spawn_agent"))

    def test_unchanged_tooling_is_quiet_and_not_revalidated(self):
        with patch.object(hooks, "fingerprint", return_value="same"), patch.object(hooks, "validate") as validate:
            self.handle("SessionStart")
            self.assertEqual({}, self.handle("PostToolUse"))
            self.assertEqual({}, self.handle("Stop"))
            validate.assert_not_called()

    def test_changed_tooling_validated_once_and_cached_by_content(self):
        with patch.object(hooks, "fingerprint", return_value="baseline"):
            self.handle("SessionStart")
        with patch.object(hooks, "fingerprint", return_value="new"), patch.object(hooks, "validate", return_value=[]) as validate:
            result = self.handle("PostToolUse", tool_input={"command": "DO_NOT_EXECUTE"})
            self.assertIn("passed", json.dumps(result))
            self.assertEqual({}, self.handle("Stop"))
            self.assertEqual(1, validate.call_count)

    def test_validation_failure_warns_without_hiding_tool_result_or_looping(self):
        with patch.object(hooks, "fingerprint", return_value="new"), patch.object(hooks, "validate", return_value=["missing skill"]):
            post = self.handle("PostToolUse")
            stop = self.handle("Stop", stop_hook_active=True)
        self.assertIn("missing skill", json.dumps(post))
        self.assertIn("missing skill", json.dumps(stop))
        self.assertNotIn("decision", post)
        self.assertNotIn("decision", stop)

    def test_mutation_during_validation_is_not_cached_as_verified(self):
        with patch.object(hooks, "fingerprint", side_effect=["before", "after"]), patch.object(hooks, "validate", return_value=[]):
            result = self.handle("Stop")
        self.assertNotIn("passed", json.dumps(result))
        with patch.object(hooks, "fingerprint", return_value="after"), patch.object(hooks, "validate", return_value=[]) as validate:
            self.handle("Stop")
            validate.assert_called_once()

    def test_unknown_event_cannot_execute_input(self):
        with patch.object(hooks, "validate") as validate:
            self.assertEqual({}, self.handle("NotAnEvent", tool_input={"command": "DO_NOT_EXECUTE"}))
            validate.assert_not_called()


if __name__ == "__main__":
    unittest.main()
