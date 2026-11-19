"""Validate data/listening/system-a1-c2-2026-11.json against the Listening lesson contract (plan 5.1).

Usage: py tools/content/validate_listening.py [manifest.json]
Errors exit with code 1; warnings are listed.
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from lib.common import read_json  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
DEFAULT = ROOT / "data" / "listening" / "system-a1-c2-2026-11.json"
LEVELS = ["A1", "A2", "B1", "B2", "C1", "C2"]
SOUND_TAG = re.compile(r"\[[^\]]*\]|♪")
YT_ID = re.compile(r"^[A-Za-z0-9_-]{11}$")
BAD = re.compile(r"(?i:\b(?:todo|tbd|lorem|xxx|placeholder)\b)|\?\?\?|\ufffd|\u00c3.")


def words(t: str) -> int:
    return len(t.split())


def stem_in(word: str, text: str) -> bool:
    w = re.sub(r"[^a-z' -]", "", word.lower()).strip()
    if not w:
        return False
    parts = w.split()
    low = text.lower()
    if len(parts) > 1:
        return all(p[: max(3, len(p) - 2)] in low for p in parts)
    for cut in (0, 1, 2, 3):
        s = w[: len(w) - cut] if cut and len(w) - cut >= 3 else w
        if re.search(r"\b" + re.escape(s), low):
            return True
    return False


def main() -> int:
    path = Path(sys.argv[1]) if len(sys.argv) > 1 else DEFAULT
    m = read_json(path)
    errors: list[str] = []
    warnings: list[str] = []
    if m.get("schemaVersion") != 1:
        errors.append("schemaVersion must be 1")
    seen: set[str] = set()
    per_level: dict[str, int] = {}
    topics_per_level: dict[tuple[str, str], int] = {}
    for les in m.get("lessons", []):
        lid = les.get("youtubeId", "?")

        def err(msg: str) -> None:
            errors.append(f"{lid}: {msg}")

        if not YT_ID.match(lid):
            err("invalid YouTube id")
        if lid in seen:
            err("duplicate YouTube id")
        seen.add(lid)
        level = les.get("level")
        if level not in LEVELS:
            err(f"level {level}")
            continue
        per_level[level] = per_level.get(level, 0) + 1
        topics_per_level[(level, les.get("topic", ""))] = topics_per_level.get((level, les.get("topic", "")), 0) + 1
        title = les.get("title", "")
        if not title or len(title) > 60:
            err("title missing or longer than 60 characters")
        if not les.get("topic") or len(les["topic"]) > 100:
            err("topic missing or too long")
        dur = les.get("durationSeconds", 0)
        limit = 12 * 60 if level in ("A1", "A2", "B1") else 20 * 60
        if not 60 <= dur <= limit:
            err(f"duration {dur}s outside 60..{limit}")
        r = les.get("rights", {})
        if not str(r.get("policyUrl", "")).startswith("https://") or not r.get("itemUrl") or not r.get("quote"):
            err("rights evidence incomplete")
        ev = les.get("levelEvidence", {})
        if not ev.get("wordsPerMinute") or not ev.get("reason"):
            err("levelEvidence incomplete")
        if les.get("transcript", {}).get("source") != "youtube-captions+reviewed":
            err("transcript.source must be youtube-captions+reviewed")
        lines = les.get("lines", [])
        if not lines:
            err("no transcript lines")
        prev_start = prev_end = -1.0
        for i, ln in enumerate(lines):
            text, vi = ln.get("text", "").strip(), ln.get("vi", "").strip()
            if not text or not vi:
                err(f"line {i}: missing text or Vietnamese")
                continue
            if words(text) > 25:
                err(f"line {i}: {words(text)} words (max 25)")
            if SOUND_TAG.search(text):
                err(f"line {i}: sound tag in text")
            if BAD.search(text + vi):
                err(f"line {i}: corrupted text or placeholder")
            s, e = ln["start"], ln["end"]
            if s < 0 or s < prev_start or e <= s or e < prev_end or e - s < 1.19 or e > dur + 2:
                err(f"line {i}: invalid times {s}-{e}")
            prev_start, prev_end = s, e
        quiz = les.get("quiz", [])
        lo, hi = (5, 5) if level in ("A1", "A2") else (6, 8)
        if not lo <= len(quiz) <= hi:
            err(f"{len(quiz)} quiz questions (expected {lo}-{hi})")
        for qi, q in enumerate(quiz):
            opts = [q.get(k, "").strip() for k in ("optionA", "optionB", "optionC", "optionD")]
            if not q.get("question", "").strip() or any(not o for o in opts) or len(set(opts)) != 4:
                err(f"quiz {qi}: question or four distinct options required")
            if q.get("correct") not in ("A", "B", "C", "D"):
                err(f"quiz {qi}: correct answer must be A-D")
            if not re.search(r"—\s*\d\d:\d\d$", q.get("explanation", "")):
                err(f"quiz {qi}: explanation must quote the line and its time")
        vocab = les.get("vocab", [])
        if not 6 <= len(vocab) <= 10:
            err(f"{len(vocab)} vocabulary items (expected 6-10)")
        alltext = " ".join(ln.get("text", "") for ln in lines)
        for v in vocab:
            if not stem_in(v.get("word", ""), alltext):
                err(f"vocab '{v.get('word')}' does not appear in the transcript")
            if not v.get("definition") or not v.get("example"):
                err(f"vocab '{v.get('word')}' lacks definition or example")
            elif v["example"].lower().strip(" .") not in alltext.lower():
                warnings.append(f"{lid}: vocab example for '{v['word']}' is not a transcript sentence")
    for (level, topic), n in topics_per_level.items():
        if n > 3:
            warnings.append(f"level {level} has {n} lessons on topic '{topic}' (limit 3 per the plan)")
    print(f"{len(seen)} lessons; per level: " + ", ".join(f"{k}={per_level.get(k, 0)}" for k in LEVELS))
    for w in warnings:
        print("WARN ", w)
    for e in errors:
        print("ERROR", e)
    print(f"{len(errors)} errors, {len(warnings)} warnings")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
