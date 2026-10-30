import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]


class PublishArtifactTests(unittest.TestCase):
    def prepare(self, root):
        return subprocess.run([sys.executable, str(ROOT / 'scripts/prepare-publish.py'), str(root)], capture_output=True, text=True)

    def fixture(self, root, config):
        (root / 'TCTEnglish.dll').write_bytes(b'test fixture')
        (root / 'TCTEnglish.runtimeconfig.json').write_text('{}')
        (root / 'appsettings.json').write_text(json.dumps(config))

    def test_rejects_source_directory_before_deleting_config(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            dev = root / 'appsettings.Development.json'
            dev.write_text('{}')
            result = self.prepare(root)
            self.assertNotEqual(result.returncode, 0)
            self.assertTrue(dev.exists())

    def test_removes_environment_config_from_publish(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self.fixture(root, {'ConnectionStrings': {'DefaultConnection': ''}})
            (root / 'appsettings.Development.json').write_text('{}')
            self.assertEqual(self.prepare(root).returncode, 0)
            self.assertFalse((root / 'appsettings.Development.json').exists())
            self.assertTrue((root / 'appsettings.json').exists())

    def test_rejects_credential_without_echoing_it(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            marker = 'a-test-secret-that-must-never-be-logged'
            self.fixture(root, {'ConnectionStrings': {'DefaultConnection': marker}})
            result = self.prepare(root)
            self.assertNotEqual(result.returncode, 0)
            self.assertNotIn(marker, result.stdout + result.stderr)

    def test_rejects_runtime_uploads(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self.fixture(root, {})
            uploads = root / 'wwwroot/uploads'
            uploads.mkdir(parents=True)
            (uploads / 'private-image.png').write_bytes(b'private runtime content')
            self.assertNotEqual(self.prepare(root).returncode, 0)


@unittest.skipUnless(os.name == 'nt' and shutil.which('powershell.exe'), 'Windows PowerShell 5.1 host check')
class PleskPreflightTests(unittest.TestCase):
    def test_disabled_release_does_not_touch_website(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            site = root / 'httpdocs'
            site.mkdir()
            marker = site / 'app_offline.htm'
            marker.write_text('operator-owned maintenance')
            manifest = root / 'release.json'
            manifest.write_text('{"enabled": false}')
            state = root / 'private/state'
            result = subprocess.run(['powershell.exe', '-NoProfile', '-NonInteractive', '-File', str(ROOT / 'scripts/deploy-plesk.ps1'),
                '-ManifestPath', str(manifest), '-SiteRoot', str(site), '-StateRoot', str(state)], capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertEqual(marker.read_text(), 'operator-owned maintenance')
            self.assertEqual(list(site.iterdir()), [marker])
            self.assertFalse(json.loads((state / 'preflight.json').read_text())['DatabaseOrWebsiteChanged'])

    def test_state_cannot_escape_private_directory(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            site = root / 'httpdocs'
            site.mkdir()
            manifest = root / 'release.json'
            manifest.write_text('{"enabled": false}')
            result = subprocess.run(['powershell.exe', '-NoProfile', '-NonInteractive', '-File', str(ROOT / 'scripts/deploy-plesk.ps1'),
                '-ManifestPath', str(manifest), '-SiteRoot', str(site), '-StateRoot', str(root / 'outside-private')], capture_output=True, text=True)
            self.assertNotEqual(result.returncode, 0)
            self.assertEqual(list(site.iterdir()), [])


if __name__ == '__main__':
    unittest.main()
