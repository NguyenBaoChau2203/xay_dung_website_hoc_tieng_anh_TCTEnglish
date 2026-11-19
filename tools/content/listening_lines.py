"""Turn a staged caption file into sentence-level transcript lines for a Listening lesson.

Input : .appdata/local/content-build/listening/captions/<id>.json  (from ContentImport StageCaptions)
Output: .appdata/local/content-build/listening/lines/<id>.json     (speaker, text, start, end), numbered for review

Rules (plan 5.1): drop sound tags such as [Music]/[Applause] and bare "Music" tokens, take "[Name]" labels as the
speaker, merge caption fragments into sentences of at most 25 words, keep times monotonic, no line shorter than 1.2 s
unless it is the last one. The text is NOT rewritten here: casing and punctuation of auto captions are corrected by
hand afterwards (stored in the lesson manifest as `text`).

Usage: py tools/content/listening_lines.py <videoId> [--show]
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from lib.common import BUILD, read_json, write_json  # noqa: E402

TAG = re.compile(r"[\[\(♪][^\]\)]*[\]\)♪]")
SPEAKER = re.compile(r"^\s*\[([A-Z][A-Za-z.' -]{1,40})\]\s*")
MUSIC = re.compile(r"\b(?:Music|Silence|Applause)\b")
END = re.compile(r"[.?!][\"')\]]?$")


def build(video_id: str) -> list[dict]:
    src = read_json(BUILD / "listening" / "captions" / f"{video_id}.json")
    raw = []
    speaker = None
    for line in src["lines"]:
        text = line["text"]
        m = SPEAKER.match(text)
        if m:
            speaker = m.group(1)
        text = TAG.sub(" ", text)
        text = MUSIC.sub(" ", text)
        text = re.sub(r"\s+", " ", text).strip()
        if not text:
            continue
        raw.append({"speaker": speaker, "text": text, "start": line["start"], "end": line["end"]})
    out: list[dict] = []
    cur: dict | None = None
    for r in raw:
        if cur is None:
            cur = dict(r)
        else:
            cur["text"] += " " + r["text"]
            cur["end"] = r["end"]
            if r["speaker"]:
                cur["speaker"] = cur["speaker"] or r["speaker"]
        words = len(cur["text"].split())
        if END.search(cur["text"]) or words >= 25:
            out.append(cur)
            cur = None
    if cur:
        out.append(cur)
    # enforce monotonic times and a minimum length
    last_end = 0.0
    for o in out:
        o["start"] = max(o["start"], last_end)
        if o["end"] - o["start"] < 1.2:
            o["end"] = o["start"] + 1.2
        last_end = o["end"]
        o["speaker"] = o["speaker"] or "Narrator"
    return out


def main() -> None:
    vid = sys.argv[1]
    lines = build(vid)
    path = BUILD / "listening" / "lines" / f"{vid}.json"
    path.parent.mkdir(parents=True, exist_ok=True)
    write_json(path, lines)
    if "--show" in sys.argv:
        for i, l in enumerate(lines):
            print(f"{i:02d} {l['start']:6.1f}-{l['end']:6.1f} [{l['speaker']}] {l['text']}")
    print(f"{vid}: {len(lines)} lines, {sum(len(l['text'].split()) for l in lines)} words", file=sys.stderr)


if __name__ == "__main__":
    main()
