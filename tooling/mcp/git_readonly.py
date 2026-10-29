"""Expose only the seven reviewed Git reads, regardless of the MCP client."""
import asyncio
from pathlib import Path

from mcp_server_git.server import create_server

ROOT = Path(__file__).resolve().parents[2]
READ_TOOLS = {"git_status", "git_diff_unstaged", "git_diff_staged", "git_diff", "git_log", "git_show", "git_branch"}


async def serve():
    server = create_server(ROOT)
    for tool in await server.list_tools():
        if tool.name not in READ_TOOLS:
            server.remove_tool(tool.name)
    names = {tool.name for tool in await server.list_tools()}
    if names != READ_TOOLS:
        raise RuntimeError("Pinned Git MCP tool set differs from the reviewed reads")
    # Upstream validates unknown tool calls against this reduced registry before
    # dispatch/argument processing, so a manually crafted git_add is rejected too.
    await server.run_stdio_async()


if __name__ == "__main__":
    asyncio.run(serve())
