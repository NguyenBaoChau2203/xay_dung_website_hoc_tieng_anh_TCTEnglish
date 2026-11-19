"""Validate system vocabulary manifests (data/content/vocabulary/**/*.json) against the plan contract.

Usage:
  py tools/content/validate_vocab.py [--require-images] [--stage words|content] [paths...]

Stages:
  words   : term, partsOfSpeech, cefr, definition only (C2).
  content : every text field of the card contract (C3). Default.
Errors exit with code 1. Warnings are listed and never fail the run.
"""
from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from lib.common import iter_manifests, read_json  # noqa: E402

POS = {"noun", "verb", "adj", "adv", "pron", "prep", "conj", "interj", "det", "phrasal_verb", "idiom", "phrase", "num"}
CEFR = {"A1", "A2", "B1", "B2", "C1", "C2"}
LEVELS_BY_PREFIX = {"A": {"A1", "A2"}, "B": {"B1", "B2"}, "C": {"A1", "A2", "B1"}}
EXPECTED = {"A": 25, "B": 25, "C": 20}
EXAMPLE_MAX_WORDS = {"A": 12, "B": 20, "C": 14}
LICENSE_OK = re.compile(r"^(CC0|Public Domain Mark|Public domain|PD[- ]|Original work|Pixabay Content License$)", re.I)
IPA = re.compile(r"^/[^/\s][^/]*/$")
MOJIBAKE = re.compile(r"Ã.|â€|Ä[\u0080-¿]|�")
PLACEHOLDER = re.compile(r"\b(todo|tbd|lorem|xxx|placeholder)\b|\?\?\?", re.I)
HEX64 = re.compile(r"^[0-9a-f]{64}$")


def words(text: str) -> int:
    return len(text.split())


def contains_term(text: str, term: str) -> bool:
    first = re.sub(r"[^a-z' -]", "", term.lower()).split()
    if not first:
        return False
    lowered = text.lower()
    if len(first) > 1:  # phrase: accept when most of the words appear
        hits = sum(1 for w in first if w[: max(3, len(w) - 2)] in lowered)
        return hits >= max(1, len(first) - 1)
    stem = first[0]
    for cut in (0, 1, 2, 3):  # run -> running, happy -> happier, knife -> knives
        s = stem[: len(stem) - cut] if cut and len(stem) - cut >= 3 else stem
        if re.search(r"\b" + re.escape(s), lowered):
            return True
    return False


