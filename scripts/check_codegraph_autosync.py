"""Real pinned MCP/SQLite fixture checks; never write application source or its index."""
import asyncio
import contextlib
import json
import os
import platform
import shutil
import sqlite3
import subprocess
import sys
import time
import uuid
from pathlib import Path

from mcp import ClientSession, StdioServerParameters
from mcp.client.stdio import stdio_client
from agent_tooling import sqlite_data_fingerprint

ROOT = Path(__file__).resolve().parent.parent
PLATFORM = "win32" if os.name == "nt" else sys.platform
ARCH = "arm64" if platform.machine().lower() in {"arm64", "aarch64"} else "x64"
BUNDLE = ROOT / "tooling/codegraph/node_modules/@colbymchenry" / f"codegraph-{PLATFORM}-{ARCH}"
RUNTIME = BUNDLE / ("node.exe" if os.name == "nt" else "node")


def run(args, cwd, log=None):
    result = subprocess.run([str(x) for x in args], cwd=cwd, capture_output=True, text=True,
                            encoding="utf-8", errors="replace", timeout=90)
    if log:
        log.write_text(result.stdout + result.stderr, encoding="utf-8")
    if result.returncode:
        raise RuntimeError(f"Fixture command failed ({result.returncode}): {result.stderr[-2000:]}")
    return result.stdout


