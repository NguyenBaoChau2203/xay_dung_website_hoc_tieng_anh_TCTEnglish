"""Check the configured MCP launch, discovery and a read-only Git request."""
import asyncio
import tomllib
from pathlib import Path

from mcp import ClientSession, StdioServerParameters
from mcp.client.stdio import stdio_client

ROOT = Path(__file__).resolve().parent.parent


async def check():
    config = tomllib.loads((ROOT / ".codex/config.toml").read_text(encoding="utf-8"))
    server = config["mcp_servers"]["tct_git"]
    allowed = set(server["enabled_tools"])
    params = StdioServerParameters(command=server["command"], args=server["args"], cwd=str(ROOT / "TCTEnglish"))
    async with stdio_client(params) as (reader, writer):
        async with ClientSession(reader, writer) as session:
            await session.initialize()
            discovered = await session.list_tools()
            names = {tool.name for tool in discovered.tools}
            if names != allowed:
                raise RuntimeError(f"Git MCP must expose exactly seven reads: {sorted(names)}")
            # The server removes write handlers, independently of host allowlists.
            status = await session.call_tool("git_status", {"repo_path": str(ROOT)})
            if status.is_error:
                raise RuntimeError("git_status returned an MCP error")
            # Empty files would also fail upstream validation: even an accidental
            # dispatch cannot stage anything while testing the negative path.
            try:
                rejected = await session.call_tool("git_add", {"repo_path": str(ROOT), "files": []})
            except Exception as error:
                if "Unknown tool" not in str(error):
                    raise
            else:
                text = " ".join(block.text for block in rejected.content if block.type == "text")
                if not rejected.is_error or "Unknown tool" not in text:
                    raise RuntimeError("A direct call to a removed write tool was not rejected")
            print(f"MCP initialize/list_tools/git_status passed; {len(allowed)} configured read tools discovered.")
            print("Unadvertised git_add direct call rejected by server: PASS")


if __name__ == "__main__":
    asyncio.run(asyncio.wait_for(check(), timeout=30))
