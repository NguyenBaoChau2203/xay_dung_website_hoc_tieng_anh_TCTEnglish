"""Read-only, credential-free inventory for the four project agent clients.

Never starts a client/server, installs dependencies, opens the index, changes
trust, or treats configuration files as evidence of native invocation.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path
import re
import tomllib

from agent_clients import ROOT, hook_config, mcp_server

CLIENTS = {
    'codex': ('.codex/config.toml', '.codex/hooks.json', ['.codex/config.toml']),
    'cursor': ('.cursor/mcp.json', '.cursor/hooks.json', ['.cursor/mcp.json']),
    'claude': ('.mcp.json', '.claude/settings.json', ['.claude.json']),
    'antigravity': ('.agents/mcp_config.json', '.agents/hooks.json',
                    ['.gemini/config/mcp_config.json', '.gemini/antigravity/mcp_config.json']),
}


def read_config(path: Path) -> tuple[dict, str]:
    if not path.is_file():
        return {}, 'missing'
    try:
        value = path.read_text(encoding='utf-8-sig')
        data = tomllib.loads(value) if path.suffix == '.toml' else json.loads(value)
        if not isinstance(data, dict):
            return {}, 'invalid'
        return data, 'configured'
    except (OSError, ValueError):
        # Do not include parser excerpts, environment values or command arguments.
        return {}, 'invalid'


def servers(config: dict, host: str) -> dict:
    value = config.get('mcp_servers' if host == 'codex' else 'mcpServers', {})
    return value if isinstance(value, dict) else {}


def route(server: object) -> str:
    if not isinstance(server, dict):
        return 'absent'
    expected = mcp_server('codegraph')
    if server.get('command') == expected['command'] and server.get('args') == expected['args']:
        return 'project-launcher'
    if server.get('command') == 'codegraph':
        return 'path-command'
    return 'other-configured-route'


def project_matches(config: dict, host: str) -> bool:
    configured = servers(config, host)
    for name, kind in (('codegraph', 'codegraph'), ('tct_git', 'git')):
        actual = configured.get(name)
        expected = mcp_server(kind)
        if not isinstance(actual, dict) or any(actual.get(key) != value for key, value in expected.items()):
            return False
    return True


def skill_catalog(root: Path, directory: str) -> list[str]:
    result = []
    for file in sorted((root / directory).glob('*/SKILL.md')):
        if not file.resolve().is_relative_to(root):
            continue
        match = re.search(r'^name:\s*(.+)$', file.read_text(encoding='utf-8'), re.M)
        if match:
            result.append(match.group(1).strip().strip('\"\''))
    return result


def instruction_inventory(root: Path) -> dict:
    sizes = {name: (root / name).stat().st_size for name in ('AGENTS.md', 'CLAUDE.md', '.ai/README.md') if (root / name).is_file()}
    imported = []
    visited = set()

    def visit(name: str, depth: int = 0):
        file = (root / name).resolve()
        if depth > 4 or file in visited or not file.is_relative_to(root) or not file.is_file():
            return 0
        visited.add(file)
        total = file.stat().st_size
        for target in re.findall(r'^@([^\s]+)\s*$', file.read_text(encoding='utf-8'), re.M):
            linked = (file.parent / target).resolve()
            if linked.is_relative_to(root):
                relative = linked.relative_to(root).as_posix()
                if relative not in imported:
                    imported.append(relative)
                total += visit(relative, depth + 1)
        return total

    footprint = visit('CLAUDE.md')
    return {'file_bytes': sizes, 'claude_imports': imported,
            'claude_startup_bytes': footprint, 'measurement': 'UTF-8 bytes, not billed tokens'}


def build_report(root: Path = ROOT, home: Path | None = None) -> dict:
    root = root.resolve()
    home = (home or Path.home()).resolve()
    report = {'schema_version': 1, 'read_only': True, 'clients': {},
              'instructions': instruction_inventory(root)}
    for host, (mcp_file, hook_file, user_files) in CLIENTS.items():
        config, status = read_config(root / mcp_file)
        hooks, hook_status = read_config(root / hook_file)
        candidates = []
        for relative in user_files:
            user, user_status = read_config(home / relative)
            if user_status == 'missing':
                continue
            known = servers(user, host)
            # Server names/URLs/headers/env/arguments from personal configs are private.
            candidates.append({'path': str(home / relative), 'status': user_status,
                               'codegraph_route': route(known.get('codegraph')),
                               'tct_git_configured': 'tct_git' in known,
                               'other_server_count': len(set(known) - {'codegraph', 'tct_git'})})
        report['clients'][host] = {
            'project_mcp': mcp_file, 'project_config_status': status,
            'project_mcp_matches': project_matches(config, host),
            'project_hooks': hook_file,
            'project_hooks_match': hook_status == 'configured' and hooks == hook_config(host),
            'user_config_candidates': candidates, 'native_status': 'unverified',
        }
    canonical = skill_catalog(root, '.agents/skills')
    claude = skill_catalog(root, '.claude/skills')
    legacy = skill_catalog(root, '.agent/skills')
    report['skills'] = {'canonical_count': len(canonical), 'canonical_unique_count': len(set(canonical)),
                        'claude_adapter_count': len(claude), 'antigravity_legacy_adapter_count': len(legacy),
                        'cursor_overlap_candidates': sorted(set(canonical) & set(claude)),
                        'native_catalog_deduplication': 'unverified; do not delete adapters from this report alone'}
    manifest, _ = read_config(root / 'tooling/codegraph/package.json')
    installed, _ = read_config(root / 'tooling/codegraph/node_modules/@colbymchenry/codegraph/package.json')
    policy, policy_status = read_config(root / '.ai/codegraph.local.json')
    report['codegraph'] = {
        'pinned_version': manifest.get('dependencies', {}).get('@colbymchenry/codegraph'),
        'installed_project_version': installed.get('version'),
        'index_exists': (root / '.codegraph/codegraph.db').is_file(),
        'local_policy_status': policy_status,
        'requested_mode': policy.get('mode', 'read-only'),
        'policy_bound_to_checkout': policy.get('checkoutRoot') == str(root),
        'index_freshness': 'not queried; use the approved CodeGraph server/status',
    }
    return report


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--json', action='store_true', help='Emit sanitized JSON metadata')
    args = parser.parse_args()
    report = build_report()
    if args.json:
        print(json.dumps(report, indent=2, ensure_ascii=False))
    else:
        print('TCT agent doctor — read-only configuration inventory')
        for host, value in report['clients'].items():
            print(f"{host}: MCP={'matched' if value['project_mcp_matches'] else 'check needed'}, "
                  f"hooks={'matched' if value['project_hooks_match'] else 'check needed'}, native=unverified")
            for candidate in value['user_config_candidates']:
                if candidate['codegraph_route'] != 'absent':
                    print(f"  Personal config candidate: {candidate['codegraph_route']} ({candidate['path']})")
        print('Instructions (bytes, not tokens): ' + json.dumps(report['instructions']))
        print('Native activation/trust/catalog selection must be checked in the intended client.')
    return 0 if all(c['project_mcp_matches'] and c['project_hooks_match'] for c in report['clients'].values()) else 1


if __name__ == '__main__':
    raise SystemExit(main())
