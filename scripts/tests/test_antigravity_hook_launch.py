"""Exercise the checked Antigravity command as Windows transports actually quote it."""
import json
import os
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from agent_clients import ROOT


@unittest.skipUnless(os.name == "nt", "Windows command transport regression")
class AntigravityHookLaunchTests(unittest.TestCase):
    def test_configured_hook_survives_cmd_and_powershell_in_relocated_checkout_with_spaces(self):
        scratch = ROOT / ".tmp"
        scratch.mkdir(exist_ok=True)
        with tempfile.TemporaryDirectory(prefix="antigravity hook spaces ", dir=scratch) as directory:
            root = Path(directory)
            for relative in ("scripts/agent-tools.cjs", ".codex/config.toml", ".ai/tooling/bootstrap.lock.json"):
                target = root / relative
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copyfile(ROOT / relative, target)
            subprocess.run(["git", "init", "--quiet", str(root)], check=True, capture_output=True)
            definition = json.loads((ROOT / ".agents/hooks.json").read_text(encoding="utf-8"))
            command = definition["tct-project-checks"]["PreToolUse"][0]["hooks"][0]["command"]
            for shell in (["cmd.exe", "/d", "/s", "/c"], ["pwsh", "-NoProfile", "-Command"]):
                for model, expected in (("gemini-3.8-flash-high", "allow"), ("Gemini 3.8 Flash High", "allow"),
                                        (None, "deny"), ({}, "deny"), ("gemini-3.8-flash-low", "deny")):
                    with self.subTest(shell=shell[0], model=model):
                        event = {"modelName": model, "toolCall": {"name": "invoke_subagent", "args": {
                            "modelName": "gemini-3.8-flash-high", "private": "PRIVATE_SENTINEL"}}}
                        result = subprocess.run([*shell, command], input=json.dumps(event), cwd=root,
                                                capture_output=True, encoding="utf-8", timeout=35)
                        self.assertEqual(0, result.returncode, result.stderr)
                        self.assertNotIn("PRIVATE_SENTINEL", result.stdout + result.stderr)
                        self.assertEqual({"decision": "allow"} if expected == "allow" else "deny",
                                         json.loads(result.stdout) if expected == "allow" else json.loads(result.stdout)["decision"])
                result = subprocess.run([*shell, command], input='{"PRIVATE_SENTINEL":', cwd=root,
                                        capture_output=True, encoding="utf-8", timeout=35)
                self.assertNotEqual(0, result.returncode)
                self.assertNotIn("PRIVATE_SENTINEL", result.stdout + result.stderr)
