"""Shared PowerShell discovery for script unit tests."""
from __future__ import annotations

import os
from pathlib import Path
import shutil


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
            return candidate
    raise FileNotFoundError("pwsh/powershell was not found for script tests.")


# Backward-compatible alias used by older test modules during the Whisper retirement.
_find_pwsh = find_pwsh
