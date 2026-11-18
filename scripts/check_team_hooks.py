"""Run the exact configured native commands with representative host payloads."""
import json
import os
import shutil
import subprocess
import uuid
from pathlib import Path

from agent_clients import ROOT, hook_command


def check():
    shells = [["pwsh", "-NoLogo", "-NoProfile", "-Command"]] if shutil.which("pwsh") else []
    if shutil.which("sh"):
        shells.append(["sh", "-c"])
    if os.name == "nt":
        shells.append(["cmd.exe", "/d", "/s", "/c"])
    if not shells:
        raise RuntimeError("No supported shell found for the native command smoke check")
    for shell in shells:
        hosts = ("antigravity",) if shell[0] == "cmd.exe" else ("codex", "claude", "cursor", "antigravity")
        for host in hosts:
            cwd = ROOT if host == "antigravity" else ROOT / "TCTEnglish"
            session = str(uuid.uuid4())
            common = {"session_id": session, "conversationId": session, "conversation_id": session,
                      "transcript_path": "PRIVATE_SENTINEL_NOT_A_FILE", "prompt": "PRIVATE_SENTINEL"}
            for name in ("SessionStart", "PreToolUse", "PostToolUse", "Stop"):
                event = dict(common)
                if host == "antigravity":
                    event["toolCall"] = {"name": "invoke_subagent" if name == "PreToolUse" else "run_command", "args": {}}
                else:
                    event["tool_name"] = "Agent" if name == "PreToolUse" else "Bash"
                result = subprocess.run([*shell, hook_command(host, name)], input=json.dumps(event),
                                        cwd=cwd, capture_output=True, encoding="utf-8", timeout=35)
                if result.returncode or "PRIVATE_SENTINEL" in result.stdout + result.stderr:
                    raise RuntimeError(f"{host}/{name} command failed or echoed private input")
                output = json.loads(result.stdout)
                if name == "PreToolUse":
                    decision = output.get("permission", output.get("decision", output.get("hookSpecificOutput", {}).get("permissionDecision")))
                    if decision != "deny":
                        raise RuntimeError(f"{host} did not deny direct delegation")
                if name == "SessionStart" and "AGENTS.md" not in result.stdout:
                    raise RuntimeError(f"{host} did not return project context")
            malformed = subprocess.run([*shell, hook_command(host, "SessionStart")], input='{"PRIVATE_SENTINEL":',
                                       cwd=cwd, capture_output=True, encoding="utf-8", timeout=35)
            if not malformed.returncode or "PRIVATE_SENTINEL" in malformed.stdout + malformed.stderr:
                raise RuntimeError(f"{host} malformed-event isolation failed")
            # Run the actual configured command; never execute a subagent tool.
            for model in ("gemini-3.8-flash-high", "Gemini 3.8 Flash High", "gemini-3.8-flash-low", None):
                for tool in ("invoke_subagent", "define_subagent"):
                    event = dict(common, modelName=model, tool_name="Agent",
                                 toolCall={"name": tool, "args": {"modelName": "gemini-3.8-flash-high"}})
                    result = subprocess.run([*shell, hook_command(host, "PreToolUse")], input=json.dumps(event),
                                            cwd=cwd, capture_output=True, encoding="utf-8", timeout=35)
                    if result.returncode or "PRIVATE_SENTINEL" in result.stdout + result.stderr:
                        raise RuntimeError(f"{host} model-gate command failed or echoed private input")
                    output = json.loads(result.stdout)
                    decision = output.get("permission", output.get("decision", output.get("hookSpecificOutput", {}).get("permissionDecision")))
                    expected = "allow" if host == "antigravity" and model in ("gemini-3.8-flash-high", "Gemini 3.8 Flash High") else "deny"
                    if decision != expected or any(key in output for key in ("overwrite", "permissionOverrides")):
                        raise RuntimeError(f"{host} violated the scoped model-gate contract")
        print(f"{', '.join(hosts)} configured commands / four events / model gate / malformed input through {shell[0]}: PASS")
    print("These are script/contract checks; automatic IDE invocation and trust are separate.")


if __name__ == "__main__":
    check()
