"""Assemble the system Listening manifest from lesson sources.

A lesson source (.tmp/content-fill/lessons/*.py) defines LESSONS = [dict(...)]. For each lesson this tool takes the
staged captions (listening_lines.build), applies the hand corrections in `fix`, attaches the Vietnamese lines `vi`
(one per transcript line), the quiz and the vocabulary list, derives the speech rate and writes
data/listening/system-a1-c2-2026-11.json (schemaVersion 1).

Usage: py tools/content/listening_build.py <lesson-source.py> [...]   (merges into the manifest by youtubeId)
"""
from __future__ import annotations

import importlib.util
import re
import sys
from datetime import date
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from lib.common import read_json, write_json  # noqa: E402
from listening_lines import build  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "data" / "listening" / "system-a1-c2-2026-11.json"
SPLITS_FILE = ROOT / ".tmp" / "content-fill" / "lessons" / "splits.py"  # authoring-time overrides (ignored)
NASA_POLICY = "https://www.nasa.gov/nasa-brand-center/images-and-media/"
NASA_QUOTE = "NASA content \"generally are not subject to copyright in the United States\" and may be used for educational or informational purposes (NASA Images and Media Usage Guidelines)."


def stem_in(word: str, text: str) -> bool:
    w = re.sub(r"[^a-z' -]", "", word.lower()).strip()
    parts = w.split()
    low = text.lower()
    if len(parts) > 1:
        return all(p[: max(3, len(p) - 2)] in low for p in parts)
    for cut in (0, 1, 2, 3):
        s = w[: len(w) - cut] if cut and len(w) - cut >= 3 else w
        if re.search(r"\b" + re.escape(s), low):
            return True
    return False


def mmss(seconds: float) -> str:
    s = int(seconds)
    return f"{s // 60:02d}:{s % 60:02d}"


def secs(duration: str) -> int:
    parts = [int(x) for x in duration.split(":")]
    return sum(v * 60 ** i for i, v in enumerate(reversed(parts)))


SENT_SPLIT = re.compile(r"(?<=[.?!])[\"')”]?\s+(?=[\"'(“]?[A-ZÀ-ỹ])")


def pack(sentences: list[str], limit: int = 25) -> list[list[int]]:
    """Greedy grouping of sentence indices into lines of at most `limit` English words."""
    groups: list[list[int]] = []
    cur: list[int] = []
    count = 0
    for i, sent in enumerate(sentences):
        w = len(sent.split())
        if cur and count + w > limit:
            groups.append(cur)
            cur, count = [], 0
        cur.append(i)
        count += w
    if cur:
        groups.append(cur)
    return groups


def expand(src_id: str, idx: int, text: str, vi: str) -> list[tuple[str, str]]:
    """Split one caption line into lines of at most 25 words, keeping Vietnamese aligned."""
    if " || " in text:
        en = [t.strip() for t in text.split(" || ")]
        vn = [t.strip() for t in vi.split(" || ")]
        if len(en) != len(vn):
            raise ValueError(f"{src_id} line {idx}: {len(en)} English pieces but {len(vn)} Vietnamese pieces")
        return list(zip(en, vn))
    if len(text.split()) <= 25:
        return [(text, vi.replace(" || ", " "))]
    sents = SENT_SPLIT.split(text)
    vsents = SENT_SPLIT.split(vi)
    if len(sents) != len(vsents):
        raise ValueError(f"{src_id} line {idx}: {len(sents)} English sentences but {len(vsents)} Vietnamese sentences; mark the split with ' || '")
    out = []
    for g in pack(sents):
        en = " ".join(sents[i] for i in g)
        if len(en.split()) > 25:
            raise ValueError(f"{src_id} line {idx}: one sentence has {len(en.split())} words; mark the split with ' || '")
        out.append((en, " ".join(vsents[i] for i in g)))
    return out


