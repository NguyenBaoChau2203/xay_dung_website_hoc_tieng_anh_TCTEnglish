"""Metadata-only speech-status.ps1 must mirror app precedence without network."""
from __future__ import annotations

import json
from pathlib import Path
import subprocess
import tempfile
import unittest

from pwsh_util import find_pwsh

REPO = Path(__file__).resolve().parents[2]


@unittest.skipUnless(__import__("sys").platform.startswith("win"), "speech-status is Windows-local")
class SpeechStatusTests(unittest.TestCase):
    def run_status(self, env: dict[str, str], secrets: dict, appsettings=None, development=None):
        with tempfile.TemporaryDirectory(prefix="tct-speech-status-") as directory:
            root = Path(directory)
            scripts = root / "scripts"
            scripts.mkdir()
            for name in ("speech-status.ps1", "LocalAiConfiguration.psm1"):
                (scripts / name).write_text(
                    (REPO / "scripts" / name).read_text(encoding="utf-8"), encoding="utf-8"
                )
            project = root / "TCTEnglish"
            project.mkdir()
            for name, value in (("appsettings.json", appsettings), ("appsettings.Development.json", development)):
                if value is not None:
                    (project / name).write_text(json.dumps(value), encoding="utf-8")
            (project / "TCTEnglish.csproj").write_text(
                "<Project><PropertyGroup><UserSecretsId>fixture-speech-status</UserSecretsId></PropertyGroup></Project>",
                encoding="utf-8",
            )
            profile = root / "profile" / "Microsoft" / "UserSecrets" / "fixture-speech-status"
            profile.mkdir(parents=True)
            (profile / "secrets.json").write_text(json.dumps(secrets), encoding="utf-8")

            driver = root / "driver.ps1"
            driver.write_text(
                "$ErrorActionPreference='Stop'\n"
                "$env:APPDATA=Join-Path $PSScriptRoot 'profile'\n"
                "& (Join-Path $PSScriptRoot 'scripts/speech-status.ps1')\n",
                encoding="utf-8",
            )
            process_env = {**dict(**{k: v for k, v in __import__("os").environ.items() if k not in env}), **env}
            for key in (
                "Learning__Speech__OpenAI__ApiKey",
                "Learning__Speech__Enabled",
                "Learning__Speech__OpenAI__Model",
                "Learning__Speech__OpenAI__BaseUrl",
                "AI__ApiKey",
                "OPENAI_API_KEY",
            ):
                process_env.pop(key, None)
            process_env.update(env)
            process_env["APPDATA"] = str(root / "profile")
            completed = subprocess.run(
                [find_pwsh(), "-NoProfile", "-File", str(driver)],
                capture_output=True,
                text=True,
                timeout=20,
                env=process_env,
                cwd=str(root),
            )
            self.assertEqual(0, completed.returncode, completed.stderr)
            line = completed.stdout.strip().splitlines()[-1]
            return json.loads(line)

    def test_disabled_env_is_not_configured_even_with_key(self):
        result = self.run_status(
            {
                "Learning__Speech__OpenAI__ApiKey": "fixture-key",
                "Learning__Speech__Enabled": "false",
                "Learning__Speech__OpenAI__Model": "fixture-override-model",
            },
            {},
        )
        self.assertFalse(result["Configured"])
        self.assertFalse(result["Enabled"])
        self.assertTrue(result["KeyPresent"])
        self.assertEqual("fixture-override-model", result["Model"])
        self.assertFalse(result["VerifiedLive"])

    def test_invalid_base_url_env_marks_endpoint_invalid(self):
        result = self.run_status(
            {
                "Learning__Speech__OpenAI__ApiKey": "fixture-key",
                "Learning__Speech__OpenAI__BaseUrl": "ftp://example.invalid",
            },
            {},
        )
        self.assertFalse(result["Configured"])
        self.assertFalse(result["EndpointValid"])

    def test_invalid_endpoint_does_not_echo_url_credentials_or_query(self):
        result = self.run_status({
            "Learning__Speech__OpenAI__ApiKey": "fixture-key",
            "Learning__Speech__OpenAI__BaseUrl": "https://fixture-user:fixture-password@example.invalid/v1/?key=fixture-query#fixture-fragment",
        }, {})
        self.assertFalse(result["Configured"])
        for private in ("fixture-user", "fixture-password", "fixture-query", "fixture-fragment", "fixture-key"):
            self.assertNotIn(private, json.dumps(result))

    def test_appsettings_development_secrets_and_env_follow_app_order(self):
        appsettings = {"Learning": {"Speech": {"Enabled": False, "OpenAI": {"Model": "file-model"}}}}
        development = {"Learning": {"Speech": {"OpenAI": {"Model": "development-model"}}}}
        result = self.run_status({"Learning__Speech__OpenAI__ApiKey": "fixture-key"}, {}, appsettings, development)
        self.assertFalse(result["Configured"])
        self.assertFalse(result["Enabled"])
        self.assertEqual("development-model", result["Model"])

        result = self.run_status({"Learning__Speech__Enabled": "true"}, {
            "Learning:Speech:OpenAI:ApiKey": "fixture-key",
            "Learning:Speech:OpenAI:Model": " secret-model ",
        }, appsettings, development)
        self.assertTrue(result["Configured"])
        self.assertEqual("secret-model", result["Model"])

    def test_invalid_boolean_is_unconfigured_instead_of_truthy(self):
        result = self.run_status({
            "Learning__Speech__Enabled": "not-a-bool",
            "Learning__Speech__OpenAI__ApiKey": "fixture-key",
        }, {})
        self.assertFalse(result["Configured"])



if __name__ == "__main__":
    unittest.main()
