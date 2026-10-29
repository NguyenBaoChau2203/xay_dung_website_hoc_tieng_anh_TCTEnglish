"""Check CodeGraph pins and project-only MCP exposure without indexing."""
import copy
import sqlite3
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import agent_tooling as tooling
from agent_clients import mcp_server


class SqliteFingerprintTests(unittest.TestCase):
    def test_readonly_connection_can_create_empty_wal_without_data_change(self):
        with tempfile.TemporaryDirectory(prefix="tct wal spaces ") as directory:
            database = Path(directory) / "sample.db"
            writer = sqlite3.connect(database)
            writer.execute("PRAGMA journal_mode=WAL")
            writer.execute("CREATE TABLE sample(value TEXT)")
            writer.execute("INSERT INTO sample VALUES ('unchanged')")
            writer.commit()
            writer.close()
            before = tooling.sqlite_data_fingerprint(database)
            reader = sqlite3.connect(database.as_uri() + "?mode=ro", uri=True)
            try:
                self.assertEqual("unchanged", reader.execute("SELECT value FROM sample").fetchone()[0])
                self.assertEqual(before, tooling.sqlite_data_fingerprint(database))
            finally:
                reader.close()
            self.assertEqual(before, tooling.sqlite_data_fingerprint(database))

    def test_nonempty_wal_change_is_detected(self):
        with tempfile.TemporaryDirectory() as directory:
            database = Path(directory) / "sample.db"
            database.write_bytes(b"main database")
            wal = database.with_name(database.name + "-wal")
            wal.write_bytes(b"first committed frames")
            before = tooling.sqlite_data_fingerprint(database)
            wal.write_bytes(b"different committed frames")
            self.assertNotEqual(before, tooling.sqlite_data_fingerprint(database))

    def test_main_database_change_is_detected(self):
        with tempfile.TemporaryDirectory() as directory:
            database = Path(directory) / "sample.db"
            database.write_bytes(b"original")
            before = tooling.sqlite_data_fingerprint(database)
            database.write_bytes(b"changed")
            self.assertNotEqual(before, tooling.sqlite_data_fingerprint(database))


class CodeGraphConfigurationTests(unittest.TestCase):
    def setUp(self):
        self.server = {
            **mcp_server("codegraph"),
            "enabled_tools": ["codegraph_explore"],
            "env": {"CODEGRAPH_TELEMETRY": "0", "DO_NOT_TRACK": "1", "CODEGRAPH_NO_DOWNLOAD": "1", "CODEGRAPH_NO_UPDATE_CHECK": "1"},
        }
        self.manifest = {"dependencies": {"@colbymchenry/codegraph": "1.6.2"}}
        self.lock = {"packages": {
            "": copy.deepcopy(self.manifest),
            "node_modules/@colbymchenry/codegraph": {"version": "1.6.2", "integrity": "sha512-" + "a" * 86 + "=="},
        }}

    def check(self):
        return tooling.check_codegraph_config(self.server, self.manifest, self.lock)

    def test_exact_package_and_narrow_mcp_config_accepted(self):
        self.assertEqual([], self.check())

    def test_mutable_or_mismatched_pin_rejected(self):
        self.manifest["dependencies"]["@colbymchenry/codegraph"] = "^1.6.2"
        self.assertTrue(self.check())
        self.manifest["dependencies"]["@colbymchenry/codegraph"] = "1.6.2"
        self.lock["packages"]["node_modules/@colbymchenry/codegraph"]["version"] = "1.6.0"
        self.assertTrue(self.check())

    def test_missing_integrity_rejected(self):
        del self.lock["packages"]["node_modules/@colbymchenry/codegraph"]["integrity"]
        self.assertTrue(self.check())

    def test_standard_auto_sync_server_or_extra_tools_rejected(self):
        self.server["args"] = ["serve", "--mcp", "--no-watch"]
        self.assertTrue(self.check())
        self.server["args"] = mcp_server("codegraph")["args"]
        self.server["enabled_tools"].append("codegraph_status")
        self.assertTrue(self.check())

    def test_opportunistic_network_fallback_not_allowed(self):
        self.server["env"]["CODEGRAPH_NO_DOWNLOAD"] = "0"
        self.assertTrue(self.check())


if __name__ == "__main__":
    unittest.main()
