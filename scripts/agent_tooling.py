"""Validate project skills and locked sources without executing external tools."""
from __future__ import annotations

import argparse
import hashlib
import json
import re
import subprocess
import sys
import tomllib
from pathlib import Path, PurePosixPath
from agent_clients import LOADER, hook_command, json_files

ROOT = Path(__file__).resolve().parent.parent
LOCK = ".ai/tooling/sources.lock.json"
READ_TOOLS = {"git_status", "git_diff_unstaged", "git_diff_staged", "git_diff", "git_log", "git_show", "git_branch"}
LIFECYCLE_EVENTS = {"SessionStart", "PreToolUse", "PostToolUse", "Stop"}


def check_codegraph_config(server: dict, manifest: dict, lock: dict) -> list[str]:
    errors = []
    name = "@colbymchenry/codegraph"
    version = manifest.get("dependencies", {}).get(name, "")
    if not re.fullmatch(r"\d+\.\d+\.\d+", version):
        errors.append("CodeGraph needs an exact project version")
    packages = lock.get("packages", {})
    if packages.get("", {}).get("dependencies", {}).get(name) != version:
        errors.append("CodeGraph root package and lockfile differ")
    entry = packages.get("node_modules/" + name, {})
    if entry.get("version") != version:
        errors.append("Locked CodeGraph version differs from the project pin")
    for key, package in packages.items():
        if key.startswith("node_modules/" + name):
            if package.get("version") != version or not re.fullmatch(r"sha512-[A-Za-z0-9+/]+={0,2}", package.get("integrity", "")):
                errors.append(f"{key}: version/integrity missing or inconsistent")
    if not entry:
        errors.append("CodeGraph package is missing from the lockfile")
    if server.get("enabled_tools") != ["codegraph_explore"]:
        errors.append("Expose only the reviewed CodeGraph explore tool")
    if server.get("command") != "node" or server.get("args") != ["-e", LOADER, "mcp", "codegraph"]:
        errors.append("Use the project read-only CodeGraph MCP launcher")
    expected_env = {"CODEGRAPH_TELEMETRY": "0", "DO_NOT_TRACK": "1", "CODEGRAPH_NO_DOWNLOAD": "1", "CODEGRAPH_NO_UPDATE_CHECK": "1"}
    if any(server.get("env", {}).get(key) != value for key, value in expected_env.items()):
        errors.append("CodeGraph project environment must disable telemetry and network fallbacks")
    return errors


def check_hook_config(config: dict) -> list[str]:
    errors = []
    events = config.get("hooks", {})
    if set(events) != LIFECYCLE_EVENTS:
        errors.append("Lifecycle hook events differ from the four reviewed events")
    for name, groups in events.items():
        if not isinstance(groups, list) or len(groups) != 1:
            errors.append(f"{name}: expected one reviewed hook group")
            continue
        handlers = groups[0].get("hooks", [])
        if len(handlers) != 1:
            errors.append(f"{name}: expected one command handler")
            continue
        handler = handlers[0]
        if handler.get("type") != "command" or handler.get("async", False):
            errors.append(f"{name}: only the synchronous local command handler is reviewed")
        portable = handler.get("command", "")
        windows = handler.get("commandWindows", "")
        if portable != hook_command("codex", name):
            errors.append(f"{name}: unreviewed portable hook launcher")
        if windows != hook_command("codex", name):
            errors.append(f"{name}: missing or unreviewed Windows launcher")
        if not 0 < handler.get("timeout", 0) <= 30:
            errors.append(f"{name}: expected a bounded timeout of at most 30 seconds")
        if any(token in portable + windows for token in ("--dangerously", "@latest", "curl ", "Invoke-WebRequest")):
            errors.append(f"{name}: automatic download or trust bypass is not allowed")
    return errors


def safe_relative(value: str) -> bool:
    return bool(value) and not any(c in value for c in ("\\", ":", "\x00")) and not PurePosixPath(value).is_absolute() and ".." not in PurePosixPath(value).parts


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def sqlite_data_fingerprint(database: Path) -> dict[str, str]:
    result = {database.name: digest(database.read_bytes())}
    wal = database.with_name(database.name + "-wal")
    try:
        wal_bytes = wal.read_bytes()
    except FileNotFoundError:
        wal_bytes = b""
    # A read-only SQLite connection may create/preserve an empty WAL/SHM pair.
    # Empty WAL has no transaction frames; nonempty WAL remains part of the hash.
    if wal_bytes:
        result[wal.name] = digest(wal_bytes)
    return result


