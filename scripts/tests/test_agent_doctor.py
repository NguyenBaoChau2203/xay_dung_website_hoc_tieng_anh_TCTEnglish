"""Read-only diagnostics must not install, write, or disclose personal MCP data."""
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from agent_clients import hook_config, mcp_server

SPEC = importlib.util.spec_from_file_location('agent_doctor', Path(__file__).resolve().parents[1] / 'agent_doctor.py')
doctor = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(doctor)


class AgentDoctorTests(unittest.TestCase):
    def fixture(self, root):
        def write(name, value):
            path = root / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(value, encoding='utf-8')
        write('AGENTS.md', '# Core\nOne agent, explicit release authority.\n')
        write('CLAUDE.md', '@AGENTS.md\nRead `.ai/README.md` on demand.\n')
        write('.codex/config.toml', '[mcp_servers.codegraph]\ncommand="node"\nargs=' + json.dumps(mcp_server('codegraph')['args']))
        for name in ('.mcp.json', '.cursor/mcp.json', '.agents/mcp_config.json'):
            write(name, json.dumps({'mcpServers': {'codegraph': mcp_server('codegraph'), 'tct_git': mcp_server('git')}}))
        for name, host in (('.codex/hooks.json', 'codex'), ('.claude/settings.json', 'claude'), ('.cursor/hooks.json', 'cursor'), ('.agents/hooks.json', 'antigravity')):
            write(name, json.dumps(hook_config(host)))
        write('tooling/codegraph/package.json', json.dumps({'dependencies': {'@colbymchenry/codegraph': '1.6.2'}}))
        write('.codegraph/codegraph.db', 'DO_NOT_OPEN_OR_MODIFY_INDEX')
        return write

    def test_global_shadow_is_a_candidate_not_a_native_activity_claim(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp) / 'repo'; home = Path(tmp) / 'home'
            self.fixture(root)
            config = home / '.gemini/config/mcp_config.json'
            config.parent.mkdir(parents=True)
            config.write_text(json.dumps({'mcpServers': {'codegraph': {'command': 'codegraph', 'args': ['serve', '--mcp']}}}))
            report = doctor.build_report(root, home)
            antigravity = report['clients']['antigravity']
            self.assertTrue(antigravity['project_mcp_matches'])
            self.assertEqual('path-command', antigravity['user_config_candidates'][0]['codegraph_route'])
            self.assertEqual('unverified', antigravity['native_status'])

    def test_diagnostics_are_read_only_and_do_not_echo_credentials(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp) / 'repo'; home = Path(tmp) / 'home'
            self.fixture(root)
            config = home / '.cursor/mcp.json'; config.parent.mkdir(parents=True)
            secret = 'PRIVATE_DOCTOR_SENTINEL'
            config.write_text(json.dumps({'mcpServers': {'codegraph': {'command': secret, 'args': [secret], 'env': {'TOKEN': secret}}}}))
            before = {str(p): p.read_bytes() for p in Path(tmp).rglob('*') if p.is_file()}
            with mock.patch.object(subprocess, 'run', side_effect=AssertionError('No process execution in doctor')):
                report = doctor.build_report(root, home)
            self.assertNotIn(secret, json.dumps(report))
            self.assertEqual(before, {str(p): p.read_bytes() for p in Path(tmp).rglob('*') if p.is_file()})

    def test_claude_import_footprint_counts_only_real_imports(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); write = self.fixture(root)
            write('.ai/README.md', 'LONG_ROUTE' * 100)
            report = doctor.build_report(root, root / 'empty-home')
            self.assertEqual(['AGENTS.md'], report['instructions']['claude_imports'])
            self.assertEqual((root / 'CLAUDE.md').stat().st_size + (root / 'AGENTS.md').stat().st_size,
                             report['instructions']['claude_startup_bytes'])

    def test_malformed_personal_config_is_reported_without_content(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp) / 'repo'; home = Path(tmp) / 'home'
            self.fixture(root)
            config = home / '.gemini/config/mcp_config.json'; config.parent.mkdir(parents=True)
            config.write_text('{"PRIVATE_SENTINEL": broken}')
            report = doctor.build_report(root, home)
            value = report['clients']['antigravity']['user_config_candidates'][0]
            self.assertEqual('invalid', value['status'])
            self.assertNotIn('PRIVATE_SENTINEL', json.dumps(report))
