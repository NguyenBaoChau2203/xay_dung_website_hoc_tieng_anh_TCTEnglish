"""Exercise the real launcher in a disposable checkout with fake native commands."""
import json
from pathlib import Path
import shutil
import socket
import subprocess
import sys
import tempfile
import unittest

from test_local_speech import _find_pwsh

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
$env:Learning__Speech__WhisperHttpBaseUrl='http://127.0.0.1:12345'
$global:NativeCalls=0
$global:WebRan=$false
$global:WebKeyMatched=$false
$global:WebSpeechMatched=$false
$global:BootstrapRan=$false
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
        $global:WebSpeechMatched=($env:Learning__Speech__WhisperHttpBaseUrl -ceq 'http://127.0.0.1:8092')
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
    WebSpeechMatched=$global:WebSpeechMatched
    BootstrapRan=$global:BootstrapRan
    KeyRestored=($null -eq [Environment]::GetEnvironmentVariable('AI__ApiKey','Process'))
    SpeechRestored=($env:Learning__Speech__WhisperHttpBaseUrl -ceq 'http://127.0.0.1:12345')
} | ConvertTo-Json -Compress
"""


@unittest.skipUnless(sys.platform.startswith('win'), 'Windows local launcher/User Secrets fixture')
class LocalLauncherTests(unittest.TestCase):
    def invoke(self, port, fail_web=False):
        with tempfile.TemporaryDirectory(prefix='tct-launcher-') as directory:
            root = Path(directory)
            scripts = root / 'scripts'
            scripts.mkdir()
            for name in ('start-local.ps1', 'LocalAiConfiguration.psm1'):
                shutil.copyfile(REPO / 'scripts' / name, scripts / name)
            (scripts / 'local-media.ps1').write_text(
                "[pscustomobject]@{Status='verified';Version='fixture'}\n", encoding='utf-8')
            (scripts / 'local-speech.ps1').write_text(
                "$global:LASTEXITCODE=0\n"
                "'{\"Status\":\"ready\",\"BaseUrl\":\"http://127.0.0.1:8092\",\"ModelIdentity\":\"fixture-tiny\"}'\n",
                encoding='utf-8')
            (root / 'TCTEnglish').mkdir()
            (root / 'TCTEnglish/TCTEnglish.csproj').write_text(
                '<Project><PropertyGroup><UserSecretsId>fixture</UserSecretsId></PropertyGroup></Project>',
                encoding='utf-8')
            script = root / 'driver.ps1'
            script.write_text(DRIVER, encoding='utf-8')
            arguments = [_find_pwsh(), '-NoProfile', '-File', str(script), '-Port', str(port)]
            if fail_web:
                arguments.append('-FailWeb')
            process = subprocess.run(arguments, text=True, capture_output=True, timeout=25)
            self.assertEqual(0, process.returncode, process.stderr)
            self.assertNotIn('fixture-launcher-openai-key', process.stdout + process.stderr)
            store = root / 'profile/Microsoft/UserSecrets/fixture/secrets.json'
            if store.exists():
                self.assertNotIn('fixture-launcher-openai-key', store.read_text(encoding='utf-8'))
            return json.loads(process.stdout.strip().splitlines()[-1])

    def test_web_inherits_key_and_speech_then_launcher_restores_environment(self):
        with socket.socket() as reservation:
            reservation.bind(('127.0.0.1', 0))
            port = reservation.getsockname()[1]
        result = self.invoke(port)
        self.assertFalse(result['Failed'], result)
        self.assertTrue(result['WebRan'] and result['WebKeyMatched'] and result['WebSpeechMatched'], result)
        self.assertTrue(result['BootstrapRan'], result)
        self.assertTrue(result['KeyRestored'] and result['SpeechRestored'], result)

    def test_web_failure_also_restores_key_and_speech_environment(self):
        with socket.socket() as reservation:
            reservation.bind(('127.0.0.1', 0))
            port = reservation.getsockname()[1]
        result = self.invoke(port, fail_web=True)
        self.assertTrue(result['Failed'] and result['WebKeyMatched'], result)
        self.assertTrue(result['KeyRestored'] and result['SpeechRestored'], result)

    def test_busy_port_fails_before_build_and_does_not_claim_old_web_has_new_key(self):
        with socket.socket() as listener:
            listener.bind(('127.0.0.1', 0))
            listener.listen()
            result = self.invoke(listener.getsockname()[1])
        self.assertTrue(result['Failed'], result)
        self.assertIn('already', result['Message'].lower())
        self.assertEqual(0, result['NativeCalls'], result)
        self.assertFalse(result['WebRan'], result)


if __name__ == '__main__':
    unittest.main()
