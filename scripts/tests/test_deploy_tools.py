import json
import importlib.util
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest
from unittest import mock

ROOT = Path(__file__).resolve().parents[2]


class PleskWebhookTests(unittest.TestCase):
    def setUp(self):
        spec = importlib.util.spec_from_file_location('plesk_publisher', ROOT / 'scripts/publish-plesk-release.py')
        self.publisher = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(self.publisher)

    def test_uses_post_for_the_plesk_webhook(self):
        with mock.patch.object(self.publisher.urllib.request, 'urlopen') as send:
            self.publisher.notify_plesk('https://plesk.example.test/web-hook.php?uuid=test')
            request = send.call_args.args[0]
            self.assertEqual(request.get_method(), 'POST')
            self.assertEqual(json.loads(request.data), {})
            self.assertEqual(request.get_header('Content-type'), 'application/json')

    def test_webhook_failure_never_exposes_its_private_url(self):
        marker = 'private-webhook-token'
        with mock.patch.object(self.publisher.urllib.request, 'urlopen', side_effect=OSError(marker)):
            with self.assertRaises(SystemExit) as error:
                self.publisher.notify_plesk('https://plesk.example.test/?uuid=' + marker)
            self.assertNotIn(marker, str(error.exception))


class PublishReleaseIdempotencyTests(unittest.TestCase):
    def setUp(self):
        spec = importlib.util.spec_from_file_location('plesk_publisher', ROOT / 'scripts/publish-plesk-release.py')
        self.publisher = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(self.publisher)

    def test_existing_matching_release_is_reused_without_create(self):
        artifact = tempfile.NamedTemporaryFile(delete=False)
        try:
            artifact.write(b'release-bytes')
            artifact.close()
            sha = 'a' * 40
            digest = __import__('hashlib').sha256(b'release-bytes').hexdigest()
            tag = 'deploy-99-' + sha[:12]
            env = {
                'GITHUB_SHA': sha,
                'GITHUB_REPOSITORY': self.publisher.REPOSITORY,
                'GITHUB_RUN_ID': '99',
                'GITHUB_RUN_NUMBER': '7',
                'GH_TOKEN': 'test-token',
                'PLESK_WEBHOOK_URL': 'https://peaceful-bartik.103-28-36-90.plesk.page:8443/modules/git/public/web-hook.php?uuid=test',
            }
            existing = {
                'tag_name': tag,
                'target_commitish': sha,
                'assets': [{'name': 'tctenglish-release.zip', 'browser_download_url': 'https://example.test/' + tag}],
            }

            create_calls = []

            def fake_run(args, check=True):
                create_calls.append(list(args))
                raise AssertionError('gh release create must not run for matching existing release')

            with mock.patch.dict(os.environ, env, clear=False), \
                    mock.patch.object(self.publisher, 'find_release_by_tag', return_value=existing), \
                    mock.patch.object(self.publisher, 'asset_sha256', return_value=digest), \
                    mock.patch.object(self.publisher.subprocess, 'run', side_effect=fake_run), \
                    mock.patch.object(self.publisher, 'update_branch'), \
                    mock.patch.object(self.publisher, 'notify_plesk'), \
                    mock.patch.object(self.publisher, 'wait_for_deployed_commit', return_value=True):
                self.publisher.main_with_artifact(artifact.name)

            self.assertEqual(create_calls, [])
        finally:
            os.unlink(artifact.name)

    def test_existing_release_digest_mismatch_stops_without_clobber(self):
        artifact = tempfile.NamedTemporaryFile(delete=False)
        try:
            artifact.write(b'release-bytes')
            artifact.close()
            sha = 'b' * 40
            tag = 'deploy-100-' + sha[:12]
            env = {
                'GITHUB_SHA': sha,
                'GITHUB_REPOSITORY': self.publisher.REPOSITORY,
                'GITHUB_RUN_ID': '100',
                'GITHUB_RUN_NUMBER': '8',
                'GH_TOKEN': 'test-token',
                'PLESK_WEBHOOK_URL': 'https://peaceful-bartik.103-28-36-90.plesk.page:8443/modules/git/public/web-hook.php?uuid=test',
            }
            existing = {
                'tag_name': tag,
                'target_commitish': sha,
                'assets': [{'name': 'tctenglish-release.zip'}],
            }
            with mock.patch.dict(os.environ, env, clear=False), \
                    mock.patch.object(self.publisher, 'find_release_by_tag', return_value=existing), \
                    mock.patch.object(self.publisher, 'asset_sha256', return_value='0' * 64), \
                    mock.patch.object(self.publisher.subprocess, 'run') as create, \
                    mock.patch.object(self.publisher, 'notify_plesk') as notify:
                with self.assertRaises(SystemExit) as error:
                    self.publisher.main_with_artifact(artifact.name)
            self.assertIn('digest', str(error.exception).lower())
            create.assert_not_called()
            notify.assert_not_called()
        finally:
            os.unlink(artifact.name)


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
