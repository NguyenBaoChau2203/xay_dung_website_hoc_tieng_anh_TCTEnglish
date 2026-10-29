"""Project lifecycle checks with explicit native host contracts; inputs are data."""
from __future__ import annotations

import hashlib
import json
import os
import sys
import tempfile
import tomllib
from pathlib import Path

from agent_tooling import ROOT, LIFECYCLE_EVENTS, RepositoryFiles, check_hook_config, validate

EVENTS = LIFECYCLE_EVENTS
HOSTS = {"codex", "claude", "cursor", "antigravity"}
DELEGATION_TOOLS = {"spawn_agent", "Agent", "Task", "followup_task", "collaboration.spawn_agent", "collaboration.followup_task", "invoke_subagent", "define_subagent"}
EDIT_TOOLS = {"Bash", "Shell", "exec_command", "apply_patch", "Edit", "Write", "run_command", "write_to_file", "replace_file_content", "multi_replace_file_content"}
CONTEXT = (
    "TCT English project context: read AGENTS.md and .ai/README.md; use only the "
    "relevant .agents/skills/tct-* skill. One agent implements and reviews. "
    "Use CodeGraph first when indexed; do not re-index automatically. Read "
    "DESIGN.md for UI work and current project files for actual framework versions. "
    "Protected config changes require authority from the user's task. Before "
    "completion verify changed behavior, build/relevant tests and report evidence "
    "in Vietnamese. Lifecycle checks validate tooling only; they do not prove "
    "application tests or browser behavior passed."
)


def fingerprint(root: Path) -> str:
    view = RepositoryFiles(root)
    result = hashlib.sha256()
    for relative in sorted(view.paths()):
        # Source/config only; never include .tmp state, transcripts, appsettings,
        # environment files, application uploads or credentials.
        result.update(relative.encode("utf-8") + b"\x00")
        result.update(hashlib.sha256(view.read(relative)).digest())
    return result.hexdigest()


def state_path(root: Path, event: dict) -> Path:
    session_key = hashlib.sha256((str(event.get("host", "codex")) + ":" + str(event.get("session_id", "no-session"))).encode("utf-8")).hexdigest()
    return root / ".tmp/agent-hooks" / f"{session_key}.json"


def load_state(path: Path) -> dict:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
        return value if isinstance(value, dict) else {}
    except (OSError, ValueError):
        return {}