def check_digest(path: str, data: bytes, expected: str) -> list[str]:
    return [] if digest(data) == expected else [f"{path}: source hash changed; review the source lock"]


def check_skill(folder: str, text: str) -> list[str]:
    errors = []
    match = re.match(r"\A---\r?\n(.*?)\r?\n---(?:\r?\n|$)", text, re.S)
    if not match:
        return [f"{folder}: missing YAML frontmatter"]
    fields = {}
    for line in match[1].splitlines():
        key, sep, value = line.partition(":")
        if sep and key in ("name", "description"):
            if key in fields:
                errors.append(f"{folder}: duplicate {key}")
            fields[key] = value.strip().strip("\"'")
    name = fields.get("name", "")
    if name != folder or not re.fullmatch(r"[a-z0-9]+(?:-[a-z0-9]+)*", name) or len(name) > 64:
        errors.append(f"{folder}: name must match the folder and use a lowercase slug")
    if not fields.get("description") or len(fields["description"]) > 1024:
        errors.append(f"{folder}: description is missing or longer than 1024 characters")
    return errors


def check_dependencies(dependencies: list[str]) -> list[str]:
    return [f"Unpinned MCP dependency: {item}" for item in dependencies
            if not re.fullmatch(r"[a-zA-Z0-9_.-]+==\d+(?:\.\d+)+", item)]


def check_bootstrap_lock(config: dict) -> list[str]:
    errors = []
    uv = config.get("uv", {})
    version = uv.get("version", "")
    if config.get("schema_version") != 1 or not re.fullmatch(r"\d+\.\d+\.\d+", version):
        errors.append("Bootstrap runtime schema/version must be pinned")
    if not re.fullmatch(r"[0-9a-f]{40}", uv.get("commit", "")):
        errors.append("Bootstrap uv source needs a full reviewed commit")
    if not re.fullmatch(r"\d+\.\d+\.\d+", config.get("python", "")):
        errors.append("Bootstrap Python needs an exact version")
    expected = {"win32-x64", "win32-arm64", "darwin-x64", "darwin-arm64", "linux-x64", "linux-arm64"}
    assets = uv.get("assets", {})
    if set(assets) != expected:
        errors.append("Bootstrap platform catalog differs from the reviewed platforms")
    for platform, asset in assets.items():
        prefix = f"https://github.com/astral-sh/uv/releases/download/{version}/uv-"
        if not asset.get("url", "").startswith(prefix) or not re.fullmatch(r"[0-9a-f]{64}", asset.get("sha256", "")):
            errors.append(f"{platform}: bootstrap URL/SHA-256 must be pinned")
        if not isinstance(asset.get("size"), int) or asset["size"] <= 0 or not safe_relative(asset.get("executable", "")):
            errors.append(f"{platform}: invalid runtime size/executable path")
    return errors


def check_team_setup(view: RepositoryFiles, expected_skills: set[str]) -> list[str]:
    errors = check_bootstrap_lock(json.loads(view.text(".ai/tooling/bootstrap.lock.json")))
    for relative, definition in json_files().items():
        if json.loads(view.text(relative)) != definition:
            errors.append(f"{relative}: native client configuration differs from reviewed definitions")
    for name in expected_skills:
        relative = f".claude/skills/{name}/SKILL.md"
        text = view.text(relative)
        errors.extend(check_skill(name, text))
        if f".agents/skills/{name}/SKILL.md" not in text:
            errors.append(f"{relative}: canonical skill link missing")
    if "@AGENTS.md" not in view.text("CLAUDE.md"):
        errors.append("CLAUDE.md must import the canonical project rules")
    for relative in ("scripts/agent-tools.cjs", "scripts/agent_clients.py", "tooling/mcp/git_readonly.py", ".githooks/post-merge", "SETUP-AGENTS.cmd"):
        view.text(relative)
    return errors


