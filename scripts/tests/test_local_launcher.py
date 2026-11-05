"""Exercise the real launcher in a disposable checkout with fake native commands."""
import json
from pathlib import Path
import shutil
import socket
import subprocess
import sys
import tempfile
import unittest

from pwsh_util import find_pwsh

REPO = Path(__file__).resolve().parents[2]

DRIVER = r"""
param([int]$Port, [switch]$FailWeb, [switch]$DatabaseOnly, [switch]$FailSync)
$ErrorActionPreference='Stop'
$env:APPDATA=Join-Path $PSScriptRoot 'profile'
$env:AI__ApiKey=$null
$env:OPENAI_API_KEY='fixture-launcher-openai-key'
foreach ($name in @('AI__Model','AI__BaseUrl','AI__ReasoningEffort')) {
    Remove-Item -LiteralPath "Env:$name" -ErrorAction SilentlyContinue
}
$global:NativeCalls=0
$global:WebRan=$false
$global:WebKeyMatched=$false
$global:WebTempMatched=$false
$global:WebDatabaseMatched=$false
$env:ConnectionStrings__DefaultConnection='fixture-parent-connection'
$previousFixtureDatabase=$env:ConnectionStrings__DefaultConnection
$fixtureRuntimeNames=@('AI__ApiKey','AI__BaseUrl','AI__Model','AI__ReasoningEffort','ConnectionStrings__DefaultConnection','Database__ServerVersion','Database__ApplyMigrationsOnStartup','Database__SeedOnStartup','Billing__PendingPaymentCleanupWorkerEnabled','Billing__PremiumExpiryWorkerEnabled','ReconciliationWorker__Enabled','ASPNETCORE_ENVIRONMENT','DOTNET_ENVIRONMENT','TEMP','TMP')
$fixtureRuntimeBefore=@{}
foreach ($name in $fixtureRuntimeNames) { $fixtureRuntimeBefore[$name]=[Environment]::GetEnvironmentVariable($name,'Process') }
$previousFixtureTemp=$env:TEMP
$previousFixtureTmp=$env:TMP
$global:BootstrapRan=$false
$global:SharedSyncRan=$false
function global:pwsh {
    if ($args -notcontains '-DatabaseReady' -or $args -notcontains '-SkipBuild' -or $args -notcontains 'Sync') { throw 'Unexpected child PowerShell call.' }
    $global:SharedSyncRan=$true
    $global:LASTEXITCODE=if($FailSync){2}else{0}
}
$global:LocalAsrTouched=$false
function global:Start-Process {
    $global:LocalAsrTouched=$true
    throw 'Ordinary NoBrowser startup must not spawn a background engine.'
}
function global:Invoke-WebRequest {
    $global:LocalAsrTouched=$true
    throw 'Ordinary startup must not download an engine or model.'
}
function global:docker {
    $global:NativeCalls++
    if ($args[0] -eq 'exec') { '1' }
    $global:LASTEXITCODE=0
}
function global:dotnet {
    $global:NativeCalls++
    if ($args[0] -eq 'run' -and $args -contains '--local-bootstrap=true') { $global:BootstrapRan=$true }
    if ($args[0] -eq 'run' -and $args -notcontains '--local-bootstrap=true') {
        $global:WebRan=$true
        $global:WebKeyMatched=($env:AI__ApiKey -ceq $env:OPENAI_API_KEY)
        $expectedFixtureTemp=Join-Path $PSScriptRoot '.appdata/local/temp'
        $global:WebTempMatched=($env:TEMP -ceq $expectedFixtureTemp -and $env:TMP -ceq $expectedFixtureTemp)
        $global:WebDatabaseMatched=($env:ConnectionStrings__DefaultConnection -match '^Server=127\.0\.0\.1;Port=3307;Database=tctenglish_dev;' -and $env:Database__SeedOnStartup -eq 'false' -and $env:Database__ApplyMigrationsOnStartup -eq 'true' -and $env:Billing__PendingPaymentCleanupWorkerEnabled -eq 'false')
        if ($FailWeb) { throw 'Fixture web startup failure.' }
    }
    $global:LASTEXITCODE=0
}
$failed=$false
$message=''
try { & (Join-Path $PSScriptRoot 'scripts/start-local.ps1') -NoBrowser -WebPort $Port -DatabaseOnly:$DatabaseOnly }
catch { $failed=$true; $message=$_.Exception.Message }
$allRuntimeRestored=$true
foreach ($name in $fixtureRuntimeNames) { if ($fixtureRuntimeBefore[$name] -cne [Environment]::GetEnvironmentVariable($name,'Process')) {$allRuntimeRestored=$false} }
[pscustomobject]@{
    Failed=$failed; Message=$message; NativeCalls=$global:NativeCalls
    WebRan=$global:WebRan; WebKeyMatched=$global:WebKeyMatched
    WebTempMatched=$global:WebTempMatched
    WebDatabaseMatched=$global:WebDatabaseMatched
    DatabaseEnvironmentRestored=($env:ConnectionStrings__DefaultConnection -ceq $previousFixtureDatabase)
    AllRuntimeRestored=$allRuntimeRestored
    TempRestored=($env:TEMP -ceq $previousFixtureTemp -and $env:TMP -ceq $previousFixtureTmp)
    BootstrapRan=$global:BootstrapRan
    SharedSyncRan=$global:SharedSyncRan
    LocalAsrTouched=$global:LocalAsrTouched
    KeyRestored=($null -eq [Environment]::GetEnvironmentVariable('AI__ApiKey','Process'))
} | ConvertTo-Json -Compress
"""


