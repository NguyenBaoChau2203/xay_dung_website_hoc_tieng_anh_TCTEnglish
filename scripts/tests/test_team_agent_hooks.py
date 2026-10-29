"""Native host hook contracts, independent of any installed IDE or model."""
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import agent_lifecycle as hooks


class TeamHookTests(unittest.TestCase):
    def test_antigravity_payload_uses_camel_case_without_reading_arguments(self):
        event = hooks.normalize_event("antigravity", "PreToolUse", {
            "conversationId": "a", "toolCall": {"name": "invoke_subagent", "args": {"SECRET": "never copy"}},
            "transcriptPath": "must not open",
        })
        self.assertEqual("a", event["session_id"])
        self.assertEqual("invoke_subagent", event["tool_name"])
        self.assertNotIn("SECRET", str(event))
        self.assertNotIn("transcript", str(event))

    def test_cursor_subagent_start_maps_to_delegation(self):
        event = hooks.normalize_event("cursor", "PreToolUse", {"conversation_id": "b", "subagent_id": "worker"})
        self.assertEqual("Agent", event["tool_name"])

    def test_cursor_native_output_denies_without_auto_approval(self):
        canonical = {"hookSpecificOutput": {"permissionDecision": "deny", "permissionDecisionReason": "one agent"}}
        self.assertEqual({"permission": "deny", "user_message": "one agent"}, hooks.host_output("cursor", "PreToolUse", canonical))
        self.assertEqual({}, hooks.host_output("cursor", "PreToolUse", {}))

    def test_cursor_session_context_and_post_context_have_native_names(self):
        result = hooks.additional_context("SessionStart", "project rules")
        self.assertEqual({"additional_context": "project rules"}, hooks.host_output("cursor", "SessionStart", result))

    def test_antigravity_context_and_denial_use_native_schema(self):
        result = hooks.additional_context("SessionStart", "project rules")
        self.assertEqual({"injectSteps": [{"ephemeralMessage": "project rules"}]}, hooks.host_output("antigravity", "SessionStart", result))
        deny = {"hookSpecificOutput": {"permissionDecision": "deny", "permissionDecisionReason": "one agent"}}
        self.assertEqual({"decision": "deny", "reason": "one agent"}, hooks.host_output("antigravity", "PreToolUse", deny))

    def test_antigravity_post_output_is_empty_and_stop_never_forces_continuation(self):
        warning = {"systemMessage": "tooling failed"}
        self.assertEqual({}, hooks.host_output("antigravity", "PostToolUse", warning))
        self.assertEqual("stop", hooks.host_output("antigravity", "Stop", warning)["decision"])
        self.assertNotIn("followup_message", hooks.host_output("cursor", "Stop", warning))

    def test_codex_and_claude_keep_the_shared_contract(self):
        result = hooks.additional_context("PostToolUse", "checked tooling")
        for host in ("claude", "codex"):
            self.assertEqual(result, hooks.host_output(host, "PostToolUse", result))


if __name__ == "__main__":
    unittest.main()
