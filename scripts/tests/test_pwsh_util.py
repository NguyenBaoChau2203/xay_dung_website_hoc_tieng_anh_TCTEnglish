"""Regression: the first PowerShell start must not be charged to a fixture's own time budget.

CI evidence (Agent tooling checks, PR #130, ubuntu-latest): the first pwsh call took 20.0s
(timeout of the first fixture), the second 7.1s, every later call 0.7-1.5s. Fixtures keep their
20-25s limits; the one-time cold start is paid inside pwsh_util.find_pwsh() under a generous cap.
"""
import subprocess
import unittest
from pathlib import Path
from unittest import mock

import pwsh_util


class PwshWarmUpTests(unittest.TestCase):
    def setUp(self):
        # Fresh warm-up registry per test (create=True so the test also runs against code without one).
        patches = [
            mock.patch.object(pwsh_util, "_WARMED", set(), create=True),
            mock.patch.object(pwsh_util.shutil, "which", return_value="/fake/pwsh"),
            mock.patch.object(Path, "is_file", return_value=True),
        ]
        for patch in patches:
            patch.start()
            self.addCleanup(patch.stop)

    def run_mock(self, **kwargs):
        patch = mock.patch.object(
            subprocess, "run",
            return_value=subprocess.CompletedProcess(args=[], returncode=0, stdout="", stderr=""), **kwargs)
        run = patch.start()
        self.addCleanup(patch.stop)
        return run

    def test_first_call_warms_powershell_up_with_a_generous_timeout(self):
        run = self.run_mock()

        self.assertEqual("/fake/pwsh", pwsh_util.find_pwsh())

        self.assertEqual(1, run.call_count, "the cold start must be paid once, inside find_pwsh()")
        command = run.call_args.args[0]
        self.assertEqual("/fake/pwsh", command[0])
        self.assertIn("-NoProfile", command)
        self.assertGreaterEqual(run.call_args.kwargs["timeout"], 60)

    def test_warm_up_runs_once_per_executable(self):
        run = self.run_mock()

        pwsh_util.find_pwsh()
        pwsh_util.find_pwsh()
        pwsh_util.find_pwsh()

        self.assertEqual(1, run.call_count)

    def test_warm_up_timeout_is_reported_not_swallowed(self):
        self.run_mock(side_effect=subprocess.TimeoutExpired(cmd="pwsh", timeout=120))

        with self.assertRaises(RuntimeError) as raised:
            pwsh_util.find_pwsh()

        self.assertIn("cold start", str(raised.exception))
        self.assertIn("/fake/pwsh", str(raised.exception))

    def test_missing_powershell_still_fails_fast_without_warming_up(self):
        run = self.run_mock()
        with mock.patch.object(pwsh_util.shutil, "which", return_value=None), \
                mock.patch.object(Path, "is_file", return_value=False):
            with self.assertRaises(FileNotFoundError):
                pwsh_util.find_pwsh()

        run.assert_not_called()


if __name__ == "__main__":
    unittest.main()
