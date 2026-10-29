"""Verify the pinned readonly CodeGraph MCP with an actual C# source query."""
import asyncio
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


async def check():
    config = tomllib.loads((ROOT / ".codex/config.toml").read_text(encoding="utf-8"))
    server = config["mcp_servers"]["codegraph"]
    before = database_digest()
    params = StdioServerParameters(command=server["command"], args=server["args"],
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
                result = await session.call_tool("codegraph_explore", {
                    "query": "TCTEnglish/Controllers/BaseController.cs",
                    "projectPath": str(ROOT),
                })
                text = "\n".join(block.text for block in result.content if block.type == "text")
                if result.is_error or "GetCurrentUserId" not in text or "BaseController" not in text:
                    raise RuntimeError("CodeGraph did not return the expected real C# source")
                print("CodeGraph 1.6.2 initialize/list_tools/explore C# source: PASS")
    finally:
        if database_digest() != before:
            raise RuntimeError("The CodeGraph database changed during read-only verification")
    print("Existing index database/WAL SHA-256 unchanged; no index/sync requested: PASS")


if __name__ == "__main__":
    asyncio.run(asyncio.wait_for(check(), timeout=45))
