"""Shared PowerShell discovery for script unit tests."""
from __future__ import annotations

import os
from pathlib import Path
import shutil
import subprocess

# One-time PowerShell cold start is paid here, not inside a fixture's own limit. On a fresh
# GitHub ubuntu runner the first pwsh calls took 20.0s and 7.1s, then 0.7-1.5s each, which
# exceeded the 20s fixture limits. Those limits are unchanged; only the cold start moves.
WARM_UP_TIMEOUT_SECONDS = 120
_WARM_UP_COMMAND = (
    "$null = Get-Module -ListAvailable; "
    "$null = ConvertTo-SecureString 'warm-up' -AsPlainText -Force"
)
_WARMED: set[str] = set()


def _warm_up(executable: str) -> None:
    if executable in _WARMED:
        return
    try:
        subprocess.run(
            [executable, "-NoProfile", "-NonInteractive", "-Command", _WARM_UP_COMMAND],
            capture_output=True, text=True, timeout=WARM_UP_TIMEOUT_SECONDS,
        )
    except subprocess.TimeoutExpired as error:
        raise RuntimeError(
            f"PowerShell cold start exceeded {WARM_UP_TIMEOUT_SECONDS}s for {executable}; "
            "the script tests cannot run reliably on this machine."
        ) from error
    _WARMED.add(executable)


def find_pwsh() -> str:
    candidates: list[str] = []
    which = shutil.which("pwsh")
    if which:
        candidates.append(which)
    windir = os.environ.get("WINDIR")
    if windir:
        candidates.append(str(Path(windir) / "System32" / "WindowsPowerShell" / "v1.0" / "powershell.exe"))
    program_files = os.environ.get("ProgramFiles")
    if program_files:
        candidates.append(str(Path(program_files) / "PowerShell" / "7" / "pwsh.exe"))
    for candidate in candidates:
        if candidate and Path(candidate).is_file():
            _warm_up(candidate)
            return candidate
    raise FileNotFoundError("pwsh/powershell was not found for script tests.")


# Backward-compatible alias used by older test modules during the Whisper retirement.
_find_pwsh = find_pwsh