def save_state(path: Path, value: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = None
    try:
        with tempfile.NamedTemporaryFile(mode="w", encoding="utf-8", dir=path.parent, delete=False) as handle:
            temporary = Path(handle.name)
            json.dump(value, handle)
        os.replace(temporary, path)
        temporary = None
    finally:
        if temporary is not None:
            temporary.unlink(missing_ok=True)


def additional_context(name: str, message: str) -> dict:
    return {"hookSpecificOutput": {"hookEventName": name, "additionalContext": message}}


def normalize_event(host: str, name: str, event: dict) -> dict:
    """Copy only the metadata needed by checks, never arguments or transcript paths."""
    if host not in HOSTS or name not in EVENTS:
        raise ValueError("Unsupported lifecycle host or event")
    if host == "antigravity":
        session = event.get("conversationId", "no-session")
        tool = event.get("toolCall", {}).get("name", "")
    else:
        session = event.get("session_id", event.get("conversation_id", "no-session"))
        tool = event.get("tool_name", "")
        if host == "cursor" and name == "PreToolUse" and event.get("subagent_id"):
            tool = "Agent"
    return {"host": host, "session_id": str(session), "tool_name": tool, "hook_event_name": name}


def host_output(host: str, name: str, result: dict) -> dict:
    specific = result.get("hookSpecificOutput", {})
    denied = specific.get("permissionDecision") == "deny"
    reason = specific.get("permissionDecisionReason", "")
    message = specific.get("additionalContext", result.get("systemMessage", ""))
    if host in ("codex", "claude"):
        return result
    if host == "cursor":
        if denied:
            return {"permission": "deny", "user_message": reason}
        if message and name in ("SessionStart", "PostToolUse"):
            return {"additional_context": message}
        return {}
    if host == "antigravity":
        if name == "PreToolUse":
            return {"decision": "deny", "reason": reason} if denied else {"decision": "allow"}
        if name == "SessionStart" and message:
            return {"injectSteps": [{"ephemeralMessage": message}]}
        if name == "Stop":
            return {"decision": "stop", **({"reason": message} if message else {})}
        # Antigravity requires an empty PostToolUse response. Feedback goes to
        # stderr in main; do not inject an unsupported additionalContext field.
        return {}
    raise ValueError("Unsupported lifecycle host")


def check_tooling(name: str, event: dict, root: Path) -> dict:
    path = state_path(root, event)
    state = load_state(path)
    before = fingerprint(root)
    if state.get("checked_fingerprint") == before:
        errors = state.get("errors", [])
        if not errors or name != "Stop":
            return {}
    elif state.get("baseline_fingerprint") == before:
        return {}
    else:
        try:
            errors = validate(RepositoryFiles(root))
        except (OSError, ValueError, KeyError, TypeError) as ex:
            errors = [f"Tooling check could not complete: {ex}"]
        if fingerprint(root) != before:
            return {"systemMessage": "TCT tooling changed during validation; verification was not cached. Recheck once edits finish."}
        state.update(checked_fingerprint=before, errors=[str(error)[:300] for error in errors[:4]])
        save_state(path, state)
    if errors:
        message = "TCT tooling validation failed: " + "; ".join(str(error)[:300] for error in errors[:4])
        message += ". Fix or report these checks as unverified; run python scripts/agent_tooling.py."
        # Feedback never hides a tool result or forces Stop to loop indefinitely.
        if name == "PostToolUse":
            return additional_context(name, message)
        return {"systemMessage": message}
    message = "TCT tooling validation passed for the current content. This does not certify application build/tests."
    return additional_context(name, message) if name == "PostToolUse" else {"systemMessage": message}


def handle(name: str, event: dict, root: Path = ROOT) -> dict:
    if name not in EVENTS:
        return {}
    root = root.resolve()
    if name == "SessionStart":
        save_state(state_path(root, event), {"baseline_fingerprint": fingerprint(root)})
        return additional_context(name, CONTEXT)
    if name == "PreToolUse":
        if event.get("tool_name") not in DELEGATION_TOOLS:
            return {}
        config = tomllib.loads((root / ".codex/config.toml").read_text(encoding="utf-8"))
        if config.get("features", {}).get("multi_agent") is False:
            return {"hookSpecificOutput": {
                "hookEventName": name,
                "permissionDecision": "deny",
                "permissionDecisionReason": "The owner requires one agent. Perform investigation, implementation and review inline; do not delegate."
            }}
        return {}
    if name == "PostToolUse" and event.get("tool_name") not in EDIT_TOOLS:
        return {}
    return check_tooling(name, event, root)


def main() -> int:
    try:
        if len(sys.argv) not in (2, 4) or sys.argv[1] not in EVENTS:
            raise ValueError("Expected one supported lifecycle event argument")
        host = sys.argv[3] if len(sys.argv) == 4 and sys.argv[2] == "--host" else "codex"
        if host not in HOSTS or (len(sys.argv) == 4 and sys.argv[2] != "--host"):
            raise ValueError("Unsupported host argument")
        event = json.load(sys.stdin)
        if not isinstance(event, dict):
            raise ValueError("Expected an event object")
        if host in ("codex", "claude") and event.get("hook_event_name", sys.argv[1]) != sys.argv[1]:
            raise ValueError("Event argument and input disagree")
        result = handle(sys.argv[1], normalize_event(host, sys.argv[1], event))
        output = host_output(host, sys.argv[1], result)
        message = result.get("systemMessage", result.get("hookSpecificOutput", {}).get("additionalContext", ""))
        if message and ((host == "antigravity" and sys.argv[1] in ("PostToolUse", "Stop")) or (host == "cursor" and sys.argv[1] == "Stop")):
            print(message, file=sys.stderr)
        print(json.dumps(output, ensure_ascii=True))
        return 0
    except (OSError, ValueError, KeyError, TypeError) as ex:
        # Do not echo the input, tool arguments, prompt or transcript contents.
        print(f"TCT lifecycle check could not run: {type(ex).__name__}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
