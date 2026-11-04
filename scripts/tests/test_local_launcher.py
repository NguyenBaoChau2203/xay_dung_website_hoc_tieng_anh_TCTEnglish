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
param([int]$Port, [switch]$FailWeb)
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
$previousFixtureTemp=$env:TEMP
$previousFixtureTmp=$env:TMP
$global:BootstrapRan=$false
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
        if ($FailWeb) { throw 'Fixture web startup failure.' }
    }
    $global:LASTEXITCODE=0
}
$failed=$false
$message=''
try { & (Join-Path $PSScriptRoot 'scripts/start-local.ps1') -NoBrowser -WebPort $Port }
catch { $failed=$true; $message=$_.Exception.Message }
[pscustomobject]@{
    Failed=$failed; Message=$message; NativeCalls=$global:NativeCalls
    WebRan=$global:WebRan; WebKeyMatched=$global:WebKeyMatched
    WebTempMatched=$global:WebTempMatched
    TempRestored=($env:TEMP -ceq $previousFixtureTemp -and $env:TMP -ceq $previousFixtureTmp)
    BootstrapRan=$global:BootstrapRan
    LocalAsrTouched=$global:LocalAsrTouched
    KeyRestored=($null -eq [Environment]::GetEnvironmentVariable('AI__ApiKey','Process'))
} | ConvertTo-Json -Compress
"""


@unittest.skipUnless(sys.platform.startswith('win'), 'Windows local launcher/User Secrets fixture')
class LocalLauncherTests(unittest.TestCase):
    def invoke(self, port, fail_web=False, initial_secrets=None):
        with tempfile.TemporaryDirectory(prefix='tct-launcher-') as directory:
            root = Path(directory)
            scripts = root / 'scripts'
            scripts.mkdir()
            for name in ('start-local.ps1', 'LocalAiConfiguration.psm1', 'speech-status.ps1'):
                shutil.copyfile(REPO / 'scripts' / name, scripts / name)
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
            script = root / 'driver.ps1'
            script.write_text(DRIVER, encoding='utf-8')
            arguments = [find_pwsh(), '-NoProfile', '-File', str(script), '-Port', str(port)]
            if fail_web:
                arguments.append('-FailWeb')
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
            return json.loads(process.stdout.strip().splitlines()[-1])

    def test_web_inherits_key_then_launcher_restores_environment_without_local_asr(self):
        with socket.socket() as reservation:
            reservation.bind(('127.0.0.1', 0))
            port = reservation.getsockname()[1]
        result = self.invoke(port)
        self.assertFalse(result['Failed'], result)
        self.assertTrue(result['WebRan'] and result['WebKeyMatched'], result)
        self.assertTrue(result['WebTempMatched'] and result['TempRestored'], result)
        self.assertTrue(result['BootstrapRan'], result)
        self.assertFalse(result['LocalAsrTouched'], result)
        self.assertTrue(result['KeyRestored'], result)

    def test_web_failure_also_restores_key_environment(self):
        with socket.socket() as reservation:
            reservation.bind(('127.0.0.1', 0))
            port = reservation.getsockname()[1]
        result = self.invoke(port, fail_web=True)
        self.assertTrue(result['Failed'] and result['WebKeyMatched'], result)
        self.assertTrue(result['WebTempMatched'] and result['TempRestored'], result)
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


if __name__ == '__main__':
    unittest.main()
