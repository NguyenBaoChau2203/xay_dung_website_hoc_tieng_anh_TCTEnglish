"""Readiness must respect its total deadline, including an in-flight HTTP probe."""
from __future__ import annotations

import json
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
import subprocess
import sys
import tempfile
import threading
import time
import unittest

from scripts.tests.test_local_speech import _find_pwsh

REPO = Path(__file__).resolve().parents[2]

DRIVER = r"""
param([string]$Source, [int]$Port, [string]$Wav)
$ErrorActionPreference = 'Stop'
$ast = [System.Management.Automation.Language.Parser]::ParseFile($Source, [ref]$null, [ref]$null)
foreach ($name in @('Test-InferenceSegmentElement','Test-InferenceResponseBody','Invoke-InferenceProbe','Wait-SpeechReady')) {
    $definitions = @($ast.FindAll({param($n) $n -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq $name}, $true))
    if ($definitions.Count -ne 1) { throw "Missing function: $name" }
    . ([scriptblock]::Create($definitions[0].Extent.Text))
}
$clock = [Diagnostics.Stopwatch]::StartNew()
$ready = $false
$errorText = ''
try {
    $null = Wait-SpeechReady -ProcessId $PID -BaseUrl "http://127.0.0.1:$Port" -WavPath $Wav -DeadlineSeconds 1 -ProbeTimeoutSeconds 30
    $ready = $true
} catch { $errorText = $_.Exception.Message }
$clock.Stop()
[pscustomobject]@{Ready=$ready; Elapsed=$clock.Elapsed.TotalSeconds; Error=$errorText} | ConvertTo-Json -Compress
"""


@unittest.skipUnless(sys.platform.startswith('win'), 'Windows speech launcher')
class SpeechReadinessDeadlineTests(unittest.TestCase):
    def _probe(self, delay: float) -> dict:
        class Handler(BaseHTTPRequestHandler):
            def do_POST(self):  # noqa: N802
                self.rfile.read(int(self.headers.get('Content-Length', '0')))
                time.sleep(delay)
                body = b'{"text":"","segments":[]}'
                try:
                    self.send_response(200)
                    self.send_header('Content-Length', str(len(body)))
                    self.end_headers()
                    self.wfile.write(body)
                except OSError:
                    pass  # Expected when the readiness deadline cancels the client.

            def log_message(self, *args):
                pass

        server = ThreadingHTTPServer(('127.0.0.1', 0), Handler)
        server.daemon_threads = True
        worker = threading.Thread(target=lambda: server.serve_forever(poll_interval=.05), daemon=True)
        worker.start()
        try:
            with tempfile.TemporaryDirectory(prefix='tct-speech-deadline-') as directory:
                root = Path(directory)
                driver = root / 'probe.ps1'
                driver.write_text(DRIVER, encoding='utf-8')
                wav = root / 'fixture.wav'
                wav.write_bytes(b'RIFF-fixture-only')
                result = subprocess.run(
                    [_find_pwsh(), '-NoProfile', '-File', str(driver), '-Source',
                     str(REPO / 'scripts/local-speech.ps1'), '-Port', str(server.server_port), '-Wav', str(wav)],
                    text=True, capture_output=True, timeout=15, check=False,
                )
                self.assertEqual(0, result.returncode, result.stdout + result.stderr)
                return json.loads(result.stdout.strip().splitlines()[-1])
        finally:
            server.shutdown()
            server.server_close()
            worker.join(timeout=2)

    def test_slow_success_does_not_outlive_total_readiness_deadline(self):
        result = self._probe(2.5)
        self.assertFalse(result['Ready'], result)
        self.assertLess(result['Elapsed'], 2.0, result)
        self.assertIn('timed out', result['Error'].lower())

    def test_fast_valid_response_is_still_ready(self):
        result = self._probe(.02)
        self.assertTrue(result['Ready'], result)
        self.assertLess(result['Elapsed'], 1.5, result)


if __name__ == '__main__':
    unittest.main()