@unittest.skipUnless(sys.platform.startswith('win'), 'Windows local launcher/User Secrets fixture')
class LocalLauncherTests(unittest.TestCase):
    def invoke(self, port, fail_web=False, initial_secrets=None, forbid_secret_writes=False, database_only=False, fail_sync=False):
        with tempfile.TemporaryDirectory(prefix='tct-launcher-') as directory:
            root = Path(directory)
            scripts = root / 'scripts'
            scripts.mkdir()
            for name in ('start-local.ps1', 'LocalAiConfiguration.psm1', 'speech-status.ps1'):
                shutil.copyfile(REPO / 'scripts' / name, scripts / name)
            if forbid_secret_writes:
                with (scripts / 'LocalAiConfiguration.psm1').open('a', encoding='utf-8') as module:
                    module.write("\nfunction Update-LocalSecretStore { throw 'Fixture credential store is read-only.' }\nExport-ModuleMember -Function Update-LocalSecretStore\n")
            # Simulate a teammate who still has the retired launcher/cache on disk.
            # Any attempt to reuse the old script must fail the ordinary-startup test.
            (scripts / 'local-speech.ps1').write_text(
                "$global:LocalAsrTouched=$true\nthrow 'Retired engine must never be started.'\n",
                encoding='utf-8')
            legacy_cache = root / '.appdata/local/speech'
            legacy_cache.mkdir(parents=True)
            (legacy_cache / 'state.json').write_text(
                json.dumps({'status': 'stopped', 'baseUrl': 'http://127.0.0.1:8091/'}), encoding='utf-8')
            (root / 'TCTEnglish').mkdir()
            (root / 'TCTEnglish/TCTEnglish.csproj').write_text(
                '<Project><PropertyGroup><UserSecretsId>fixture</UserSecretsId></PropertyGroup></Project>',
                encoding='utf-8')
            store = root / 'profile/Microsoft/UserSecrets/fixture/secrets.json'
            if initial_secrets is not None:
                store.parent.mkdir(parents=True)
                store.write_text(json.dumps(initial_secrets), encoding='utf-8')
            previous_store = store.read_bytes() if store.exists() else None
            previous_mtime = store.stat().st_mtime_ns if store.exists() else None
            script = root / 'driver.ps1'
            script.write_text(DRIVER, encoding='utf-8')
            arguments = [find_pwsh(), '-NoProfile', '-File', str(script), '-Port', str(port)]
            if fail_web:
                arguments.append('-FailWeb')
            if database_only:
                arguments.append('-DatabaseOnly')
            if fail_sync:
                arguments.append('-FailSync')
            process = subprocess.run(arguments, text=True, capture_output=True, timeout=25)
            self.assertEqual(0, process.returncode, process.stderr)
            combined = process.stdout + process.stderr
            self.assertNotIn('fixture-launcher-openai-key', combined)
            self.assertNotIn('local-speech', combined.lower())
            self.assertNotRegex(combined.lower(), r'\bwhisper\b')
            if store.exists():
                self.assertNotIn('fixture-launcher-openai-key', store.read_text(encoding='utf-8'))
                if initial_secrets is None:
                    self.assertNotIn('AI:ApiKey', json.loads(store.read_text(encoding='utf-8-sig')),
                                     'Fresh startup must not create an empty key override.')
            if forbid_secret_writes:
                self.assertEqual(previous_store, store.read_bytes() if store.exists() else None)
                self.assertEqual(previous_mtime, store.stat().st_mtime_ns if store.exists() else None)
            result = json.loads(process.stdout.strip().splitlines()[-1])
            self.assertTrue(result['AllRuntimeRestored'], result)
            return result

    def test_web_inherits_key_then_launcher_restores_environment_without_local_asr(self):
        with socket.socket() as reservation:
            reservation.bind(('127.0.0.1', 0))
            port = reservation.getsockname()[1]
        result = self.invoke(port)
        self.assertFalse(result['Failed'], result)
        self.assertTrue(result['WebRan'] and result['WebKeyMatched'], result)
        self.assertTrue(result['WebTempMatched'] and result['TempRestored'], result)
        self.assertTrue(result['WebDatabaseMatched'] and result['DatabaseEnvironmentRestored'], result)
        self.assertTrue(result['BootstrapRan'], result)
        self.assertTrue(result['SharedSyncRan'], result)
        self.assertFalse(result['LocalAsrTouched'], result)
        self.assertTrue(result['KeyRestored'], result)

    def test_web_failure_also_restores_key_environment(self):
        with socket.socket() as reservation:
            reservation.bind(('127.0.0.1', 0))
            port = reservation.getsockname()[1]
        result = self.invoke(port, fail_web=True)
        self.assertTrue(result['Failed'] and result['WebKeyMatched'], result)
        self.assertTrue(result['WebTempMatched'] and result['TempRestored'], result)
        self.assertTrue(result['WebDatabaseMatched'] and result['DatabaseEnvironmentRestored'], result)
        self.assertTrue(result['KeyRestored'], result)

    def test_existing_empty_key_is_preserved_without_global_fallback(self):
        with socket.socket() as reservation:
            reservation.bind(('127.0.0.1', 0))
            port = reservation.getsockname()[1]
        result = self.invoke(port, initial_secrets={'AI:ApiKey': ''})
        self.assertFalse(result['Failed'], result)
        self.assertTrue(result['WebRan'], result)
        self.assertFalse(result['WebKeyMatched'], result)
        self.assertTrue(result['WebTempMatched'] and result['TempRestored'], result)
        self.assertTrue(result['KeyRestored'], result)

    def test_busy_port_fails_before_build_and_does_not_claim_old_web_has_new_key(self):
        with socket.socket() as listener:
            listener.bind(('127.0.0.1', 0))
            listener.listen()
            result = self.invoke(listener.getsockname()[1])
        self.assertTrue(result['Failed'], result)
        self.assertIn('already', result['Message'].lower())
        self.assertEqual(0, result['NativeCalls'], result)
        self.assertFalse(result['WebRan'], result)
        self.assertTrue(result['TempRestored'], result)

    def test_startup_never_writes_an_existing_credential_store(self):
        with socket.socket() as reservation:
            reservation.bind(('127.0.0.1', 0))
            port = reservation.getsockname()[1]
        result = self.invoke(port, initial_secrets={'AI:ApiKey': 'fixture-stored-key',
            'SmtpSettings:Password': 'fixture-other-secret', 'ConnectionStrings:DefaultConnection': 'fixture-old-connection'},
            forbid_secret_writes=True)
        self.assertFalse(result['Failed'], result)
        self.assertTrue(result['WebRan'] and result['WebDatabaseMatched'], result)
        self.assertTrue(result['DatabaseEnvironmentRestored'] and result['TempRestored'], result)

    def test_fresh_startup_works_without_creating_a_credential_store(self):
        with socket.socket() as reservation:
            reservation.bind(('127.0.0.1', 0))
            port = reservation.getsockname()[1]
        result = self.invoke(port, forbid_secret_writes=True)
        self.assertFalse(result['Failed'], result)
        self.assertTrue(result['WebRan'] and result['WebKeyMatched'] and result['WebDatabaseMatched'], result)

    def test_database_only_does_not_touch_credentials_and_restores_runtime(self):
        result = self.invoke(5100, initial_secrets={'AI:ApiKey': 'fixture-stored-key'},
                             forbid_secret_writes=True, database_only=True)
        self.assertFalse(result['Failed'], result)
        self.assertFalse(result['WebRan'] or result['BootstrapRan'], result)
        self.assertFalse(result['SharedSyncRan'], result)
        self.assertTrue(result['DatabaseEnvironmentRestored'] and result['TempRestored'], result)

    def test_shared_data_conflict_stops_web_and_restores_environment(self):
        with socket.socket() as reservation:
            reservation.bind(('127.0.0.1', 0)); port=reservation.getsockname()[1]
        result=self.invoke(port,fail_sync=True,initial_secrets={'AI:ApiKey':'fixture-stored-key'},forbid_secret_writes=True)
        self.assertTrue(result['Failed'] and result['SharedSyncRan'],result)
        self.assertFalse(result['WebRan'],result)


if __name__ == '__main__':
    unittest.main()