def check_file(path: Path, stage: str, require_images: bool, errors: list[str], warnings: list[str], stats: dict) -> None:
    rel = path.as_posix().split("data/content/vocabulary/")[-1]

    def err(msg: str, term: str | None = None) -> None:
        errors.append(f"{rel}{' / ' + term if term else ''}: {msg}")

    def warn(msg: str, term: str | None = None) -> None:
        warnings.append(f"{rel}{' / ' + term if term else ''}: {msg}")

    try:
        m = read_json(path)
    except Exception as exc:  # noqa: BLE001
        err(f"invalid JSON ({exc})")
        return
    if m.get("schemaVersion") != 1:
        err("schemaVersion must be 1")
    folder = m.get("folder") or ""
    s = m.get("set") or {}
    code = s.get("code") or ""
    if not folder:
        err("folder is empty")
    if not re.fullmatch(r"[ABC]\d{2}", code):
        err(f"set.code '{code}' must look like A01, B02 or C03")
    prefix = code[:1]
    for field in ("name", "description", "topic", "wordListBasis"):
        if not (s.get(field) or "").strip():
            err(f"set.{field} is empty")
    cards = m.get("cards") or []
    expected = s.get("expectedCount") or EXPECTED.get(prefix)
    if expected and len(cards) != expected:
        err(f"{len(cards)} cards, expected {expected}")
    seen: dict[str, int] = {}
    unverified = 0
    for i, c in enumerate(cards):
        term = (c.get("term") or "").strip()
        label = term or f"#{i}"
        if not term:
            err("empty term", label)
            continue
        key = term.lower()
        if key in seen:
            err("duplicate term inside the set", term)
        seen[key] = i
        stats["terms"].setdefault((folder, key), []).append(code)
        if len(term) > 100:
            err("term longer than 100 characters", term)
        pos = c.get("partsOfSpeech") or []
        if not pos or len(pos) > 6 or any(p not in POS for p in pos):
            err(f"partsOfSpeech invalid: {pos}", term)
        if prefix == "C" and pos != ["phrase"]:
            err("phrases must use partsOfSpeech ['phrase']", term)
        cefr = c.get("cefr")
        if cefr not in CEFR or (prefix in LEVELS_BY_PREFIX and cefr not in LEVELS_BY_PREFIX[prefix]):
            err(f"cefr '{cefr}' not allowed in a {prefix}-folder set", term)
        definition = (c.get("definition") or "").strip()
        if not definition:
            err("definition is empty", term)
        elif words(definition) > (14 if prefix == "C" else 10):
            err(f"definition has {words(definition)} words", term)
        elif words(definition) > (10 if prefix == "C" else 8):
            warn(f"definition has {words(definition)} words", term)
        for field, value in (("term", term), ("definition", definition)):
            if MOJIBAKE.search(value) or PLACEHOLDER.search(value):
                err(f"{field} looks corrupted or a placeholder", term)
        if stage == "words":
            continue
        # --- content stage ---
        for side, field in (("US", "ipaUs"), ("UK", "ipaUk")):
            v = (c.get(field) or "").strip()
            if v and not IPA.match(v):
                err(f"{field} must be IPA between slashes: '{v}'", term)
            if not v and prefix != "C":
                err(f"{field} missing", term)
        if c.get("ipaVerified") is not True and prefix != "C":  # phrases carry no IPA, so they are not counted
            unverified += 1
            warn("ipaVerified is not true", term)
        if c.get("ipaVerified") is True and not (c.get("ipaSource") or "").startswith("https://"):
            err("ipaVerified without an ipaSource URL", term)
        me = (c.get("meaningEn") or "").strip()
        if not me or words(me) > 15:
            err(f"meaningEn must be 1-15 words ({words(me)})", term)
        ex = c.get("examples") or []
        if not 2 <= len(ex) <= 3:
            err(f"needs 2-3 examples, has {len(ex)}", term)
        maxw = EXAMPLE_MAX_WORDS.get(prefix, 20)
        for j, e in enumerate(ex):
            t, vi = (e.get("text") or "").strip(), (e.get("vi") or "").strip()
            if not t or not vi:
                err(f"example {j + 1} lacks text or Vietnamese translation", term)
                continue
            if words(t) > maxw:
                err(f"example {j + 1} has {words(t)} words (max {maxw})", term)
            if j == 0 and not contains_term(t, term):
                err("first example does not contain the term", term)
            if MOJIBAKE.search(t + vi) or PLACEHOLDER.search(t + vi):
                err(f"example {j + 1} looks corrupted or a placeholder", term)
        col = c.get("collocations") or []
        if prefix == "B" and len(col) < 2:
            err("folder B cards need at least 2 collocations", term)
        elif prefix == "A" and not col:
            warn("no collocation", term)
        if any(not (x.get("text") and x.get("vi")) for x in col):
            err("collocation lacks text or vi", term)
        wf = c.get("wordFamily") or []
        if any(not (x.get("text") and x.get("pos") in POS) for x in wf):
            err("wordFamily item needs text and a valid pos", term)
        if "verb" in pos and not c.get("verbForms"):
            warn("verb without verbForms", term)
        if require_images:
            img = c.get("image") or {}
            if not img:
                err("image missing", term)
                continue
            for f in ("file", "provider", "pageUrl", "license", "query", "sha256", "reviewedAt"):
                if not img.get(f):
                    err(f"image.{f} missing", term)
            if not LICENSE_OK.match(str(img.get("license", ""))):
                err(f"image licence '{img.get('license')}' is not on the allowlist", term)
            if not str(img.get("pageUrl", "")).startswith("https://"):
                err("image.pageUrl must be https", term)
            if str(img.get("file", "")).startswith(("http", "/", "..")):
                err("image.file must be a relative file name, never a URL", term)
            if not HEX64.match(str(img.get("sha256", ""))):
                err("image.sha256 is not a sha256 hex digest", term)
            if (img.get("width"), img.get("height")) != (480, 360):
                err("image must be 480x360", term)
    stats["cards"] += len(cards)
    stats["unverified"] += unverified


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("paths", nargs="*", type=Path)
    ap.add_argument("--stage", choices=("words", "content"), default="content")
    ap.add_argument("--require-images", action="store_true")
    a = ap.parse_args()
    files = [p for p in a.paths if p.is_file()] or [f for p in a.paths if p.is_dir() for f in iter_manifests(p)] or iter_manifests()
    errors: list[str] = []
    warnings: list[str] = []
    stats: dict = {"terms": {}, "cards": 0, "unverified": 0}
    for f in files:
        check_file(f, a.stage, a.require_images, errors, warnings, stats)
    for (folder, term), codes in stats["terms"].items():  # same word in one folder only with a different sense
        if len(set(codes)) > 1:
            warnings.append(f"'{term}' appears in several sets of '{folder}': {', '.join(sorted(set(codes)))} (allowed only with a different sense)")
    if a.stage == "content" and stats["cards"] and stats["unverified"] / stats["cards"] > 0.03:
        errors.append(f"{stats['unverified']} of {stats['cards']} cards have unverified IPA (limit 3 %)")
    for w in warnings:
        print("WARN ", w)
    for e in errors:
        print("ERROR", e)
    print(f"{len(files)} files, {stats['cards']} cards, {len(errors)} errors, {len(warnings)} warnings")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
