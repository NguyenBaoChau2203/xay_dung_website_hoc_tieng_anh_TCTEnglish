"""Exercise the shipped recovery functions, with process operations mocked only in tests."""
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]


@unittest.skipUnless(os.name == 'nt', 'Windows PowerShell 5.1 hosting recovery')
class OwnedStagingImportRecoveryTests(unittest.TestCase):
    def drill(self, failure=None, expect_stop=False, healthy=False):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            site = root / ('httpdocs' if failure == 'production' else 'staging.tctenglish.site')
            state = root / 'private/tctenglish-staging-state'
            old = state / ('123-' + 'a' * 12)
            backup = old / 'previous-app'; backup.mkdir(parents=True)
            (site / 'wwwroot').mkdir(parents=True)
            (old / 'stage/app').mkdir(parents=True)
            plan = {'runNumber': 123, 'commit': 'a' * 40, 'previousCommit': 'b' * 40}
            owner = {'environment': 'Staging', 'commit': plan['commit'], 'runNumber': 123}
            if failure == 'owner': owner['commit'] = 'c' * 40
            (old / 'owner.json').write_text(json.dumps(owner))
            (state / 'current.json').write_text(json.dumps({'commit': plan['previousCommit']}))
            (state / 'deploy.lock').write_bytes(b'keep lock inode/file')
            (old / 'database-before.sql').write_bytes(b'x' * (10 if failure == 'backup' else 1500))
            migrations = ['20261113112346_AddSpeakingAssessmentComponentScores', '20261113125249_AddSpeakingVideoVisits']
            if failure == 'schema': migrations.append('20261114000000_UnexpectedChange')
            (old / 'migration.log').write_text('\n'.join("Applying migration '%s'." % m for m in migrations) + '\nDone.')
            (site / 'wwwroot/tct-release.json').write_text(json.dumps({'environment': 'Staging', 'commit': plan['previousCommit']}))
            (backup / 'TCTEnglish.dll').write_bytes(b'previous app')
            (site / 'TCTEnglish.dll').write_bytes(b'changed app' if failure == 'files' else b'previous app')
            offline = site / 'app_offline.htm'
            if not healthy:
                offline.write_text('operator maintenance' if failure == 'maintenance' else '<html><body>He thong dang duoc cap nhat. Vui long quay lai sau it phut.</body></html>')
            (root / 'plan.json').write_text(json.dumps(plan))
            harness = root / 'harness.ps1'
            harness.write_text(r'''param($Source,$Root,$Site,$State,$Mode)
$ErrorActionPreference='Stop'
Import-Module (Join-Path $PSHOME 'Modules/Microsoft.PowerShell.Utility/Microsoft.PowerShell.Utility.psd1') -Force
# Mirror the real entry point before invoking extracted functions. Windows CI
# TEMP can contain an 8.3 RUNNER~1 alias that .NET expands during path validation.
$Root=[IO.Path]::GetFullPath($Root)
$Site=[IO.Path]::GetFullPath($Site).TrimEnd('\')
$State=[IO.Path]::GetFullPath($State).TrimEnd('\')
$tokens=$null;$errors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile($Source,[ref]$tokens,[ref]$errors)
if($errors.Count) { throw 'Shipped staging script does not parse.' }
foreach($name in @('Assert-ChildPath','Assert-NoReparsePath','Invoke-OwnedStagingImportRecovery')) {
 $function=$ast.Find({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name},$true)
 if(-not $function) { throw "Missing real function $name" }
 . ([scriptblock]::Create($function.Extent.Text))
}
$script:stopped=$false
function Get-CimInstance {
 param($ClassName,$Filter)
 if($script:stopped) { return }
 $path=Join-Path $State ('123-aaaaaaaaaaaa\stage\app\TCTEnglish.exe')
 if($Mode -eq 'other-process') { $path=Join-Path $Root 'different.exe' }
 [pscustomobject]@{ProcessId=98765;ExecutablePath=$path;CommandLine=('"'+$path+'" --shared-data=sync --shared-target=staging --shared-root="'+$State+'\123-aaaaaaaaaaaa\stage\app\App_Data\shared-data"')}
}
function Stop-Process {
 param($Id,$ErrorAction)
 if($Id -ne 98765) { throw 'Wrong process selected.' }
 [IO.File]::WriteAllText((Join-Path $Root 'stopped.txt'),[string]$Id)
 $script:stopped=$true
 if($Mode -ne 'parent-cleanup') {
  $failure=@{release=('a'*40);maintenanceRetained=$true;recoveryDirectory=(Join-Path $State '123-aaaaaaaaaaaa')}
  [IO.File]::WriteAllText((Join-Path $State 'last-failure.json'),($failure|ConvertTo-Json))
 }
 if($Mode -eq 'current-changed') { [IO.File]::WriteAllText((Join-Path $State 'current.json'),'{"commit":"cccccccccccccccccccccccccccccccccccccccc"}') }
}
function Start-Sleep { param($Milliseconds) }
$plan=Get-Content -Raw -LiteralPath (Join-Path $Root 'plan.json')|ConvertFrom-Json
Invoke-OwnedStagingImportRecovery $Site $State $plan
''', encoding='utf-8')
            env = dict(os.environ); env.pop('PSModulePath', None)
            result = subprocess.run(['powershell.exe', '-NoProfile', '-NonInteractive', '-File', str(harness),
                str(ROOT / 'scripts/deploy-staging.ps1'), str(root), str(site), str(state), failure or ''],
                env=env, capture_output=True, text=True)
            self.assertEqual((root / 'stopped.txt').exists(), expect_stop, result.stdout + result.stderr)
            self.assertEqual((state / 'deploy.lock').read_bytes(), b'keep lock inode/file')
            self.assertEqual((backup / 'TCTEnglish.dll').read_bytes(), b'previous app')
            self.assertTrue((old / 'database-before.sql').exists())
            self.assertEqual((site / 'TCTEnglish.dll').read_bytes(), b'changed app' if failure == 'files' else b'previous app')
            if failure:
                self.assertNotEqual(result.returncode, 0, result.stdout + result.stderr)
                self.assertTrue(offline.exists())
                self.assertFalse((old / 'recovered-app-offline.htm').exists())
            else:
                self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
                self.assertFalse(offline.exists())
                self.assertEqual((old / 'recovered-app-offline.htm').exists(), not healthy)

    def test_only_reviewed_owned_import_is_stopped_and_maintenance_is_preserved_privately(self):
        self.drill(expect_stop=True)

    def test_healthy_site_is_a_no_op(self):
        self.drill(healthy=True)

    def test_wrong_evidence_preserves_maintenance_without_stopping_any_process(self):
        for failure in ('production', 'owner', 'backup', 'schema', 'files', 'maintenance', 'other-process'):
            with self.subTest(failure=failure): self.drill(failure)

    def test_parent_cleanup_and_current_marker_are_required_after_stop(self):
        for failure in ('parent-cleanup', 'current-changed'):
            with self.subTest(failure=failure): self.drill(failure, expect_stop=True)


if __name__ == '__main__':
    unittest.main()
