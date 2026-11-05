"""Staging must never reuse the production destination or credentials."""
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
import zipfile
from unittest import mock

ROOT = Path(__file__).resolve().parents[2]


class StagingPublisherTests(unittest.TestCase):
    def setUp(self):
        spec = importlib.util.spec_from_file_location('stage_publisher', ROOT / 'scripts/publish-staging-release.py')
        self.module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(self.module)

    def test_marker_rejects_production_even_with_same_commit_and_digest(self):
        marker = {'commit': 'a' * 40, 'runNumber': 12, 'sha256': 'b' * 64}
        self.assertFalse(self.module.marker_matches(marker, 'a' * 40, 12, 'b' * 64))
        marker['environment'] = 'Staging'
        self.assertTrue(self.module.marker_matches(marker, 'a' * 40, 12, 'b' * 64))

    def test_webhook_and_portal_are_required_before_any_remote_write(self):
        with tempfile.NamedTemporaryFile() as artifact:
            for missing in ('PLESK_STAGING_WEBHOOK_URL', 'STAGING_PORTAL_PASSWORD'):
                env = self.valid_env()
                del env[missing]
                with self.subTest(missing=missing), mock.patch.dict(os.environ, env, clear=True), \
                        mock.patch.object(self.module, 'ensure_staging_release') as create:
                    with self.assertRaises(SystemExit):
                        self.module.main_with_artifact(artifact.name)
                    create.assert_not_called()

    def valid_env(self):
        return {
            'GITHUB_SHA': 'a' * 40, 'GITHUB_RUN_ID': '123456',
            'GITHUB_REPOSITORY': self.module.common.REPOSITORY, 'GITHUB_REF': 'refs/heads/master',
            'GH_TOKEN': 'test-only', 'PLESK_STAGING_WEBHOOK_URL': self.module.WEBHOOK_PREFIX + 'uuid=test',
            'STAGING_PORTAL_USERNAME': 'stage-user', 'STAGING_PORTAL_PASSWORD': 'test-only',
        }

    def test_feature_branch_cannot_publish(self):
        env = self.valid_env(); env['GITHUB_REF'] = 'refs/heads/feature/example'
        with mock.patch.dict(os.environ, env, clear=True), mock.patch.object(self.module, 'ensure_staging_release') as create:
            with self.assertRaises(SystemExit):
                self.module.main_with_artifact('unused.zip')
            create.assert_not_called()

    def test_https_basic_auth_cannot_be_redirected_to_another_origin(self):
        handler = self.module.NoRedirect()
        with self.assertRaises(Exception):
            handler.redirect_request(None, None, 302, 'Found', {}, 'https://other.invalid/')

    def test_publish_targets_only_staging_branch_manifest_and_script(self):
        env = self.valid_env(); digest = 'b' * 64
        with tempfile.NamedTemporaryFile() as artifact, mock.patch.dict(os.environ, env, clear=True), \
                mock.patch.object(self.module, 'ensure_staging_release', return_value=digest), \
                mock.patch.object(self.module.common, 'update_branch') as update, \
                mock.patch.object(self.module.common, 'notify_plesk', return_value='accepted'), \
                mock.patch.object(self.module, 'wait_for_deployed_release', return_value=True):
            self.module.main_with_artifact(artifact.name)
        files = update.call_args.args[0]
        self.assertEqual(set(files), {'release.json', 'deploy-staging.ps1'})
        manifest = json.loads(files['release.json'])
        self.assertEqual(manifest['environment'], 'Staging')
        self.assertEqual(self.module.common.BRANCH, 'release/staging')
        self.assertNotIn('test-only', json.dumps(files))

    def test_reused_release_requires_correct_tag_commit_and_digest(self):
        with tempfile.NamedTemporaryFile() as artifact, \
                mock.patch.object(self.module.common, 'file_sha256', return_value='b' * 64), \
                mock.patch.object(self.module.common, 'find_release_by_tag', return_value={'id': 1}), \
                mock.patch.object(self.module.common, 'resolve_tag_commit_sha', return_value='c' * 40), \
                mock.patch.object(self.module.common.subprocess, 'run') as create:
            with self.assertRaises(SystemExit):
                self.module.ensure_staging_release('tag', Path(artifact.name), 'a' * 40)
            create.assert_not_called()

    def test_slow_host_release_after_eight_minutes_is_still_verified(self):
        elapsed = [0.0]
        def portal(relative, **kwargs):
            if relative.endswith('ready'):
                return 200, {'status': 'ready'}
            return 200, {'environment': 'Staging', 'commit': 'a' * 40 if elapsed[0] >= 480 else 'c' * 40,
                'runNumber': 12, 'sha256': 'b' * 64}
        with mock.patch.object(self.module, 'portal_json', side_effect=portal), \
                mock.patch.object(self.module.time, 'monotonic', side_effect=lambda: elapsed[0]), \
                mock.patch.object(self.module.time, 'sleep', side_effect=lambda seconds: elapsed.__setitem__(0, elapsed[0] + seconds)):
            self.assertTrue(self.module.wait_for_deployed_release('a' * 40, 12, 'b' * 64))
        self.assertGreaterEqual(elapsed[0], 480)
        self.assertLess(elapsed[0], 1200)

    def test_verification_budget_includes_http_time_and_stops(self):
        elapsed = [0.0]
        def unavailable(*args, **kwargs):
            elapsed[0] += min(8, kwargs.get('timeout', 10))
            raise OSError('unavailable')
        with mock.patch.object(self.module, 'portal_json', side_effect=unavailable), \
                mock.patch.object(self.module.time, 'monotonic', side_effect=lambda: elapsed[0]), \
                mock.patch.object(self.module.time, 'sleep', side_effect=lambda seconds: elapsed.__setitem__(0, elapsed[0] + seconds)):
            self.assertFalse(self.module.wait_for_deployed_release('a' * 40, 12, 'b' * 64, timeout_seconds=30))
        self.assertLessEqual(elapsed[0], 30)