def lesson_record(src: dict) -> dict:
    lines = build(src["id"])
    for idx, text in src.get("fix", {}).items():
        lines[int(idx)]["text"] = text
    vi = list(src["vi"])
    splits = {}
    splits_file = SPLITS_FILE
    if splits_file and splits_file.exists():
        spec = importlib.util.spec_from_file_location("lesson_splits", splits_file)
        mod = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(mod)
        splits = mod.SPLITS.get(src["id"], {})
    for idx, (en_text, vi_text) in splits.items():
        lines[idx]["text"] = en_text
        vi[idx] = vi_text
    if len(vi) != len(lines):
        raise SystemExit(f"{src['id']}: {len(vi)} translations for {len(lines)} lines")
    out_lines: list[dict] = []
    first_of: dict[int, int] = {}
    problems: list[str] = []
    for i, (l, v) in enumerate(zip(lines, vi)):
        try:
            pieces = expand(src["id"], i, l["text"], v)
        except ValueError as exc:
            problems.append(str(exc))
            continue
        total_words = sum(len(e.split()) for e, _ in pieces)
        span = l["end"] - l["start"]
        t = l["start"]
        first_of[i] = len(out_lines)
        for j, (en, vn) in enumerate(pieces):
            frac = len(en.split()) / total_words
            end = l["end"] if j == len(pieces) - 1 else t + span * frac
            out_lines.append({"speaker": l["speaker"], "text": en, "vi": vn, "start": round(t, 1), "end": round(end, 1)})
            t = end
    if problems:
        raise SystemExit("\n".join(problems))
    words = sum(len(l["text"].split()) for l in out_lines)
    speech = sum(l["end"] - l["start"] for l in out_lines)
    wpm = round(words / speech * 60)

    def best_line(orig: int, q: tuple) -> dict:
        """Line (piece) of the original caption line that best matches the question and correct option."""
        lo = first_of[orig]
        hi = first_of.get(orig + 1, len(out_lines))
        if hi - lo == 1:
            return out_lines[lo]
        key = set(re.findall(r"[a-z]{4,}", (q[0] + " " + q[2 + "ABCD".index(q[5])]).lower()))
        return max(out_lines[lo:hi], key=lambda l: len(key & set(re.findall(r"[a-z]{4,}", l["text"].lower()))))

    quiz = []
    for q in src["quiz"]:
        text, a, b, c, d, correct, line_no = q
        line = best_line(line_no, q)
        quiz.append({"question": text, "optionA": a, "optionB": b, "optionC": c, "optionD": d, "correct": correct,
                     "explanation": f"\"{line['text']}\" — {mmss(line['start'])}"})
    vocab = []
    for w, d, _ignored in src["vocab"]:
        found = next((l["text"] for l in out_lines if stem_in(w, l["text"])), None)
        if found is None:
            raise SystemExit(f"{src['id']}: vocabulary word '{w}' does not appear in the transcript")
        vocab.append({"word": w, "definition": d, "example": found})
    return {
        "youtubeId": src["id"], "title": src["title"], "level": src["level"], "topic": src["topic"],
        "duration": src["duration"], "durationSeconds": secs(src["duration"]), "watchUrl": f"https://www.youtube.com/watch?v={src['id']}",
        "channelName": src["channel"],
        "rights": {"policyUrl": NASA_POLICY, "itemUrl": f"https://www.youtube.com/watch?v={src['id']}", "quote": NASA_QUOTE},
        "levelEvidence": {"wordsPerMinute": wpm, "reason": src["reason"].replace("{wpm}", str(wpm))},
        "transcript": {"source": "youtube-captions+reviewed", "retrievedAt": date.today().isoformat(), "corrections": src.get("fix_note", "")},
        "speakers": src.get("speakers", []),
        "lines": out_lines, "quiz": quiz, "vocab": vocab,
    }


def main() -> None:
    manifest = read_json(OUT) if OUT.exists() else {"schemaVersion": 1, "lessons": []}
    by_id = {l["youtubeId"]: l for l in manifest["lessons"]}
    for path in sys.argv[1:]:
        spec = importlib.util.spec_from_file_location("lesson_src", path)
        mod = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(mod)
        for src in mod.LESSONS:
            by_id[src["id"]] = lesson_record(src)
            print("built", src["id"], src["level"], by_id[src["id"]]["levelEvidence"]["wordsPerMinute"], "wpm")
    order = {"A1": 0, "A2": 1, "B1": 2, "B2": 3, "C1": 4, "C2": 5}
    manifest["lessons"] = sorted(by_id.values(), key=lambda l: (order[l["level"]], l["title"]))
    OUT.parent.mkdir(parents=True, exist_ok=True)
    write_json(OUT, manifest)
    print(f"{len(manifest['lessons'])} lessons in {OUT}")


if __name__ == "__main__":
    main()
