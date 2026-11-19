"""Shared helpers for the content authoring tools (local use only, never a startup seeder)."""
from __future__ import annotations

import hashlib
import json
import re
import sys
import time
import unicodedata
import urllib.error
import urllib.request
from pathlib import Path

USER_AGENT = "TCTEnglish-content-build/1.0 (local authoring tool; contact via repository owner)"
REPO = Path(__file__).resolve().parents[3]
BUILD = REPO / ".appdata" / "local" / "content-build"
VOCAB_DIR = REPO / "data" / "content" / "vocabulary"

for _stream in (sys.stdout, sys.stderr):  # Windows consoles default to cp1252
    try:
        _stream.reconfigure(encoding="utf-8")
    except Exception:  # noqa: BLE001
        pass

_last_call: dict[str, float] = {}


def slug(text: str) -> str:
    """ASCII lowercase slug used for file names and package keys."""
    norm = unicodedata.normalize("NFKD", text.replace("đ", "d").replace("Đ", "D"))
    norm = "".join(c for c in norm if not unicodedata.combining(c))
    return re.sub(r"[^a-z0-9]+", "-", norm.lower()).strip("-") or "x"


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def sha256_file(path: Path) -> str:
    return sha256_bytes(path.read_bytes())


def read_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def write_json(path: Path, value) -> None:
    """Atomic UTF-8 (no BOM), LF, 2-space JSON write."""
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_suffix(path.suffix + ".tmp")
    tmp.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    for attempt in range(8):  # Windows: antivirus or the indexer can hold the target for a moment
        try:
            tmp.replace(path)
            return
        except PermissionError:
            if attempt == 7:
                raise
            time.sleep(0.25 * (attempt + 1))


def throttle(host_key: str, per_second: float = 2.0) -> None:
    gap = 1.0 / per_second
    wait = _last_call.get(host_key, 0) + gap - time.monotonic()
    if wait > 0:
        time.sleep(wait)
    _last_call[host_key] = time.monotonic()


def http_get(url: str, host_key: str, *, per_second: float = 2.0, max_bytes: int = 15_000_000, timeout: int = 40, retries: int = 3) -> bytes:
    """GET with a project User-Agent, a per-host rate limit, back-off on 429/5xx and a size cap."""
    delay = 2.0
    for attempt in range(retries + 1):
        throttle(host_key, per_second)
        req = urllib.request.Request(url, headers={"User-Agent": USER_AGENT, "Accept": "*/*"})
        try:
            with urllib.request.urlopen(req, timeout=timeout) as resp:
                data = resp.read(max_bytes + 1)
                if len(data) > max_bytes:
                    raise ValueError("response larger than the size cap")
                return data
        except urllib.error.HTTPError as exc:
            if exc.code in (429, 500, 502, 503, 504) and attempt < retries:
                time.sleep(delay)
                delay *= 2
                continue
            raise
        except (urllib.error.URLError, TimeoutError):
            if attempt < retries:
                time.sleep(delay)
                delay *= 2
                continue
            raise
    raise RuntimeError("unreachable")


def http_json(url: str, host_key: str, **kw):
    return json.loads(http_get(url, host_key, **kw).decode("utf-8"))


def redact(text: str, *secrets: str) -> str:
    for s in secrets:
        if s:
            text = text.replace(s, "<redacted>")
    return text


def iter_manifests(root: Path = VOCAB_DIR):
    return sorted(p for p in root.rglob("*.json") if p.is_file())


def log(*parts) -> None:
    print(*parts, file=sys.stderr, flush=True)
