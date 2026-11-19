"""Expand a compact vocabulary source (.txt) into a manifest JSON that validate_vocab.py accepts.

The compact format keeps authoring cheap: one line per card, fields separated by " | ".

  SET B06 | Nấu ăn & chế biến | #b1 #b2 #cooking
  ---
  term | pos[,pos] | CEFR | định nghĩa tiếng Việt | English meaning | ex => vi ;; ex => vi | col => vi ;; col => vi | image query | {"verbForms": {...}}

The last field (JSON with extra keys such as verbForms, wordFamily, synonyms, antonyms, plural) and the
image query are optional. A line that starts with "//" is a comment. Existing manifests are never overwritten
unless --force is given, so reviewed images and IPA are not lost.

Usage: py tools/content/compact_to_manifest.py <source.txt> [--out <manifest.json>] [--force]
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from lib.common import slug, write_json  # noqa: E402

FOLDERS = {
    "A": ("a-co-ban", "Từ vựng theo chủ đề – Cơ bản (A1–A2)", "NGSL + CEFR-J A1-A2, topic judgement"),
    "B": ("b-trung-cap", "Từ vựng theo chủ đề – Trung cấp (B1–B2)", "NGSL + NAWL + CEFR-J, topic judgement"),
    "C": ("c-giao-tiep-hang-ngay", "Giao tiếp hằng ngày", "high-frequency spoken phrases, author-written"),
}
ROOT = Path(__file__).resolve().parents[2] / "data" / "content" / "vocabulary"


def split_pairs(field: str) -> list[dict]:
    out = []
    for part in field.split(";;"):
        part = part.strip()
        if not part:
            continue
        if "=>" not in part:
            raise SystemExit(f"missing '=>' in: {part}")
        en, vi = part.split("=>", 1)
        out.append({"text": en.strip(), "vi": vi.strip()})
    return out


def build(src: Path) -> tuple[Path, dict]:
    header = None
    cards = []
    for n, raw in enumerate(src.read_text(encoding="utf-8").splitlines(), 1):
        line = raw.strip()
        if not line or line.startswith("//"):
            continue
        if line.startswith("SET "):
            parts = [p.strip() for p in line[4:].split("|")]
            header = {"code": parts[0], "name": parts[1], "description": parts[2]}
            continue
        if line == "---":
            continue
        f = [p.strip() for p in line.split(" | ")]
        if len(f) < 7:
            raise SystemExit(f"{src.name}:{n}: expected at least 7 fields, got {len(f)}: {line[:60]}")
        term, pos, cefr, vi, en, ex, col = f[:7]
        card = {
            "term": term,
            "partsOfSpeech": [p.strip() for p in pos.split(",")],
            "cefr": cefr,
            "definition": vi,
            "meaningEn": en,
            "examples": split_pairs(ex),
            "collocations": split_pairs(col),
        }
        rest = f[7:]
        for item in rest:
            if item.startswith("{"):
                card.update(json.loads(item))
            elif item:
                card["imageQuery"] = item
        cards.append(card)
    if not header:
        raise SystemExit(f"{src.name}: no SET line")
    prefix = header["code"][0]
    folder_dir, folder_name, basis = FOLDERS[prefix]
    manifest = {
        "schemaVersion": 1,
        "folder": folder_name,
        "set": {**header, "topic": header["name"], "wordListBasis": basis},
        "cards": cards,
    }
    name = f"{header['code'].lower()}-{slug(header['name'])}.json"
    return ROOT / folder_dir / name, manifest


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("source", type=Path)
    ap.add_argument("--out", type=Path)
    ap.add_argument("--force", action="store_true")
    a = ap.parse_args()
    out, manifest = build(a.source)
    out = a.out or out
    if out.exists() and not a.force:
        print(f"{out} exists; use --force to overwrite", file=sys.stderr)
        return 1
    out.parent.mkdir(parents=True, exist_ok=True)
    write_json(out, manifest)
    print(f"wrote {out} ({len(manifest['cards'])} cards)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
