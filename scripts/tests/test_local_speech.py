"""Lifecycle tests for scripts/local-speech.ps1 (Windows; fake process + HTTP)."""
from __future__ import annotations

import hashlib
import json
import os
import shutil
import socket
import subprocess
import sys
import tempfile
import threading
import time
import unittest
from datetime import datetime
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from unittest import mock

REPO = Path(__file__).resolve().parents[2]
SPEECH_PS1 = REPO / "scripts" / "local-speech.ps1"
START_LOCAL_PS1 = REPO / "scripts" / "start-local.ps1"
LOCK_JSON = REPO / "tooling" / "whisper" / "artifacts.lock.json"
FAKE_PY = REPO / "scripts" / "tests" / "fixtures" / "fake_whisper_server.py"


def _free_port() -> int:
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as sock:
        sock.bind(("127.0.0.1", 0))
        return int(sock.getsockname()[1])


def _find_pwsh() -> str:
    candidates: list[str] = []
    if os.environ.get("TCT_PWSH"):
        candidates.append(os.environ["TCT_PWSH"])
    candidates.extend(
        [
            r"C:\Program Files\PowerShell\7\pwsh.exe",
            r"C:\Program Files\PowerShell\7-preview\pwsh.exe",
            r"C:\Program Files\WindowsApps\Microsoft.PowerShell_7.6.9.0_x64__8wekyb3d8bbwe\pwsh.exe",
        ]
    )
    candidates.extend(["pwsh.exe", "pwsh"])
    for candidate in candidates:
        if not candidate:
            continue
        path = Path(candidate)
        try:
            if path.is_file() and path.stat().st_size > 0:
                return str(path)
        except OSError:
            continue
        found = shutil.which(candidate)
        if found:
            try:
                if Path(found).stat().st_size > 0:
                    return found
            except OSError:
                continue
    raise FileNotFoundError("pwsh not found; set TCT_PWSH to a real pwsh.exe (not the WindowsApps alias)")


def _pwsh(
    args: list[str],
    check: bool = True,
    timeout: int = 120,
    env: dict[str, str] | None = None,
) -> subprocess.CompletedProcess[str]:
    command = [_find_pwsh(), "-NoProfile", "-File", str(SPEECH_PS1), *args]
    merged = os.environ.copy()
    if env:
        merged.update(env)
    result = subprocess.run(
        command,
        cwd=str(REPO),
        text=True,
        capture_output=True,
        timeout=timeout,
        check=False,
        stdin=subprocess.DEVNULL,
        env=merged,
    )
    if check and result.returncode != 0:
        raise AssertionError(
            f"pwsh failed ({result.returncode})\nstdout:\n{result.stdout}\nstderr:\n{result.stderr}"
        )
    return result


def _last_json(stdout: str) -> dict:
    lines = [line.strip() for line in stdout.splitlines() if line.strip().startswith("{")]
    if not lines:
        raise AssertionError(f"No JSON object in stdout:\n{stdout}")
    return json.loads(lines[-1])


def _sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def _process_start_time_iso(pid: int) -> str:
    script = (
        f"(Get-Process -Id {pid}).StartTime.ToString('o')"
    )
    result = subprocess.run(
        [_find_pwsh(), "-NoProfile", "-Command", script],
        text=True,
        capture_output=True,
        check=False,
        stdin=subprocess.DEVNULL,
        timeout=30,
    )
    if result.returncode != 0 or not result.stdout.strip():
        raise AssertionError(f"unable to read start time for pid {pid}: {result.stderr}")
    return result.stdout.strip()


def _parse_start_time(value: str) -> datetime:
    text = (value or "").strip()
    if not text:
        raise ValueError("empty start time")
    # PowerShell 'o' may emit 7 fractional digits; fromisoformat accepts up to 6.
    if "." in text:
        head, rest = text.split(".", 1)
        digits = ""
        tz = ""
        for index, ch in enumerate(rest):
            if ch.isdigit():
                digits += ch
            else:
                tz = rest[index:]
                break
        digits = (digits + "000000")[:6]
        text = f"{head}.{digits}{tz}"
    return datetime.fromisoformat(text)


def _paths_equal(left: str, right: str) -> bool:
    try:
        return Path(left).resolve().as_posix().lower() == Path(right).resolve().as_posix().lower()
    except OSError:
        return left.lower() == right.lower()