class RepositoryFiles:
    def __init__(self, root: Path, staged: bool = False):
        self.root = root.resolve()
        self.staged = staged
        self._index_contents = None

    def git(self, *args: str) -> bytes:
        return subprocess.run(["git", *args], cwd=self.root, check=True, capture_output=True).stdout

    def paths(self) -> set[str]:
        if self.staged:
            return set(self.git("ls-files", "--cached", "-z").decode("utf-8").strip("\x00").split("\x00")) - {""}
        paths = set()
        for directory in (".agents/skills", ".agent/skills", ".claude/skills", ".ai/vendor", ".ai/roles", ".ai/tooling", ".codex", "tooling/mcp", ".githooks", "scripts", ".github/workflows"):
            base = self.root / directory
            if base.exists():
                paths.update(p.relative_to(self.root).as_posix() for p in base.rglob("*")
                             if p.is_file() and ".venv" not in p.parts and "__pycache__" not in p.parts)
        paths.update(p for p in ("AGENTS.md", "DESIGN.md") if (self.root / p).is_file())
        paths.update(p for p in ("CLAUDE.md", ".mcp.json", ".agents/hooks.json", ".agents/mcp_config.json", ".claude/settings.json", ".cursor/hooks.json", ".cursor/mcp.json", ".vscode/tasks.json", "SETUP-AGENTS.cmd") if (self.root / p).is_file())
        paths.update(p for p in ("tooling/codegraph/package.json", "tooling/codegraph/package-lock.json", "tooling/codegraph/launch.cjs", "tooling/codegraph/mcp-readonly.cjs") if (self.root / p).is_file())
        return paths

    def read(self, path: str) -> bytes:
        if not safe_relative(path):
            raise ValueError(f"Path must stay inside the repository: {path}")
        if self.staged:
            if self._index_contents is None:
                self._load_tooling_index()
            if path in self._index_contents:
                return self._index_contents[path]
            try:
                return self.git("show", f":{path}")
            except subprocess.CalledProcessError as ex:
                raise FileNotFoundError(path) from ex
        target = (self.root / path).resolve()
        if not target.is_relative_to(self.root):
            raise ValueError(f"Path resolves outside the repository: {path}")
        return target.read_bytes()

    def _load_tooling_index(self) -> None:
        # One Git process reads the relevant staged blobs, rather than spawning
        # a process per CSV/reference. Blob IDs avoid interpreting path characters.
        blobs = []
        for record in self.git("ls-files", "--stage", "-z").split(b"\x00"):
            if not record:
                continue
            metadata, path = record.split(b"\t", 1)
            _, object_id, stage = metadata.split()
            name = path.decode("utf-8")
            if stage != b"0":
                raise ValueError(f"Unresolved index conflict: {name}")
            if name.startswith((".agents/", ".agent/", ".claude/", ".cursor/", ".ai/", ".codex/", "tooling/")) or name in ("DESIGN.md", "CLAUDE.md", ".mcp.json", ".vscode/tasks.json"):
                blobs.append((name, object_id))
        self._index_contents = {}
        if not blobs:
            return
        result = subprocess.run(["git", "cat-file", "--batch"], cwd=self.root,
                                input=b"\n".join(oid for _, oid in blobs) + b"\n",
                                check=True, capture_output=True).stdout
        offset = 0
        for name, _ in blobs:
            end = result.index(b"\n", offset)
            _, kind, size = result[offset:end].split()
            if kind != b"blob":
                raise ValueError(f"Expected a staged blob: {name}")
            length = int(size)
            self._index_contents[name] = result[end + 1:end + 1 + length]
            offset = end + length + 2

    def text(self, path: str) -> str:
        text = self.read(path).decode("utf-8", errors="strict")
        if "\ufffd" in text:
            raise ValueError(f"{path}: Unicode replacement character")
        return text


