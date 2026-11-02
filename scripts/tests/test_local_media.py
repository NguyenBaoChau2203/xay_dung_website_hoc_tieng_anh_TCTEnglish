"""Pinned-media provisioning in disposable fixture roots; no system FFmpeg mutation."""
import hashlib
import json
import subprocess
import sys
import tempfile
import unittest
import zipfile
from pathlib import Path

from test_local_speech import _find_pwsh

REPO = Path(__file__).resolve().parents[2]


class LocalMediaTests(unittest.TestCase):
    def fixture(self, directory):
        root = Path(directory)
        archive = root / "fixture.zip"
        payloads = {"bin/ffmpeg.exe": b"fixture executable - never executed", "LICENSE": b"fixture license"}
        with zipfile.ZipFile(archive, "w") as bundle:
            for name, payload in payloads.items():
                bundle.writestr(name, payload)
            bundle.writestr("../../outside.exe", b"must not extract")
        pin = {
            "schemaVersion": 1, "platform": "win-x64", "version": "fixture",
            "archive": {"url": "https://fixture.invalid/never-download", "bytes": archive.stat().st_size, "sha256": hashlib.sha256(archive.read_bytes()).hexdigest()},
            "files": [{"entry": name, "name": "ffmpeg.exe" if name.endswith(".exe") else "LICENSE.ffmpeg.txt", "sha256": hashlib.sha256(payload).hexdigest()} for name, payload in payloads.items()],
        }
        lock = root / "pin.json"
        lock.write_text(json.dumps(pin), encoding="utf-8")
        return root / "media", archive, lock

    def invoke(self, media, archive, lock, action="Setup"):
        return subprocess.run([_find_pwsh(), "-NoProfile", "-File", str(REPO / "scripts/local-media.ps1"), "-Action", action, "-Offline", "-MediaRoot", str(media), "-ArchivePath", str(archive), "-LockPath", str(lock)], capture_output=True, text=True, timeout=20)

    @unittest.skipUnless(sys.platform.startswith('win'), 'Windows x64 media bootstrap; exercised by the Windows CI job')
    def test_cold_setup_repeat_verify_and_no_unlisted_zip_extraction(self):
        with tempfile.TemporaryDirectory(prefix="tct-media-") as directory:
            media, archive, lock = self.fixture(directory)
            for action in ("Setup", "Setup", "Verify"):
                result = self.invoke(media, archive, lock, action)
                self.assertEqual(0, result.returncode, result.stderr)
            self.assertEqual({"ffmpeg.exe", "LICENSE.ffmpeg.txt", "setup.lock"}, {p.name for p in media.iterdir()})
            self.assertFalse((Path(directory) / "outside.exe").exists())

    @unittest.skipUnless(sys.platform.startswith('win'), 'Windows x64 media bootstrap; exercised by the Windows CI job')
    def test_tampered_executable_refused_without_overwrite(self):
        with tempfile.TemporaryDirectory(prefix="tct-media-") as directory:
            media, archive, lock = self.fixture(directory)
            self.assertEqual(0, self.invoke(media, archive, lock).returncode)
            (media / "ffmpeg.exe").write_bytes(b"tampered")
            result = self.invoke(media, archive, lock)
            self.assertNotEqual(0, result.returncode)
            self.assertEqual(b"tampered", (media / "ffmpeg.exe").read_bytes())

    @unittest.skipUnless(sys.platform.startswith('win'), 'Windows x64 media bootstrap; exercised by the Windows CI job')
    def test_bad_archive_and_missing_offline_dependency_are_actionable(self):
        for missing in (False, True):
            with self.subTest(missing=missing), tempfile.TemporaryDirectory(prefix="tct-media-") as directory:
                media, archive, lock = self.fixture(directory)
                if missing:
                    archive.unlink()
                else:
                    archive.write_bytes(b"corrupt")
                result = self.invoke(media, archive, lock)
                self.assertNotEqual(0, result.returncode)
                self.assertFalse((media / "ffmpeg.exe").exists())

    @unittest.skipIf(sys.platform.startswith('win'), 'Non-Windows rejection contract')
    def test_unsupported_platform_is_rejected_before_creating_artifacts(self):
        with tempfile.TemporaryDirectory(prefix='tct-media-unsupported-') as directory:
            media, archive, lock = self.fixture(directory)
            result = self.invoke(media, archive, lock)
            self.assertNotEqual(0, result.returncode)
            self.assertIn('supports Windows x64 only', result.stdout + result.stderr)
            self.assertFalse(media.exists())

    def test_runtime_hash_and_lock_match_and_ordinary_setup_never_starts_asr(self):
        pin = json.loads((REPO / "tooling/media/artifacts.lock.json").read_text())
        source = (REPO / "TCTEnglish/Services/Speaking/AudioImportOptions.cs").read_text()
        self.assertIn(pin["files"][0]["sha256"], source)
        self.assertNotIn("local-media.ps1", (REPO / "scripts/agent_clients.py").read_text())


if __name__ == "__main__":
    unittest.main()