class Fixture:
    def __init__(self):
        self.root = (ROOT / ".tmp/codegraph-auto-sync" / f"fixture-{uuid.uuid4().hex[:8]}").resolve()
        self.root.mkdir(parents=True)
        self.tools = self.root / "tooling/codegraph"
        self.tools.mkdir(parents=True)
        (self.root / ".ai").mkdir()
        for name in ("launch.cjs", "project-policy.cjs", "mcp-readonly.cjs", "mcp-autosync.cjs", "package.json"):
            shutil.copyfile(ROOT / "tooling/codegraph" / name, self.tools / name)
        modules = ROOT / "tooling/codegraph/node_modules"
        run(["node", "-e", "require('fs').symlinkSync(process.argv[1],process.argv[2],'junction')",
             modules, self.tools / "node_modules"], self.root)
        (self.root / ".gitignore").write_text(".codegraph/\ntooling/\n.ai/\n", encoding="utf-8")
        run(["git", "init", "--quiet"], self.root)
        self.source = self.root / "Fixture.cs"
        self.source.write_text("public class FixtureRoot { public int OriginalMethod() => 1; }\n", encoding="utf-8")
        self.launch = self.tools / "launch.cjs"
        self.db = self.root / ".codegraph/codegraph.db"
        self.policy = self.root / ".ai/codegraph.local.json"
        self.observations = []
        run(["node", self.launch, "init", self.root], self.root, self.root / "index.log")

    def count(self, name):
        with sqlite3.connect(self.db.as_uri() + "?mode=ro", uri=True, timeout=2) as db:
            return db.execute("SELECT COUNT(*) FROM nodes WHERE name = ?", (name,)).fetchone()[0]

    async def wait_count(self, name, wanted, timeout=20):
        started = time.monotonic()
        while time.monotonic() - started < timeout:
            if bool(self.count(name)) == wanted:
                return round((time.monotonic() - started) * 1000)
            await asyncio.sleep(.15)
        raise AssertionError(f"Index did not {'add' if wanted else 'remove'} {name}")

    def daemon_pid(self):
        file = self.root / ".codegraph/daemon.pid"
        if not file.exists():
            return None
        try:
            value = json.loads(file.read_text(encoding="utf-8"))
            return value.get("pid") if isinstance(value, dict) else int(value)
        except (ValueError, OSError):
            return None

    @contextlib.asynccontextmanager
    async def session(self, label, native_backend=False):
        env = dict(os.environ, CODEGRAPH_TELEMETRY="0", DO_NOT_TRACK="1", CODEGRAPH_NO_DOWNLOAD="1",
                   CODEGRAPH_NO_UPDATE_CHECK="1", CODEGRAPH_MCP_TOOLS="explore",
                   CODEGRAPH_DAEMON_IDLE_TIMEOUT_MS="1000", PYTHONUTF8="1")
        if native_backend:
            env["CODEGRAPH_MCP_TOOLS"] = "explore,node"
            env["CODEGRAPH_NO_DAEMON"] = "0"
        params = StdioServerParameters(
            command=str(RUNTIME) if native_backend else "node",
            args=["--liftoff-only", "--disable-warning=ExperimentalWarning", str(BUNDLE / "lib/dist/bin/codegraph.js"),
                  "serve", "--mcp", "--path", str(self.root)] if native_backend else [str(self.launch), "--mcp-project"],
            cwd=str(self.root), env=env)
        with (self.root / f"{label}.log").open("w", encoding="utf-8") as errlog:
            async with stdio_client(params, errlog=errlog) as (reader, writer):
                async with ClientSession(reader, writer) as session:
                    info = await session.initialize()
                    assert info.server_info.version == "1.6.2"
                    names = {t.name for t in (await session.list_tools()).tools}
                    assert len(names) > 1 if native_backend else names == {"codegraph_explore"}, names
                    yield session

    async def explore(self, session, symbol):
        result = await session.call_tool("codegraph_explore", {"query": symbol, "projectPath": str(self.root), "maxFiles": 1})
        text = "\n".join(x.text for x in result.content if x.type == "text")
        assert not result.is_error and symbol in text, text[:500]

    async def stop_fixture_daemon(self):
        # Upstream verifies socket identity; only this unique fixture root is targeted.
        assert self.root.parent == (ROOT / ".tmp/codegraph-auto-sync").resolve()
        module = BUNDLE / "lib/dist/mcp/daemon-registry.js"
        js = "require(process.argv[1]).stopDaemonAt(process.argv[2],{preserveUnverified:true}).then(x=>process.stdout.write(JSON.stringify(x))).catch(e=>{process.stderr.write(e.message);process.exitCode=1})"
        result = await asyncio.to_thread(run, [RUNTIME, "--liftoff-only", "--disable-warning=ExperimentalWarning", "-e", js, module, self.root], self.root)
        for _ in range(40):
            if self.daemon_pid() is None:
                return result
            await asyncio.sleep(.1)
        raise AssertionError("Fixture daemon did not release its identity lock")

    async def check(self):
        before = sqlite_data_fingerprint(self.db)
        async with self.session("default-readonly") as first:
            await self.explore(first, "OriginalMethod")
            (self.root / "ReadOnlyChange.cs").write_text("public class ReadOnlyChange {}\n", encoding="utf-8")
            await asyncio.sleep(.8)
            assert self.count("ReadOnlyChange") == 0
        assert before == sqlite_data_fingerprint(self.db)
        self.observations.append({"check": "default checkout stays read-only", "passed": True})
        self.policy.write_text(json.dumps({"schemaVersion": 1, "mode": "auto-sync", "checkoutRoot": str(self.root),
                                           "reviewedVersion": "1.6.2", "debounceMs": 1000}), encoding="utf-8")
        async with self.session("wide-backend", native_backend=True), self.session("auto-client-1") as first:
            for arguments in ({"name": "codegraph_sync", "arguments": {}},
                              {"name": "codegraph_explore", "arguments": {"query": "Probe", "projectPath": str(self.root.parent)}}):
                try:
                    await first.call_tool(arguments["name"], arguments["arguments"])
                except Exception as error:
                    assert "codegraph_explore" in str(error) or "approved checkout" in str(error), str(error)
                else:
                    raise AssertionError("MCP scope gate accepted a forbidden request")
            self.observations.append({"check": "unlisted tool and outside-checkout query rejected", "passed": True})
            await self.explore(first, "ReadOnlyChange")
            assert self.count("ReadOnlyChange") > 0  # Real catch-up extraction, not a fresh disk excerpt.
            first_pid = self.daemon_pid()
            assert first_pid, "Expected upstream shared daemon"
            async with self.session("auto-client-2") as second:
                await self.explore(second, "OriginalMethod")
                assert self.daemon_pid() == first_pid
                self.observations.append({"check": "two guarded clients share wider backend daemon; each exposes explore only", "pid": first_pid, "passed": True})
                self.source.write_text("public class FixtureRoot { public int EditedMethod() => 2; }\n", encoding="utf-8")
                latency = await self.wait_count("EditedMethod", True)
                assert self.count("OriginalMethod") == 0
                await self.explore(second, "EditedMethod")
                self.observations.append({"check": "edit updates real graph visible through other client", "latency_ms": latency, "passed": True})
                extra = self.root / "Extra.cs"
                extra.write_text("public class CreatedProbe { public int CreatedMethod() => 3; }\n", encoding="utf-8")
                await self.wait_count("CreatedMethod", True)
                await self.explore(first, "CreatedMethod")
                extra.unlink()
                await self.wait_count("CreatedMethod", False)
                self.observations.append({"check": "create/delete update real nodes", "passed": True})
                for number in range(10):
                    self.source.write_text(f"public class FixtureRoot {{ public int Burst{number}() => {number}; }}\n", encoding="utf-8")
                # While inside the configured debounce window, no intermediate graph is published.
                await asyncio.sleep(.2)
                assert not any(self.count(f"Burst{n}") for n in range(10))
                await self.wait_count("Burst9", True)
                assert not any(self.count(f"Burst{n}") for n in range(9))
                await self.explore(second, "Burst9")
                self.observations.append({"check": "burst of ten saves is debounced to final source", "passed": True})
                if os.name == "nt":
                    stats = await asyncio.to_thread(run, ["powershell", "-NoProfile", "-Command", f"Get-Process -Id {int(first_pid)} | Select-Object Id,CPU,WorkingSet64 | ConvertTo-Json -Compress"], self.root)
                    self.observations.append({"check": "observed shared daemon resources", "sample": json.loads(stats)})
        await self.stop_fixture_daemon()
        offline = self.root / "Offline.cs"
        offline.write_text("public class OfflineProbe { public int OfflineMethod() => 4; }\n", encoding="utf-8")
        assert self.count("OfflineMethod") == 0
        async with self.session("reconnect-catchup") as session:
            await self.explore(session, "OfflineMethod")
            assert self.count("OfflineMethod") > 0
        await self.stop_fixture_daemon()
        self.observations.append({"check": "reconnect catches changes made with updater stopped", "passed": True})
        self.policy.unlink()
        self.source.write_text("public class FixtureRoot { public int DisabledProbe() => 5; }\n", encoding="utf-8")
        before = sqlite_data_fingerprint(self.db)
        async with self.session("disabled-readonly") as session:
            await self.explore(session, "OfflineMethod")
            await asyncio.sleep(1.3)
            assert self.count("DisabledProbe") == 0
        assert before == sqlite_data_fingerprint(self.db)
        self.observations.append({"check": "removing opt-in restores read-only with unchanged DB/WAL", "passed": True})


async def main():
    fixture = await asyncio.to_thread(Fixture)
    print(f"Fixture: {fixture.root}", flush=True)
    try:
        await asyncio.wait_for(fixture.check(), timeout=150)
    finally:
        await fixture.stop_fixture_daemon()
        (fixture.root / "results.json").write_text(json.dumps(fixture.observations, indent=2), encoding="utf-8")
    print(json.dumps(fixture.observations, indent=2), flush=True)
    print("Auto-sync fixture checks: PASS; application source/index untouched", flush=True)


if __name__ == "__main__":
    asyncio.run(main())
