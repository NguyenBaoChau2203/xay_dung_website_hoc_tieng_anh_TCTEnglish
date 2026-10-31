"""SA02 + LB01: actual PowerShell bootstrap identity guards and SQL literal encoding."""

from __future__ import annotations

import json
import os
import re
import shutil
import subprocess
import tempfile
import textwrap
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / 'scripts' / 'bootstrap-mariadb-acceptance.ps1'


def mariadb_escape_literal(value: str) -> str:
    """MariaDB default string-literal escaping (no NO_BACKSLASH_ESCAPES): \\ then '."""
    return value.replace('\\', '\\\\').replace("'", "''")


def extract_identified_by_literals(sql: str) -> list[str]:
    # MariaDB literals escape quote as '' and backslash as \\ (default sql_mode).
    return re.findall(r"IDENTIFIED BY '((?:''|\\.|[^'\\])*)'", sql, flags=re.IGNORECASE)


@unittest.skipUnless(os.name == 'nt' and shutil.which('powershell.exe'), 'Windows PowerShell 5.1 host check')
class BootstrapMariadbAcceptanceScriptTests(unittest.TestCase):
    def run_bootstrap(
        self,
        bin_dir: Path,
        *,
        run_id: str,
        host_port: int = 3310,
        container: str = 'tctenglish-mariadb-acceptance-sa02',
        user_password: str = 'local-only-acc-pass',
    ) -> subprocess.CompletedProcess[str]:
        env = os.environ.copy()
        env['PATH'] = str(bin_dir) + os.pathsep + env.get('PATH', '')
        # Prefer explicit -UserPassword so backslash cases are not rewritten by the shell env.
        if 'TCT_ACCEPTANCE_DB_PASSWORD' in env:
            del env['TCT_ACCEPTANCE_DB_PASSWORD']
        return subprocess.run(
            [
                'powershell.exe',
                '-NoProfile',
                '-NonInteractive',
                '-ExecutionPolicy', 'Bypass',
                '-File',
                str(SCRIPT),
                '-ContainerName', container,
                '-Database', f'tct_acceptance_{run_id}',
                '-RunId', run_id,
                '-RootPassword', 'unused-local-only',
                '-HostPort', str(host_port),
                '-UserPassword', user_password,
            ],
            capture_output=True,
            text=True,
            env=env,
            cwd=str(ROOT),
        )

    def write_fake_docker(
        self,
        directory: Path,
        *,
        labels: dict,
        host_ip: str,
        host_port: int,
        sql_log: Path,
        inspect_exit: int = 0,
        exec_exit: int = 0,
        schema_exists: bool = False,
        sentinel_purpose: str = 'final-acceptance-g03',
        sentinel_run: str | None = None,
    ) -> None:
        labels_path = directory / 'labels.json'
        labels_path.write_text(json.dumps(labels, separators=(',', ':')), encoding='utf-8')
        ports_path = directory / 'ports.json'
        ports_path.write_text(
            json.dumps({'3306/tcp': [{'HostIp': host_ip, 'HostPort': str(host_port)}]}, separators=(',', ':')),
            encoding='utf-8',
        )
        inv_path = directory / 'invocations.log'
        schema_name = f"tct_acceptance_{(sentinel_run or labels.get('tct.run') or 'x')}"
        # First exec is schema probe (SELECT SCHEMA_NAME...); later may be identity/create.
        state_path = directory / 'exec_count.txt'
        state_path.write_text('0', encoding='utf-8')
        sentinel_run = sentinel_run or labels.get('tct.run') or 'x'

        bat = directory / 'docker.bat'
        bat.write_text(
            textwrap.dedent(
                f'''
                @echo off
                setlocal EnableExtensions EnableDelayedExpansion
                echo %*>> "{inv_path}"
                echo %*| findstr /I /C:"inspect" >nul
                if not errorlevel 1 (
                  if not "{inspect_exit}"=="0" exit /b {inspect_exit}
                  echo %*| findstr /I /C:"NetworkSettings.Ports" >nul
                  if not errorlevel 1 (
                    type "{ports_path}"
                    exit /b 0
                  )
                  type "{labels_path}"
                  exit /b 0
                )
                echo %*| findstr /I /C:"exec" >nul
                if not errorlevel 1 (
                  set /p COUNT=<"{state_path}"
                  set /a COUNT=!COUNT!+1
                  >"{state_path}" echo !COUNT!
                  more >> "{sql_log}"
                  rem Use process-level exit so Start-Process sees non-zero from .bat shims.
                  if not "{exec_exit}"=="0" exit {exec_exit}
                  if "!COUNT!"=="1" (
                    if "{str(schema_exists).lower()}"=="true" (
                      echo {schema_name}
                    )
                    exit /b 0
                  )
                  if "{str(schema_exists).lower()}"=="true" (
                    echo {sentinel_purpose}	{sentinel_run}
                  )
                  exit /b 0
                )
                exit /b 1
                '''
            ).lstrip(),
            encoding='utf-8',
        )

    def test_rejects_foreign_run_label_without_sql_write(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            sql_log = root / 'sql.txt'
            labels = {
                'tct.acceptance': '1',
                'tct.purpose': 'final-acceptance-g03',
                'tct.run': 'foreign_run',
            }
            self.write_fake_docker(root, labels=labels, host_ip='127.0.0.1', host_port=3310, sql_log=sql_log)
            result = self.run_bootstrap(root, run_id='new_run', host_port=3310)
            combined = result.stdout + result.stderr
            self.assertNotEqual(result.returncode, 0, combined)
            self.assertNotIn('Bootstrapped', result.stdout)
            self.assertFalse(sql_log.exists())

    def test_rejects_run_label_mismatch_exact(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            sql_log = root / 'sql.txt'
            labels = {
                'tct.acceptance': '1',
                'tct.purpose': 'final-acceptance-g03',
                'tct.run': 'other_run',
            }
            self.write_fake_docker(root, labels=labels, host_ip='127.0.0.1', host_port=3311, sql_log=sql_log)
            result = self.run_bootstrap(root, run_id='s01_run', host_port=3311)
            self.assertNotEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertFalse(sql_log.exists())

    def test_rejects_port_binding_mismatch(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            sql_log = root / 'sql.txt'
            labels = {
                'tct.acceptance': '1',
                'tct.purpose': 'final-acceptance-g03',
                'tct.run': 'port_run',
            }
            self.write_fake_docker(root, labels=labels, host_ip='127.0.0.1', host_port=3390, sql_log=sql_log)
            result = self.run_bootstrap(root, run_id='port_run', host_port=3312)
            self.assertNotEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertNotIn('Bootstrapped', result.stdout)
            self.assertFalse(sql_log.exists())

    def test_rejects_native_docker_exec_failure(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            sql_log = root / 'sql.txt'
            labels = {
                'tct.acceptance': '1',
                'tct.purpose': 'final-acceptance-g03',
                'tct.run': 'exec_run',
            }
            self.write_fake_docker(
                root,
                labels=labels,
                host_ip='127.0.0.1',
                host_port=3313,
                sql_log=sql_log,
                exec_exit=42,
            )
            result = self.run_bootstrap(root, run_id='exec_run', host_port=3313)
            self.assertNotEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertNotIn('Bootstrapped', result.stdout)

    def test_rejects_missing_acceptance_label(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            sql_log = root / 'sql.txt'
            labels = {
                'tct.purpose': 'final-acceptance-g03',
                'tct.run': 'miss_run',
            }
            self.write_fake_docker(root, labels=labels, host_ip='127.0.0.1', host_port=3314, sql_log=sql_log)
            result = self.run_bootstrap(root, run_id='miss_run', host_port=3314)
            self.assertNotEqual(result.returncode, 0, result.stdout + result.stderr)

    def test_rejects_existing_db_foreign_sentinel_without_drop(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            sql_log = root / 'sql.txt'
            labels = {
                'tct.acceptance': '1',
                'tct.purpose': 'final-acceptance-g03',
                'tct.run': 'same_run',
            }
            self.write_fake_docker(
                root,
                labels=labels,
                host_ip='127.0.0.1',
                host_port=3315,
                sql_log=sql_log,
                schema_exists=True,
                sentinel_purpose='final-acceptance-g03',
                sentinel_run='foreign_data_run',
            )
            result = self.run_bootstrap(root, run_id='same_run', host_port=3315)
            self.assertNotEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertNotIn('Bootstrapped', result.stdout)
            if sql_log.exists():
                sql = sql_log.read_text(encoding='utf-8', errors='ignore').upper()
                self.assertNotIn('DROP TABLE', sql)
                self.assertNotIn('INSERT INTO `__TCT_ACCEPTANCE_SENTINEL`', sql.replace('`', ''))

    def _bootstrap_new_db_sql(self, *, run_id: str, host_port: int, user_password: str) -> str:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            sql_log = root / 'sql.txt'
            labels = {
                'tct.acceptance': '1',
                'tct.purpose': 'final-acceptance-g03',
                'tct.run': run_id,
            }
            self.write_fake_docker(
                root,
                labels=labels,
                host_ip='127.0.0.1',
                host_port=host_port,
                sql_log=sql_log,
                schema_exists=False,
            )
            result = self.run_bootstrap(
                root,
                run_id=run_id,
                host_port=host_port,
                user_password=user_password,
            )
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertIn('Bootstrapped', result.stdout)
            self.assertTrue(sql_log.exists(), 'create path must write SQL through docker exec')
            # Do not echo password/SQL into unittest stdout.
            return sql_log.read_text(encoding='utf-8', errors='surrogateescape')

    def test_password_backslash_n_keeps_literal_bytes_in_identified_by(self):
        # Synthetic LB01 case: Aa + backslash + n + Bb. Expected SQL escape doubles backslash.
        password = 'Aa\\nBb'
        sql = self._bootstrap_new_db_sql(run_id='pwd_bsn', host_port=3316, user_password=password)
        literals = extract_identified_by_literals(sql)
        self.assertGreaterEqual(len(literals), 1, 'IDENTIFIED BY missing from generated SQL')
        expected = mariadb_escape_literal(password)
        self.assertEqual(literals[0], expected)
        # Bug shape before fix: single backslash left in SQL becomes newline under default sql_mode.
        self.assertNotEqual(literals[0], password)
        self.assertIn('\\\\n', literals[0])

    def test_each_sql_session_establishes_backslash_contract(self):
        sql = self._bootstrap_new_db_sql(run_id='pwd_mode', host_port=3316, user_password=r'Aa\nBb')
        self.assertIn('SET SESSION sql_mode', sql)
        self.assertIn('NO_BACKSLASH_ESCAPES', sql)

    def test_password_quote_and_backslash_quote_escape_in_identified_by(self):
        cases = {
            "pwd_quote": ("O'Brien", 3317),
            "pwd_bs_q": ("a\\b'c", 3318),
            "pwd_ws": ("pass word", 3319),
            "pwd_uni": ("päss✓", 3320),
        }
        for run_id, (password, port) in cases.items():
            with self.subTest(run_id=run_id):
                sql = self._bootstrap_new_db_sql(run_id=run_id, host_port=port, user_password=password)
                literals = extract_identified_by_literals(sql)
                self.assertGreaterEqual(len(literals), 1)
                self.assertEqual(literals[0], mariadb_escape_literal(password))


if __name__ == '__main__':
    unittest.main()