def _pid_alive(pid: int) -> bool:
    listed = subprocess.run(
        ["tasklist", "/FI", f"PID eq {pid}"],
        capture_output=True,
        text=True,
        check=False,
    )
    return str(pid) in listed.stdout and "No tasks" not in listed.stdout


@unittest.skipUnless(sys.platform.startswith("win"), "local-speech lifecycle is Windows-focused")
@unittest.skipUnless(SPEECH_PS1.is_file(), "local-speech.ps1 missing")
@unittest.skipUnless(FAKE_PY.is_file(), "fake whisper fixture missing")
class LocalSpeechLifecycleTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.python = sys.executable
        cls.lock = json.loads(LOCK_JSON.read_text(encoding="utf-8"))

    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory(prefix="tct-speech-")
        self.speech_root = Path(self.temp.name) / "speech"
        self.speech_root.mkdir(parents=True, exist_ok=True)
        self.model = self.speech_root / "dummy-model.bin"
        self.model.write_bytes(b"dummy-model")
        self.port = _free_port()
        # Independent fixture registry — never trust state.json for kill authority (WH02).
        self._fixture_procs: list[dict[str, object]] = []
        self._extra_victims: list[subprocess.Popen[str]] = []

    def _register_fixture(self, pid: int, path: str, start_time: str | None = None) -> None:
        self._fixture_procs.append(
            {
                "pid": int(pid),
                "path": str(Path(path).resolve()),
                "start_time": start_time or _process_start_time_iso(int(pid)),
            }
        )

    def _kill_registered(self, entry: dict[str, object]) -> None:
        pid = int(entry["pid"])
        expected_path = str(entry["path"])
        expected_start_raw = str(entry.get("start_time") or "").strip()
        # Fail closed: missing registry creation time must never authorize kill (WR01).
        if not expected_start_raw:
            return
        try:
            expected_start = _parse_start_time(expected_start_raw)
        except ValueError:
            return
        if not _pid_alive(pid):
            return
        # Confirm OS image path + creation time still match the recorded fixture identity.
        probe = subprocess.run(
            [
                _find_pwsh(),
                "-NoProfile",
                "-Command",
                (
                    f"$p=Get-Process -Id {pid} -EA SilentlyContinue; "
                    "if(-not $p){ return }; "
                    "[pscustomobject]@{ Path=$p.Path; Start=$p.StartTime.ToString('o') } "
                    "| ConvertTo-Json -Compress"
                ),
            ],
            text=True,
            capture_output=True,
            check=False,
            stdin=subprocess.DEVNULL,
            timeout=30,
        )
        raw = (probe.stdout or "").strip()
        if not raw:
            return
        try:
            identity = json.loads(raw)
        except json.JSONDecodeError:
            return
        actual_path = str(identity.get("Path") or "").strip()
        actual_start_raw = str(identity.get("Start") or "").strip()
        if not actual_path or not actual_start_raw:
            return
        if not _paths_equal(actual_path, expected_path):
            return
        try:
            actual_start = _parse_start_time(actual_start_raw)
        except ValueError:
            return
        # Match ownership helper: allow 2s skew between registry and OS start times.
        if abs((actual_start - expected_start).total_seconds()) > 2:
            return
        subprocess.run(
            ["taskkill", "/PID", str(pid), "/F", "/T"],
            capture_output=True,
            check=False,
            timeout=15,
        )

    def tearDown(self) -> None:
        errors: list[str] = []
        try:
            for victim in self._extra_victims:
                if victim.poll() is None:
                    victim.kill()
                    try:
                        victim.wait(timeout=10)
                    except Exception as exc:  # noqa: BLE001
                        errors.append(str(exc))
            for entry in list(self._fixture_procs):
                try:
                    self._kill_registered(entry)
                except Exception as exc:  # noqa: BLE001
                    errors.append(str(exc))
            # Best-effort owned Stop using helper; never read state PID for taskkill.
            _pwsh(
                [
                    "-Action",
                    "Stop",
                    "-SpeechRoot",
                    str(self.speech_root),
                    "-Port",
                    str(self.port),
                    "-ExecutableOverride",
                    self.python,
                    "-ModelOverride",
                    str(self.model),
                    "-RuntimeFileName",
                    self.python,
                    "-RuntimePrefixArgs",
                    str(FAKE_PY),
                ],
                check=False,
                timeout=30,
            )
        finally:
            self.temp.cleanup()
        if errors:
            self.fail("teardown errors: " + "; ".join(errors))

    def _common_args(self) -> list[str]:
        return [
            "-SpeechRoot",
            str(self.speech_root),
            "-Port",
            str(self.port),
            "-ExecutableOverride",
            self.python,
            "-ModelOverride",
            str(self.model),
            "-RuntimeFileName",
            self.python,
            "-RuntimePrefixArgs",
            str(FAKE_PY),
        ]

    def _start(self, check: bool = True, env: dict[str, str] | None = None) -> dict:
        result = _pwsh(
            [
                "-Action",
                "Start",
                *self._common_args(),
                "-ReadinessDeadlineSeconds",
                "30",
            ],
            check=check,
            timeout=90,
            env=env,
        )
        if result.returncode != 0:
            return {"_failed": True, "stdout": result.stdout, "stderr": result.stderr, "code": result.returncode}
        payload = _last_json(result.stdout)
        if payload.get("Pid"):
            self._register_fixture(int(payload["Pid"]), self.python)
        return payload

    def test_lock_pins_b5454_and_tiny_en(self) -> None:
        self.assertEqual("b5454", self.lock["releaseTag"])
        self.assertEqual(
            "6ba69e3482d7826214f90a6a9c84ca07782aec1e1d0c6a7c30c994fd5d816ccb",
            self.lock["serverZip"]["sha256"],
        )
        self.assertEqual(
            "921e4cf8686fdd993dcd081a5da5b6c365bfde1162e72b08d75ac75289920b1f",
            self.lock["model"]["sha256"],
        )
        self.assertEqual(
            "a97456e747f28e7bb09c1dac35f40d94404c5ecffa74aaaaa7a3753ea6543121",
            self.lock["executable"]["sha256"],
        )
        self.assertGreaterEqual(len(self.lock.get("cpuBackendDlls", [])), 9)
        self.assertEqual(8092, self.lock["runtimeDefaults"]["port"])
        self.assertEqual(2, self.lock["runtimeDefaults"]["threads"])

    def test_hash_mismatch_refuses_execute_without_override(self) -> None:
        bin_dir = self.speech_root / "artifacts" / "bin" / "Release"
        bin_dir.mkdir(parents=True, exist_ok=True)
        (bin_dir / "whisper-server.exe").write_bytes(b"not-the-real-binary")
        model = self.speech_root / "artifacts" / "ggml-tiny.en.bin"
        model.parent.mkdir(parents=True, exist_ok=True)
        model.write_bytes(b"bad-model")
        result = _pwsh(
            [
                "-Action",
                "Start",
                "-SpeechRoot",
                str(self.speech_root),
                "-Port",
                str(self.port),
                "-Offline",
                "-SkipLegacyCache",
                "-ReadinessDeadlineSeconds",
                "5",
            ],
            check=False,
            timeout=60,
        )
        self.assertNotEqual(0, result.returncode)
        combined = (result.stdout + result.stderr).lower()
        self.assertTrue("hash" in combined or "missing" in combined or "offline" in combined)

    def test_start_status_stop_ownership(self) -> None:
        started = self._start()
        self.assertEqual("ready", started["Status"])
        self.assertTrue(started["Pid"] > 0)
        time.sleep(0.5)
        self.assertTrue(_pid_alive(int(started["Pid"])))

        status = _last_json(_pwsh(["-Action", "Status", *self._common_args()]).stdout)
        self.assertEqual("ready", status["Status"])
        self.assertEqual(started["Pid"], status["Pid"])

        stopped = _last_json(_pwsh(["-Action", "Stop", *self._common_args()]).stdout)
        self.assertEqual("stopped", stopped["Status"])
        self.assertTrue(stopped["Stopped"])

    def test_concurrent_start_reuses_one_process(self) -> None:
        first = self._start()
        self.assertEqual("ready", first["Status"])

        barrier = threading.Barrier(2)
        results: list[dict] = []
        errors: list[str] = []

        def worker() -> None:
            try:
                barrier.wait(timeout=30)
                results.append(self._start())
            except Exception as exc:  # noqa: BLE001
                errors.append(str(exc))

        threads = [threading.Thread(target=worker) for _ in range(2)]
        for thread in threads:
            thread.start()
        for thread in threads:
            thread.join(timeout=90)
        self.assertEqual([], errors)
        self.assertEqual(2, len(results))
        self.assertEqual(first["Pid"], results[0]["Pid"])
        self.assertEqual(results[0]["Pid"], results[1]["Pid"])
        self.assertTrue(all(item.get("Status") == "ready" for item in results))
        self.assertTrue(any(item.get("Reused") for item in results))

    def test_foreign_port_occupant_is_not_killed(self) -> None:
        class Handler(BaseHTTPRequestHandler):
            def do_GET(self):  # noqa: N802
                self.send_response(200)
                self.end_headers()

            def log_message(self, format, *args):  # noqa: A003
                return

        server = ThreadingHTTPServer(("127.0.0.1", self.port), Handler)
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        try:
            failed = self._start(check=False)
            self.assertTrue(failed.get("_failed"))
            combined = (failed.get("stdout", "") + failed.get("stderr", "")).lower()
            self.assertIn("refusing", combined)
            with socket.create_connection(("127.0.0.1", self.port), timeout=1):
                pass
        finally:
            server.shutdown()
            server.server_close()

    def test_stop_refuses_wrong_executable_even_when_state_cmdline_lies(self) -> None:
        # WH01: OS path is python victim; expected runtime is a ghost whisper path;
        # state.commandLine pretends to be whisper. Must refuse Stop; victim stays alive.
        ghost = self.speech_root / "ghost-whisper-server.exe"
        ghost.write_bytes(b"ghost-exe-for-ownership-test")
        ghost_hash = _sha256_file(ghost)
        victim = subprocess.Popen(
            [self.python, "-c", "import time; time.sleep(300)"],
            stdin=subprocess.DEVNULL,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        self._extra_victims.append(victim)
        time.sleep(0.3)
        start_iso = _process_start_time_iso(victim.pid)
        state = {
            "status": "ready",
            "runId": "ownership-wh01",
            "pid": victim.pid,
            "startTimeLocal": start_iso,
            "checkoutRoot": str(REPO),
            "host": "127.0.0.1",
            "port": self.port,
            "executablePath": str(ghost),
            "executableHash": ghost_hash,
            "runtimeFileName": str(ghost),
            "modelPath": str(self.model),
            "modelHash": _sha256_file(self.model),
            "modelIdentity": "test",
            "commandLine": f"{ghost} --host 127.0.0.1 --port {self.port} -m {self.model.name}",
        }
        (self.speech_root / "state.json").write_text(json.dumps(state), encoding="utf-8")
        result = _pwsh(
            [
                "-Action",
                "Stop",
                "-SpeechRoot",
                str(self.speech_root),
                "-Port",
                str(self.port),
                "-ExecutableOverride",
                str(ghost),
                "-ModelOverride",
                str(self.model),
                "-RuntimeFileName",
                str(ghost),
            ],
            check=False,
        )
        self.assertNotEqual(0, result.returncode)
        self.assertIn("refusing stop", (result.stdout + result.stderr).lower())
        self.assertIsNone(victim.poll())

    def test_stop_refuses_when_state_points_at_unrelated_victim(self) -> None:
        started = self._start()
        fixture_pid = int(started["Pid"])
        victim = subprocess.Popen(
            [self.python, "-c", "import time; time.sleep(300)"],
            stdin=subprocess.DEVNULL,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        self._extra_victims.append(victim)
        time.sleep(0.3)
        state_path = self.speech_root / "state.json"
        state = json.loads(state_path.read_text(encoding="utf-8"))
        state["pid"] = victim.pid
        state["startTimeLocal"] = _process_start_time_iso(victim.pid)
        # Keep hashes, but runtime expected remains python fixture path while victim is also python —
        # force mismatch via impossible start skew already avoided; change executableHash instead.
        state["executableHash"] = "0" * 64
        state_path.write_text(json.dumps(state), encoding="utf-8")
        result = _pwsh(["-Action", "Stop", *self._common_args()], check=False)
        self.assertNotEqual(0, result.returncode)
        self.assertIn("refusing stop", (result.stdout + result.stderr).lower())
        self.assertIsNone(victim.poll())
        self.assertTrue(_pid_alive(fixture_pid))

    def test_teardown_registry_never_taskkills_state_pid_alone(self) -> None:
        # WH02 + WR01: full tearDown with mutated state.pid must not kill the foreign victim.
        started = self._start()
        fixture_pid = int(started["Pid"])
        victim = subprocess.Popen(
            [self.python, "-c", "import time; time.sleep(300)"],
            stdin=subprocess.DEVNULL,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        time.sleep(0.2)
        state_path = self.speech_root / "state.json"
        state = json.loads(state_path.read_text(encoding="utf-8"))
        state["pid"] = victim.pid
        # Corrupt start identity as well — Stop must refuse; teardown must not adopt state PID.
        state["startTimeLocal"] = "2000-01-01T00:00:00.0000000+07:00"
        state_path.write_text(json.dumps(state), encoding="utf-8")

        captured: list[list[str]] = []

        def fake_run(cmd, *args, **kwargs):  # noqa: ANN001
            if cmd and str(cmd[0]).lower().endswith("taskkill"):
                captured.append([str(x) for x in cmd])
                target = cmd[2] if len(cmd) > 2 else ""
                if target == str(victim.pid):
                    # Swallow only foreign-victim kills; fixture may still need real cleanup.
                    return subprocess.CompletedProcess(cmd, 0, "", "")
            return original_run(cmd, *args, **kwargs)

        original_run = subprocess.run
        with mock.patch("subprocess.run", side_effect=fake_run):
            # Intentionally invoke the real tearDown path (registry + owned Stop).
            # Keep victim out of _extra_victims so survival proves state.pid was not authoritative.
            self._extra_victims = []
            self.tearDown()
            self._fixture_procs = []
            self._extra_victims = []
            self.temp = tempfile.TemporaryDirectory(prefix="tct-speech-done-")

        victim_kill_cmds = [c for c in captured if len(c) > 2 and c[2] == str(victim.pid)]
        self.assertEqual([], victim_kill_cmds)
        self.assertIsNone(victim.poll())
        self.assertTrue(_pid_alive(victim.pid))
        # Registry cleanup (real taskkill) should have stopped the fixture server.
        self.assertFalse(_pid_alive(fixture_pid))
        victim.kill()
        victim.wait(timeout=10)

    def test_kill_registered_refuses_stale_start_time(self) -> None:
        # WR01: same executable path + live PID, but stale registry creation time → no kill.
        victim = subprocess.Popen(
            [self.python, "-c", "import time; time.sleep(300)"],
            stdin=subprocess.DEVNULL,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        self._extra_victims.append(victim)
        time.sleep(0.3)
        entry = {
            "pid": int(victim.pid),
            "path": str(Path(self.python).resolve()),
            "start_time": "2000-01-01T00:00:00.0000000+07:00",
        }
        captured: list[list[str]] = []

        def fake_run(cmd, *args, **kwargs):  # noqa: ANN001
            if cmd and str(cmd[0]).lower().endswith("taskkill"):
                captured.append([str(x) for x in cmd])
                return subprocess.CompletedProcess(cmd, 0, "", "")
            return original_run(cmd, *args, **kwargs)

        original_run = subprocess.run
        with mock.patch("subprocess.run", side_effect=fake_run):
            self._kill_registered(entry)
        self.assertEqual([], captured)
        self.assertIsNone(victim.poll())
        self.assertTrue(_pid_alive(victim.pid))

    def test_inference_probe_schema_table(self) -> None:
        # WR02: table-driven MapVerboseJson parity via ValidateProbeBody.
        cases: list[tuple[str, bool, str]] = [
            ('{"segments":[]}', False, "segments-only-empty"),
            ('{"text":"hello","segments":[42]}', False, "segment-primitive"),
            (
                '{"text":"hello","segments":[{"start":3,"end":1,"text":"hello"}]}',
                False,
                "end-before-start",
            ),
            ('[{"text":""}]', False, "root-array"),
            ('{"text":"hello","segments":[{"text":1}]}', False, "segment-text-number"),
            (
                '{"text":"hello","segments":[{"start":"0","end":1,"text":"x"}]}',
                False,
                "start-string",
            ),
            (
                '{"text":"hello","segments":[{"start":0,"end":1e309,"text":"x"}]}',
                False,
                "end-overflow",
            ),
            ('{"text":null}', False, "root-text-null"),
            ("null", False, "null-root"),
            ("<html>x</html>", False, "html"),
            ('{"error":"boom"}', False, "error-property"),
            ('{"text":""}', True, "valid-silence-text-only"),
            ('{"text":"","segments":[]}', True, "valid-silence"),
            (
                '{"segments":[{"text":"hi","start":0,"end":1}]}',
                True,
                "valid-segment-only",
            ),
            ('{"text":"hello"}', True, "valid-text-only"),
            (
                '{"text":"hello","segments":[{"start":0,"end":1}]}',
                True,
                "optional-segment-text",
            ),
        ]
        for body, expect_ok, label in cases:
            with self.subTest(label=label):
                probe_file = self.speech_root / f"probe-{label}.json"
                probe_file.write_text(body, encoding="utf-8")
                result = _pwsh(
                    [
                        "-Action",
                        "ValidateProbeBody",
                        "-SpeechRoot",
                        str(self.speech_root),
                        "-ProbeBodyFile",
                        str(probe_file),
                    ],
                    check=False,
                )
                self.assertEqual(0, result.returncode, msg=result.stdout + result.stderr)
                parsed = _last_json(result.stdout)
                self.assertEqual(expect_ok, bool(parsed.get("Ok")), msg=f"{label}: {parsed}")

    def test_segments_empty_http_mode_is_not_ready(self) -> None:
        # WR02 loopback: schema wrong outside `error` must not become ready.
        (self.speech_root / "tct-fake-mode.txt").write_text("segments-empty", encoding="utf-8")
        failed = self._start(check=False)
        self.assertTrue(failed.get("_failed"), msg=f"stdout={failed.get('stdout')} stderr={failed.get('stderr')}")
        combined = (failed.get("stdout", "") + failed.get("stderr", "")).lower()
        self.assertTrue(
            "timed out" in combined
            or "silence-requires-explicit-text" in combined
            or "reason=" in combined
            or "exited before readiness" in combined,
            msg=combined[-1000:],
        )

    def test_error_json_http200_is_not_ready(self) -> None:
        # WMI Create may not surface custom env; use side-car mode file next to model.
        (self.speech_root / "tct-fake-mode.txt").write_text("error", encoding="utf-8")
        failed = self._start(check=False)
        self.assertTrue(failed.get("_failed"), msg=f"stdout={failed.get('stdout')} stderr={failed.get('stderr')}")
        combined = (failed.get("stdout", "") + failed.get("stderr", "")).lower()
        self.assertTrue(
            "timed out" in combined
            or "reason=error-property" in combined
            or "exited before readiness" in combined
            or "reason=" in combined,
            msg=combined[-1000:],
        )

    def test_cpu_backend_tamper_rejects_without_override(self) -> None:
        # Copy a verified Release tree, then Verify (no auto-repair extract).
        src = REPO / ".appdata/local/speech/artifacts/bin/Release"
        if not src.is_dir():
            src = REPO / ".tmp/asr-acceptance-2930/bin-b5454/Release"
        if not src.is_dir():
            self.skipTest("verified whisper Release folder unavailable")
        dest = self.speech_root / "artifacts" / "bin" / "Release"
        dest.mkdir(parents=True, exist_ok=True)
        for item in src.iterdir():
            target = dest / item.name
            if item.is_file():
                shutil.copy2(item, target)
        model_src = REPO / ".appdata/local/speech/artifacts/ggml-tiny.en.bin"
        if not model_src.is_file():
            model_src = REPO / ".tmp/asr-acceptance-2930/ggml-tiny.en.bin"
        model_dest = self.speech_root / "artifacts" / "ggml-tiny.en.bin"
        shutil.copy2(model_src, model_dest)
        cpu = dest / "ggml-cpu-x64.dll"
        cpu.write_bytes(b"bad")
        result = _pwsh(
            [
                "-Action",
                "Verify",
                "-SpeechRoot",
                str(self.speech_root),
                "-Offline",
                "-SkipLegacyCache",
            ],
            check=False,
            timeout=60,
        )
        self.assertNotEqual(0, result.returncode)
        self.assertIn("cpu backend", (result.stdout + result.stderr).lower())


class StartLocalContractTests(unittest.TestCase):
    def test_start_local_exposes_skip_whisper_and_keeps_database_only(self) -> None:
        text = START_LOCAL_PS1.read_text(encoding="utf-8")
        self.assertIn("[switch]$SkipWhisper", text)
        self.assertIn("[switch]$DatabaseOnly", text)
        self.assertIn("local-speech.ps1", text)
        self.assertIn("Learning__Speech__WhisperHttpBaseUrl", text)
        db_idx = text.index("if ($DatabaseOnly)")
        whisper_ensure_idx = text.index("Starting local Whisper")
        self.assertLess(db_idx, whisper_ensure_idx)
        self.assertIn("No fake scores", text)
        # Busy-port refusal and success/failure environment restoration are exercised
        # through the actual launcher in test_local_launcher, not by matching copy/names.


if __name__ == "__main__":
    unittest.main()