def validate(view: RepositoryFiles) -> list[str]:
    errors = []
    paths = view.paths()
    lock = json.loads(view.text(LOCK))
    if lock.get("schema_version") != 1:
        errors.append("Unsupported source lock schema")
    expected_skills = set(lock["installed_skills"])
    skills = {PurePosixPath(p).parent.name: p for p in paths
              if re.fullmatch(r"\.agents/skills/[^/]+/SKILL\.md", p)}
    if set(skills) != expected_skills:
        errors.append(f"Skill catalog differs from lock: {sorted(set(skills) ^ expected_skills)}")
    for folder, path in skills.items():
        text = view.text(path)
        errors.extend(check_skill(folder, text))
        # Entrypoints must resolve all local Markdown references. Upstream reference
        # documents retain their original links and are checked by content hashes.
        for target in re.findall(r"\]\(([^)]+)\)", text):
            if re.match(r"[a-z]+://|#", target):
                continue
            resolved = (view.root / PurePosixPath(path).parent / target.split("#")[0]).resolve()
            if not resolved.is_relative_to(view.root) or resolved.relative_to(view.root).as_posix() not in paths:
                errors.append(f"{path}: missing/outside reference {target}")
    for folder in lock["compatibility_skills"]:
        path = f".agent/skills/{folder}/SKILL.md"
        canonical = f".agents/skills/tct-{folder}/SKILL.md"
        if canonical not in view.text(path):
            errors.append(f"{path}: compatibility entrypoint is not linked to {canonical}")
    seen = set()
    for source in lock["sources"]:
        if source["repository"] in seen:
            errors.append(f"Duplicate source: {source['repository']}")
        seen.add(source["repository"])
        if not re.fullmatch(r"[0-9a-f]{40}", source["commit"]):
            errors.append(f"{source['repository']}: source must be pinned to a full commit")
        for entry in source["files"]:
            path = entry["path"]
            if not safe_relative(path) or path not in paths:
                errors.append(f"Missing or invalid source path: {path}")
                continue
            errors.extend(check_digest(path, view.read(path), entry["sha256"]))
    config = tomllib.loads(view.text(".codex/config.toml"))
    if config.get("features", {}).get("multi_agent") is not False:
        errors.append("Codex multi_agent must remain false under the owner's policy")
    if config.get("features", {}).get("hooks") or ".codex/hooks.json" in paths:
        errors.extend(check_hook_config(json.loads(view.text(".codex/hooks.json"))))
        if config.get("hooks"):
            errors.append("Use only hooks.json for the project layer, not duplicate inline hooks")
        for path in ("scripts/agent_lifecycle.py", "scripts/run-agent-hook.ps1"):
            view.text(path)
    git_server = config["mcp_servers"]["tct_git"]
    if set(git_server.get("enabled_tools", [])) != READ_TOOLS:
        errors.append("Git MCP allowlist must contain exactly the reviewed read tools")
    if ".ai/tooling/bootstrap.lock.json" in paths:
        errors.extend(check_team_setup(view, expected_skills))
        if git_server.get("command") != "node" or git_server.get("args") != ["-e", LOADER, "mcp", "git"]:
            errors.append("Git MCP must use the shared read-only project launcher")
    if "codegraph" in config["mcp_servers"] or "tooling/codegraph/package.json" in paths:
        errors.extend(check_codegraph_config(config["mcp_servers"].get("codegraph", {}),
                                            json.loads(view.text("tooling/codegraph/package.json")),
                                            json.loads(view.text("tooling/codegraph/package-lock.json"))))
        for path in ("tooling/codegraph/launch.cjs", "tooling/codegraph/mcp-readonly.cjs", "scripts/codegraph.ps1", "scripts/start-codegraph-mcp.ps1"):
            view.text(path)
    project = tomllib.loads(view.text("tooling/mcp/pyproject.toml"))
    errors.extend(check_dependencies(project["project"]["dependencies"]))
    view.text("tooling/mcp/uv.lock")
    design = view.text("DESIGN.md")
    for section in ("## Overview", "## Colors", "## Typography", "## Spacing", "## Components"):
        if section not in design:
            errors.append(f"DESIGN.md: missing {section}")
    for role in ("implementer", "ui-reviewer", "security-reviewer"):
        if "same agent" not in view.text(f".ai/roles/{role}.md"):
            errors.append(f"{role}: role must explicitly apply to the same agent")
    for path in paths:
        if path.startswith((".agents/skills/", ".agent/skills/", ".ai/roles/", ".codex/")) and path.endswith((".md", ".toml", ".yaml", ".py", ".csv")):
            view.text(path)
    return errors


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--staged", action="store_true", help="Read exclusively from the Git index")
    args = parser.parse_args()
    view = RepositoryFiles(ROOT, staged=args.staged)
    if args.staged:
        changed = view.git("diff", "--cached", "--name-only", "-z").decode("utf-8").split("\x00")
        prefixes = (".agents/", ".agent/", ".claude/", ".cursor/", ".ai/", ".codex/", ".githooks/", "tooling/", "scripts/", ".vscode/tasks.json", ".mcp.json", "CLAUDE.md", "SETUP-AGENTS.cmd", "AGENTS.md", "DESIGN.md")
        if not any(p.startswith(prefixes) for p in changed if p):
            print("Agent tooling: no staged tooling changes.")
            return 0
    try:
        errors = validate(view)
    except (OSError, ValueError, KeyError, TypeError, subprocess.CalledProcessError) as ex:
        errors = [f"Tooling validation could not complete: {ex}"]
    for error in errors:
        print(f"[FAIL] {error}", file=sys.stderr)
    if errors:
        return 1
    print(f"Agent tooling verified ({'Git index' if args.staged else 'working tree'}).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
