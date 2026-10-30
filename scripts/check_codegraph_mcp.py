"""Verify the pinned readonly CodeGraph MCP with an actual C# source query."""
import asyncio
import argparse
import json
import tomllib
from pathlib import Path

from mcp import ClientSession, StdioServerParameters
from mcp.client.stdio import stdio_client
from agent_tooling import sqlite_data_fingerprint

ROOT = Path(__file__).resolve().parent.parent
DATABASE = ROOT / ".codegraph/codegraph.db"


def database_digest():
    # WAL contains committed SQLite changes not yet folded into the main file.
    # The shared-memory sidecar contains volatile reader locks and is excluded.
    return sqlite_data_fingerprint(DATABASE)


async def check(project_mode=False):
    config = tomllib.loads((ROOT / ".codex/config.toml").read_text(encoding="utf-8"))
    server = config["mcp_servers"]["codegraph"]
    before = database_digest()
    if project_mode:
        policy = json.loads((ROOT / ".ai/codegraph.local.json").read_text(encoding="utf-8"))
        if policy.get("mode") != "auto-sync":
            raise RuntimeError("Project-mode check requires explicit checkout-local auto-sync approval")
    # Default smoke always forces read-only, independently of the local opt-in.
    params = StdioServerParameters(command="node", args=[str(ROOT / "tooling/codegraph/launch.cjs"), "--mcp-project" if project_mode else "--mcp-readonly"],
                                   env=server["env"], cwd=str(ROOT / "TCTEnglish"))
    try:
        async with stdio_client(params) as (reader, writer):
            async with ClientSession(reader, writer) as session:
                initialized = await session.initialize()
                if initialized.server_info.version != "1.6.2":
                    raise RuntimeError("CodeGraph MCP server version does not match the reviewed pin")
                discovered = await session.list_tools()
                names = {tool.name for tool in discovered.tools}
                if names != {"codegraph_explore"}:
                    raise RuntimeError(f"Unexpected CodeGraph tool exposure: {sorted(names)}")
                query = "SpeakingImportJobService" if project_mode else "TCTEnglish/Controllers/BaseController.cs"
                result = await session.call_tool("codegraph_explore", {
                    "query": query,
                    "projectPath": str(ROOT),
                    "maxFiles": 1,
                })
                text = "\n".join(block.text for block in result.content if block.type == "text")
                expected = "SpeakingImportJobService" if project_mode else "GetCurrentUserId"
                if result.is_error or expected not in text:
                    raise RuntimeError("CodeGraph did not return the expected real C# source")
                print("CodeGraph 1.6.2 initialize/list_tools/explore C# source: PASS")
    finally:
        if not project_mode and database_digest() != before:
            raise RuntimeError("The CodeGraph database changed during read-only verification")
    if project_mode:
        print("Owner-approved project-mode MCP, only explore, rebuilt C# symbol query: PASS")
    else:
        print("Existing index database/WAL SHA-256 unchanged; no index/sync requested: PASS")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-mode", action="store_true", help="Check an explicitly approved local auto-sync mode")
    args = parser.parse_args()
    asyncio.run(asyncio.wait_for(check(args.project_mode), timeout=45))
