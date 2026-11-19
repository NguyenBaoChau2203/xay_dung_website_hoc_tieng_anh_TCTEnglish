"""Write the repaired legacy images (D7) into TCTEnglish/wwwroot/images/<topic>/ and fix ImageUrl in the legacy JSON.

Reads data/content/legacy-images/l*.json (after fetch/pick/generate filled `image`), converts each final WebP to a
JPEG 480x360 at the card's `target` path, writes data/content/legacy-images/provenance.json, and edits ONLY the
ImageUrl of the legacy cards that had no local path (30 empty + 1 remote shop URL). Nothing is renamed or deleted.

Usage: py tools/content/legacy_export.py [--check]
"""
from __future__ import annotations

import io
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from lib.common import BUILD, read_json, write_json  # noqa: E402

from PIL import Image  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / "TCTEnglish" / "wwwroot" / "data" / "system-vocabulary.json"
WWW = ROOT / "TCTEnglish" / "wwwroot"
MANIFESTS = sorted((ROOT / "data" / "content" / "legacy-images").glob("l*.json"))
HARD_CAP = 80_000


def jpeg_bytes(webp: Path) -> bytes:
    img = Image.open(webp).convert("RGB")
    if img.size != (480, 360):
        raise SystemExit(f"{webp.name} is not 480x360")
    for q in (86, 80, 74, 68, 60):
        buf = io.BytesIO()
        img.save(buf, "JPEG", quality=q, optimize=True, progressive=True)
        if buf.tell() <= HARD_CAP:
            return buf.getvalue()
    raise SystemExit(f"{webp.name} cannot be compressed under {HARD_CAP} bytes")


def main() -> int:
    check = "--check" in sys.argv
    provenance, new_urls, problems = [], {}, []
    for path in MANIFESTS:
        m = read_json(path)
        code = m["set"]["code"].lower()
        for c in m["cards"]:
            img = c.get("image")
            if not img:
                problems.append(f"{m['set']['code']} / {c['term']}: no image yet")
                continue
            final = BUILD / "vocab-images" / "final" / img["file"]
            if not final.exists():
                problems.append(f"{m['set']['code']} / {c['term']}: final file missing")
                continue
            target = WWW / c["target"].lstrip("/").replace("/", "\\")
            if not check:
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(jpeg_bytes(final))
            if not c["hadImage"]:
                new_urls[(m["set"]["name"], c["term"])] = c["target"]
            provenance.append({"set": m["set"]["name"], "term": c["term"], "file": c["target"], "provider": img["provider"],
                               "pageUrl": img["pageUrl"], "author": img.get("author", ""), "license": img["license"], "sha256": img["sha256"]})
    print(f"{len(provenance)} images ready, {len(new_urls)} ImageUrl values to set, {len(problems)} problems")
    for p in problems:
        print("  PROBLEM", p)
    if problems or check:
        return 1 if problems else 0
    raw = SRC.read_bytes().decode("utf-8-sig")
    data = json.loads(raw)
    changed = 0
    for folder in data:
        for s in folder["Sets"]:
            for c in s["Cards"]:
                url = new_urls.get((s["SetName"], c["Term"]))
                if url and c.get("ImageUrl") != url:
                    c["ImageUrl"] = url
                    changed += 1
    out = json.dumps(data, ensure_ascii=False, indent=2).replace("\n", "\r\n")
    SRC.write_bytes(out.encode("utf-8"))
    write_json(ROOT / "data" / "content" / "legacy-images" / "provenance.json", provenance)
    print(f"updated {changed} ImageUrl values")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
