"""Reviewed project client definitions. Import to validate; --write regenerates adapters."""
from __future__ import annotations

import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
LOADER = (
    "const cp=require('node:child_process');const p=require('node:path');"
    "const r=cp.execFileSync('git',['rev-parse','--show-toplevel'],{encoding:'utf8',windowsHide:true}).trim();"
    "require(p.join(r,'scripts/agent-tools.cjs')).main(process.argv.slice(1))"
    ".then(c=>{process.exitCode=c;}).catch(e=>{console.error('TCT tools: '+(process.argv[1]==='hook'?'Hook could not run; check local setup.':e.message));process.exitCode=1;});"
)
CODEGRAPH_ENV = {"CODEGRAPH_TELEMETRY": "0", "DO_NOT_TRACK": "1", "CODEGRAPH_NO_DOWNLOAD": "1", "CODEGRAPH_NO_UPDATE_CHECK": "1", "CODEGRAPH_MCP_TOOLS": "explore"}
MATCHERS = {
    "codex": (r"^(spawn_agent|Agent|Task|followup_task|collaboration\.(spawn_agent|followup_task))$", r"^(Bash|exec_command|apply_patch|Edit|Write)$"),
    "claude": (r"^(Agent|Task)$", r"^(Bash|Edit|Write)$"),
    "cursor": (r"^(Task|Agent)$", r"^(Shell|Edit|Write|ApplyPatch)$"),
    "antigravity": (r"^(invoke_subagent|define_subagent)$", r"^(run_command|write_to_file|replace_file_content|multi_replace_file_content)$"),
}


def hook_command(host: str, event: str) -> str:
    return f'node -e "{LOADER}" hook {host} {event}'


def mcp_server(kind: str) -> dict:
    result = {"command": "node", "args": ["-e", LOADER, "mcp", kind]}
    if kind == "codegraph":
        result["env"] = CODEGRAPH_ENV.copy()
    return result


def hook_config(host: str) -> dict:
    groups = {}
    for event in ("SessionStart", "PreToolUse", "PostToolUse", "Stop"):
        handler = {"type": "command", "command": hook_command(host, event), "timeout": 30}
        group = {"hooks": [handler]}
        if event == "PreToolUse":
            group["matcher"] = MATCHERS[host][0]
        elif event == "PostToolUse":
            group["matcher"] = MATCHERS[host][1]
        groups[event] = [group]
    if host == "codex":
        # This fixed node command works with both platform shells; no pwsh dependency.
        for event, definitions in groups.items():
            definitions[0]["hooks"][0]["commandWindows"] = hook_command(host, event)
        return {"description": "TCT English lifecycle checks; native host trust required.", "hooks": groups}
    if host == "claude":
        return {"hooks": groups}
    if host == "cursor":
        names = {"SessionStart": "sessionStart", "PreToolUse": "preToolUse", "PostToolUse": "postToolUse", "Stop": "stop"}
        native = {}
        for event, definitions in groups.items():
            handler = definitions[0]["hooks"][0]
            value = {"command": handler["command"], "timeout": 30}
            if event == "PreToolUse":
                value["matcher"] = MATCHERS[host][0]
            native[names[event]] = [value]
        # Cursor exposes a dedicated gate as well as the Task tool interception.
        native["subagentStart"] = [{"command": hook_command(host, "PreToolUse"), "timeout": 30}]
        return {"version": 1, "hooks": native}
    if host == "antigravity":
        native = {}
        for event, definitions in groups.items():
            native[event] = definitions if event in ("PreToolUse", "PostToolUse") else definitions[0]["hooks"]
        return {"tct-project-checks": native}
    raise ValueError("Unknown client")


def json_files() -> dict[str, dict]:
    mcp = {"mcpServers": {"tct_git": mcp_server("git"), "codegraph": mcp_server("codegraph")}}
    return {
        ".codex/hooks.json": hook_config("codex"),
        ".claude/settings.json": hook_config("claude"),
        ".cursor/hooks.json": hook_config("cursor"),
        ".agents/hooks.json": hook_config("antigravity"),
        ".mcp.json": mcp,
        ".cursor/mcp.json": mcp,
        ".agents/mcp_config.json": mcp,
        ".vscode/tasks.json": {"version": "2.0.0", "tasks": [
            {"label": "TCT: Set up shared agent tools", "type": "process", "command": "node",
             "args": ["${workspaceFolder}/scripts/agent-tools.cjs", "setup"],
             "options": {"cwd": "${workspaceFolder}"}, "problemMatcher": [],
             "runOptions": {"runOn": "folderOpen", "instanceLimit": 1},
             "presentation": {"reveal": "silent", "panel": "dedicated"}},
            {"label": "TCT: Check agent tooling", "type": "process", "command": "node",
             "args": ["${workspaceFolder}/scripts/agent-tools.cjs", "validate"],
             "options": {"cwd": "${workspaceFolder}"}, "problemMatcher": []},
            {"label": "TCT: Create CodeGraph index (explicit action)", "type": "process", "command": "node",
             "args": ["${workspaceFolder}/scripts/agent-tools.cjs", "index"],
             "options": {"cwd": "${workspaceFolder}"}, "problemMatcher": [],
             "presentation": {"reveal": "always", "panel": "dedicated"}},
        ]},
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--write", action="store_true", help="Write the reviewed managed client JSON files")
    args = parser.parse_args()
    if not args.write:
        parser.error("Use --write explicitly; normal validation imports these definitions")
    for relative, definition in json_files().items():
        target = ROOT / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(json.dumps(definition, indent=2) + "\n", encoding="utf-8", newline="\n")
    for source in sorted((ROOT / ".agents/skills").glob("*/SKILL.md")):
        name = source.parent.name
        description = next(line.partition(":")[2].strip() for line in source.read_text(encoding="utf-8").splitlines() if line.startswith("description:"))
        target = ROOT / ".claude/skills" / name / "SKILL.md"
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(f"---\nname: {name}\ndescription: {description}\n---\n\n"
                          f"Read `.agents/skills/{name}/SKILL.md` from the repository root.\n"
                          "That canonical skill owns the procedure, scripts and relative references.\n"
                          "Follow AGENTS.md and use the same agent for implementation and review.\n",
                          encoding="utf-8", newline="\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