@unittest.skipUnless(os.name == 'nt', 'Plesk Windows PowerShell 5.1 preflight')
class StagingHostGuardTests(unittest.TestCase):
    def run_script(self, root, site_name='staging.tctenglish.site', enabled=False, settings=None):
        site = root / site_name
        site.mkdir()
        (site / 'app_offline.htm').write_text('operator maintenance')
        manifest = root / 'release.json'
        manifest.write_text(json.dumps({'enabled': enabled, 'environment': 'Staging'}))
        state = root / 'private' / 'tctenglish-staging-state'
        state.mkdir(parents=True)
        if settings is not None:
            (state / 'hosting.json').write_text(settings if isinstance(settings, str) else json.dumps(settings))
        result = subprocess.run(['powershell.exe', '-NoProfile', '-NonInteractive', '-File',
            str(ROOT / 'scripts/deploy-staging.ps1'), '-ManifestPath', str(manifest),
            '-SiteRoot', str(site), '-StateRoot', str(state)], capture_output=True, text=True)
        self.assertEqual((site / 'app_offline.htm').read_text(), 'operator maintenance')
        return result, state

    def test_disabled_preflight_preserves_app(self):
        with tempfile.TemporaryDirectory() as directory:
            result, state = self.run_script(Path(directory))
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertFalse(json.loads((state / 'preflight.json').read_text())['DatabaseOrWebsiteChanged'])

    def test_production_root_is_rejected_before_state_write(self):
        with tempfile.TemporaryDirectory() as directory:
            result, state = self.run_script(Path(directory), site_name='httpdocs')
            self.assertNotEqual(result.returncode, 0)
            self.assertFalse((state / 'preflight.json').exists())

    def test_host_identity_rejects_production_database_before_download(self):
        with tempfile.TemporaryDirectory() as directory:
            settings = {'environment': 'Staging', 'siteHost': 'staging.tctenglish.site',
                'connectionString': 'Server=localhost;Database=nhtctsnn_tctenglish;User=x;Password=private-test-marker;',
                'portalUsername': 'x', 'portalPassword': 'private-test-marker'}
            result, state = self.run_script(Path(directory), enabled=True, settings=settings)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn('Staging database identity', result.stdout + result.stderr)
            self.assertNotIn('private-test-marker', result.stdout + result.stderr)
            self.assertFalse((state / 'deploy.lock').exists())

    def test_malformed_settings_does_not_echo_private_json(self):
        with tempfile.TemporaryDirectory() as directory:
            result, state = self.run_script(Path(directory), enabled=True,
                settings='{"connectionString":"private-test-marker", broken')
            self.assertNotEqual(result.returncode, 0)
            self.assertIn('credential details omitted', result.stdout + result.stderr)
            self.assertNotIn('private-test-marker', result.stdout + result.stderr)
            self.assertFalse((state / 'deploy.lock').exists())


