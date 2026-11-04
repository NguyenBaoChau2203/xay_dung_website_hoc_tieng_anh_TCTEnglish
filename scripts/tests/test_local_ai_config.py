"""Fixture-only key/config checks: never use the owner's User Secrets."""
import json
import subprocess
import tempfile
import unittest
from pathlib import Path

from pwsh_util import find_pwsh as _find_pwsh

REPO = Path(__file__).resolve().parents[2]


class LocalAiConfigTests(unittest.TestCase):
    def run_fixture(self, initial, command):
        with tempfile.TemporaryDirectory(prefix="tct-ai-secrets-") as directory:
            root = Path(directory)
            store = root / "secrets.json"
            store.write_text(json.dumps(initial), encoding="utf-8")
            script = root / "fixture.ps1"
            module = (REPO / "scripts/LocalAiConfiguration.psm1").as_posix().replace("'", "''")
            script.write_text(
                f"$ErrorActionPreference='Stop'\nImport-Module '{module}' -Force\n"
                "$store = Join-Path $PSScriptRoot 'secrets.json'\n" + command,
                encoding="utf-8",
            )
            result = subprocess.run([_find_pwsh(), "-NoProfile", "-File", str(script)], capture_output=True, text=True, timeout=20)
            after = json.loads(store.read_text(encoding="utf-8-sig"))
            self.assertNotIn("fixture-openai-secret", result.stdout + result.stderr)
            self.assertNotIn("fixture-other-secret", result.stdout + result.stderr)
            return result, after

    def test_masked_helper_merges_nested_and_flat_preserving_other_secrets(self):
        result, after = self.run_fixture(
            {"Authentication": {"Google": {"ClientSecret": "fixture-other-secret"}}, "SmtpSettings:Password": "fixture-other-secret", "Learning": {"Speech": {"ModelIdentity": "tiny.en"}}, "AI": {"BaseUrl": "https://generativelanguage.googleapis.com/v1beta", "Model": "gemini-2.5-flash-lite", "ApiKey": "old-fixture"}},
            "Set-LocalAiKey -SecretPath $store -SecureKey (ConvertTo-SecureString 'fixture-openai-secret' -AsPlainText -Force)\n",
        )
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual("fixture-openai-secret", after["AI:ApiKey"])
        self.assertEqual("fixture-other-secret", after["Authentication:Google:ClientSecret"])
        self.assertEqual("fixture-other-secret", after["SmtpSettings:Password"])
        self.assertEqual("tiny.en", after["Learning:Speech:ModelIdentity"])
        self.assertEqual("gpt-6-luna", after["AI:Model"])
        self.assertEqual("low", after["AI:ReasoningEffort"])
        self.assertEqual("https://api.openai.com/v1", after["AI:BaseUrl"])

    def test_launcher_normalization_preserves_existing_key_and_database(self):
        initial = {"AI:ApiKey": "fixture-openai-secret", "AI:Model": "gemini-2.5-flash-lite", "AI:BaseUrl": "https://generativelanguage.googleapis.com/v1beta/", "ConnectionStrings:DefaultConnection": "fixture-other-secret"}
        result, after = self.run_fixture(initial, "Update-LocalSecretStore -SecretPath $store -Update { param($values) Convert-LegacyAiSettings $values }\n")
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual(initial["AI:ApiKey"], after["AI:ApiKey"])
        self.assertEqual(initial["ConnectionStrings:DefaultConnection"], after["ConnectionStrings:DefaultConnection"])
        self.assertEqual("gpt-6-luna", after["AI:Model"])

    def test_custom_non_google_endpoint_preserved_and_unknown_model_rejected_atomically(self):
        initial = {"AI:BaseUrl": "https://custom.test/v1", "AI:Model": "another-model", "AI:ApiKey": "fixture-openai-secret"}
        result, after = self.run_fixture(initial, "Update-LocalSecretStore -SecretPath $store -Update { param($values) Convert-LegacyAiSettings $values }\n")
        self.assertNotEqual(0, result.returncode)
        self.assertEqual(initial, after)

    def test_conflicting_nested_flat_setting_is_rejected_without_destroying_store(self):
        initial = {"AI": {"ApiKey": "fixture-other-secret"}, "AI:ApiKey": "fixture-openai-secret"}
        result, after = self.run_fixture(initial, "Set-LocalAiKey -SecretPath $store -SecureKey (ConvertTo-SecureString 'fixture-openai-secret' -AsPlainText -Force)\n")
        self.assertNotEqual(0, result.returncode)
        self.assertEqual(initial, after)

    def test_masked_entrypoint_has_no_plain_key_argument_or_dotnet_secrets_argv(self):
        source = (REPO / "scripts/set-local-ai-key.ps1").read_text(encoding="utf-8-sig")
        self.assertIn("Read-Host", source)
        self.assertIn("-AsSecureString", source)
        self.assertNotIn("user-secrets set", source)
        self.assertNotIn("[string]$ApiKey", source)

    def test_empty_teammate_store_needs_only_key_and_shared_source_defaults(self):
        result, after = self.run_fixture({}, "Set-LocalAiKey -SecretPath $store -SecureKey (ConvertTo-SecureString 'fixture-openai-secret' -AsPlainText -Force)\n")
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual("fixture-openai-secret", after["AI:ApiKey"])
        defaults = json.loads((REPO / "TCTEnglish/appsettings.json").read_text(encoding="utf-8-sig"))["AI"]
        self.assertEqual("gpt-6-luna", defaults["Model"])
        self.assertEqual("low", defaults["ReasoningEffort"])
        self.assertEqual("https://api.openai.com/v1", defaults["BaseUrl"])

    def test_environment_legacy_model_is_rejected_without_changing_store(self):
        result, after = self.run_fixture({"AI:ApiKey": "fixture-openai-secret"}, "$env:AI__Model='gemini-2.5-flash-lite'\nAssert-LocalAiEnvironment\n")
        self.assertNotEqual(0, result.returncode)
        self.assertEqual("fixture-openai-secret", after["AI:ApiKey"])

    def test_openai_environment_fallback_is_available_to_web_without_persisting_key(self):
        initial = {"SmtpSettings:Password": "fixture-other-secret"}
        result, after = self.run_fixture(initial, """
$env:AI__ApiKey=$null
$env:OPENAI_API_KEY='fixture-openai-secret'
$status = Initialize-LocalAiEnvironment -SecretPath $store
if ($env:AI__ApiKey -cne $env:OPENAI_API_KEY) { throw 'Web key was not mapped.' }
if (-not $status.Configured -or $status.Source -ne 'OPENAI_API_KEY') { throw 'Incorrect key source.' }
$status | ConvertTo-Json -Compress
""")
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual(initial, after)
        self.assertIn('OPENAI_API_KEY', result.stdout)

    def test_explicit_app_environment_key_has_priority_over_global_openai_key(self):
        result, after = self.run_fixture({"AI:ApiKey": "fixture-other-secret"}, """
$env:AI__ApiKey='fixture-app-env'
$env:OPENAI_API_KEY='fixture-openai-secret'
$status = Initialize-LocalAiEnvironment -SecretPath $store
if ($env:AI__ApiKey -cne 'fixture-app-env' -or $status.Source -ne 'AI__ApiKey') { throw 'Explicit app override was replaced.' }
""")
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual("fixture-other-secret", after["AI:ApiKey"])

    def test_user_secrets_key_has_priority_over_global_openai_key(self):
        result, after = self.run_fixture({"AI:ApiKey": "fixture-other-secret"}, """
$env:AI__ApiKey=$null
$env:OPENAI_API_KEY='fixture-openai-secret'
$status = Initialize-LocalAiEnvironment -SecretPath $store
if ($env:AI__ApiKey -or $status.Source -ne 'User Secrets' -or -not $status.Configured) { throw 'App secret was overridden.' }
""")
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual("fixture-other-secret", after["AI:ApiKey"])

    def test_missing_key_still_allows_local_learning_without_mapping(self):
        result, after = self.run_fixture({}, """
$env:AI__ApiKey=$null
$env:OPENAI_API_KEY=$null
$status = Initialize-LocalAiEnvironment -SecretPath $store
if ($status.Configured -or $env:AI__ApiKey -or $status.Source -ne 'missing') { throw 'Missing key reported as configured.' }
""")
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual({}, after)

    def test_explicit_empty_secret_does_not_map_lower_global_key(self):
        initial = {"AI:ApiKey": ""}
        result, after = self.run_fixture(initial, """
$env:AI__ApiKey=$null
$env:OPENAI_API_KEY='fixture-openai-secret'
$status = Initialize-LocalAiEnvironment -SecretPath $store
if ($null -ne [Environment]::GetEnvironmentVariable('AI__ApiKey', 'Process')) { throw 'Explicit empty secret fell through to global key.' }
if ($status.Configured -or $status.Source -ne 'User Secrets') { throw 'Empty explicit secret must remain unconfigured.' }
""")
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual(initial, after)

    def test_invalid_fallback_key_fails_without_exposing_or_persisting_it(self):
        for key in ("AIza-fixture-openai-secret", "fixture-openai-secret with whitespace"):
            with self.subTest(key_kind='legacy' if key.startswith('AIza') else 'whitespace'):
                result, after = self.run_fixture({}, f"""
$env:AI__ApiKey=$null
$env:OPENAI_API_KEY='{key}'
Initialize-LocalAiEnvironment -SecretPath $store
""")
                self.assertNotEqual(0, result.returncode)
                self.assertIn('Local AI key is invalid', result.stderr)
                self.assertEqual({}, after)


if __name__ == "__main__":
    unittest.main()