@unittest.skipUnless(os.name == 'nt', 'Windows PowerShell deployment failure drill')
class StagingFailureDrillTests(unittest.TestCase):
    """Run the real host script against owned files and fake external commands.

    Only this test's in-memory copy replaces the fixed backup binary path.
    The shipped script exposes no enabled-deployment bypass or fake runner flag.
    """
    @classmethod
    def setUpClass(cls):
        (ROOT / '.tmp').mkdir(exist_ok=True)
        cls.lab = tempfile.TemporaryDirectory(dir=ROOT / '.tmp')
        cls.fake_exe = Path(cls.lab.name) / 'command.exe'
        compile_script = Path(cls.lab.name) / 'compile.ps1'
        compile_script.write_text(r'''param($Output)
$ErrorActionPreference='Stop'
Add-Type -OutputAssembly $Output -OutputType ConsoleApplication -TypeDefinition @'
using System;
using System.IO;
public class FakeStageCommand {
 public static int Main(string[] args) {
  var log=Environment.GetEnvironmentVariable("TCT_TEST_TRACE");
  var fail=Environment.GetEnvironmentVariable("TCT_TEST_FAIL");
  foreach(var arg in args) if(arg.StartsWith("--result-file=")) {
   File.AppendAllText(log,"backup\n");
   if(fail=="backup") return 7;
   File.WriteAllText(arg.Substring(14),new string('x',1500));return 0;
  }
  File.AppendAllText(log,"migration\n");
  var connection=Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")??"";
  if(connection.IndexOf("database=nhtctsnn_tctenglish_staging",StringComparison.OrdinalIgnoreCase)<0) return 8;
  return fail=="migration"?9:0;
 }
}
'@
''')
        result = subprocess.run(['powershell.exe', '-NoProfile', '-File', str(compile_script),
            '-Output', str(cls.fake_exe)], capture_output=True, text=True)
        if result.returncode:
            raise RuntimeError(result.stdout + result.stderr)

    @classmethod
    def tearDownClass(cls):
        cls.lab.cleanup()

    def drill(self, mode='', traversal=False, repeat=False):
        with tempfile.TemporaryDirectory(dir=self.lab.name) as directory:
            root = Path(directory); site = root / 'staging.tctenglish.site'; site.mkdir()
            state = root / 'private/tctenglish-staging-state'; state.mkdir(parents=True)
            (site / 'wwwroot/uploads').mkdir(parents=True)
            (site / 'wwwroot/uploads/keep.txt').write_text('user upload')
            (site / 'TCTEnglish.dll').write_bytes(b'old app')
            (site / 'appsettings.json').write_text('{"ConnectionStrings":{"DefaultConnection":""}}')
            (site / 'appsettings.Staging.json').write_text('{"Learning":{"Speech":{"Enabled":true}}}')
            (site / 'web.config').write_text('<configuration><system.webServer><aspNetCore><environmentVariables>'
                '<environmentVariable name="AI__ApiKey" value="private-test-key"/>'
                '</environmentVariables></aspNetCore></system.webServer></configuration>')
            (state / 'hosting.json').write_text(json.dumps({'environment':'Staging','siteHost':'staging.tctenglish.site',
                'connectionString':'Server=localhost;Port=3306;Database=nhtctsnn_tctenglish_staging;User=nhtctsnn_tct_stage;Password=private-test-password;',
                'portalUsername':'tct-stage','portalPassword':'private-test-password'}))
            manifest = root / 'manifest.json'
            manifest.write_text(json.dumps({'enabled':True,'environment':'Staging',
                'repository':'NguyenBaoChau2203/xay_dung_website_hoc_tieng_anh_TCTEnglish',
                'commit':'a'*40,'runNumber':123456,'sha256':'',
                'artifactUrl':'https://github.com/NguyenBaoChau2203/xay_dung_website_hoc_tieng_anh_TCTEnglish/releases/download/staging-123456-aaaaaaaaaaaa/tctenglish-release.zip'}))
            artifact = root / 'release.zip'
            with zipfile.ZipFile(artifact,'w') as archive:
                for name, data in {'TCTEnglish.exe':b'fake-app','TCTEnglish.dll':b'new app',
                        'appsettings.json':b'{"ConnectionStrings":{"DefaultConnection":""}}',
                        'web.config':b'<configuration><system.webServer><aspNetCore/></system.webServer></configuration>'}.items():
                    archive.writestr('app/'+name,data)
                archive.write(self.fake_exe,'migration/efbundle.exe')
                if traversal:
                    archive.writestr('../escaped.txt',b'must never extract')
            data=json.loads(manifest.read_text()); data['sha256']=__import__('hashlib').sha256(artifact.read_bytes()).hexdigest()
            manifest.write_text(json.dumps(data))
            trace=root/'trace.txt'; harness=root/'harness.ps1'
            source=(ROOT/'scripts/deploy-staging.ps1').read_text(encoding='utf-8')
            needle="$defaultBackupClient = 'C:\\Program Files (x86)\\Plesk\\MySQL\\bin\\mariadb-dump.exe'"
            self.assertEqual(source.count(needle),1)
            source=source.replace(needle,"$defaultBackupClient = $env:TCT_TEST_BINARY")
            source=source.replace("    Write-Json (Join-Path $StateRoot 'last-failure.json')", "    [IO.File]::WriteAllText($env:TCT_TEST_ERROR, [string]$_.Exception.Message + [string]$_.ScriptStackTrace)\n    Write-Json (Join-Path $StateRoot 'last-failure.json')")
            (root/'host-script.ps1').write_text(source,encoding='utf-8')
            harness.write_text(r'''param($Manifest,$Site,$State,$Source)
$ErrorActionPreference='Stop'
Import-Module (Join-Path $PSHOME 'Modules/Microsoft.PowerShell.Utility/Microsoft.PowerShell.Utility.psd1') -Force
function Start-Sleep { param($Seconds) }
function Invoke-WebRequest {
 param([switch]$UseBasicParsing,$Uri,$OutFile,$Headers,$TimeoutSec,$MaximumRedirection)
 if($OutFile) { [IO.File]::Copy($env:TCT_TEST_ZIP,$OutFile);return }
 if($Uri -cne 'https://staging.tctenglish.site/health/ready' -or -not $Headers.Authorization) { throw 'Wrong readiness target/auth.' }
 if($env:TCT_TEST_FAIL -eq 'ready') { throw 'Fake readiness failure' }
 return [pscustomobject]@{StatusCode=200;Content='{"status":"ready"}'}
}
& ([scriptblock]::Create([IO.File]::ReadAllText($Source))) -ManifestPath $Manifest -SiteRoot $Site -StateRoot $State
''',encoding='utf-8')
            env=dict(os.environ,TCT_TEST_TRACE=str(trace),TCT_TEST_FAIL=mode,TCT_TEST_BINARY=str(self.fake_exe),TCT_TEST_ZIP=str(artifact),TCT_TEST_ERROR=str(root/'diagnostic.txt'))
            # The caller may be bundled PowerShell 7. Windows PowerShell 5.1
            # must discover its own modules instead of inheriting PS7 Utility.
            env.pop('PSModulePath', None)
            cmd=['powershell.exe','-NoProfile','-File',str(harness),str(manifest),str(site),str(state),str(root/'host-script.ps1')]
            result=subprocess.run(cmd,env=env,capture_output=True,text=True)
            diagnostic=(root/'diagnostic.txt').read_text() if (root/'diagnostic.txt').exists() else ''
            self.assertNotIn('private-test-',result.stdout+result.stderr)
            self.assertEqual((site/'wwwroot/uploads/keep.txt').read_text(),'user upload')
            steps=trace.read_text().splitlines() if trace.exists() else []
            if not mode and not traversal:
                self.assertEqual(result.returncode,0,result.stdout+result.stderr+diagnostic)
                self.assertEqual(steps,['backup','migration'])
                self.assertEqual((site/'TCTEnglish.dll').read_bytes(),b'new app')
                self.assertIn('private-test-key',(site/'web.config').read_text())
                self.assertTrue(json.loads((site/'appsettings.Staging.json').read_text())['Learning']['Speech']['Enabled'])
                self.assertEqual(json.loads((site/'wwwroot/tct-release.json').read_text())['environment'],'Staging')
                self.assertFalse((site/'app_offline.htm').exists())
                run=next(state.glob('123456-*'))
                self.assertTrue((run/'database-before.sql').exists())
                self.assertFalse((run/'stage').exists())
                if repeat:
                    repeated=subprocess.run(cmd,env=env,capture_output=True,text=True)
                    self.assertEqual(repeated.returncode,0,repeated.stdout+repeated.stderr)
                    self.assertEqual(trace.read_text().splitlines(),steps)
            else:
                self.assertNotEqual(result.returncode,0)
                self.assertEqual(steps,[] if traversal else ['backup'] if mode=='backup' else ['backup','migration'],diagnostic)
                self.assertEqual((site/'TCTEnglish.dll').read_bytes(),b'old app')
                self.assertEqual((site/'app_offline.htm').exists(),mode in ('migration','ready'))
                self.assertFalse((state/'current.json').exists())
                self.assertFalse((root/'escaped.txt').exists())

    def test_success_keeps_host_key_uploads_and_repeat_does_not_migrate(self):
        self.drill(repeat=True)

    def test_backup_failure_does_not_migrate_and_reopens_old_app(self):
        self.drill(mode='backup')

    def test_migration_failure_retains_maintenance_and_backup(self):
        self.drill(mode='migration')

    def test_readiness_failure_restores_old_files_and_retains_maintenance(self):
        self.drill(mode='ready')

    def test_archive_traversal_stops_before_backup_or_migration(self):
        self.drill(traversal=True)


if __name__ == '__main__':
    unittest.main()
