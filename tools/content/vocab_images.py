"""Find, download, convert and review licensed images for system vocabulary cards.

Local authoring tool. Providers: Openverse and Wikimedia Commons, restricted to CC0 / public-domain
works. The card detail view only shows a credit line for Pixabay, so images that need attribution
(CC BY, CC BY-SA) are never used. Nothing is hotlinked: every file is downloaded, converted to WebP
480x360 and stored under the ignored `.appdata/local/content-build`.

Usage (from the repository root):
  py tools/content/vocab_images.py fetch  <manifest.json> [--per 5] [--only a,b]
  py tools/content/vocab_images.py sheet  <manifest.json> [--rows 8]
  py tools/content/vocab_images.py pick   <manifest.json> "term=1,term=0"
  py tools/content/vocab_images.py status <manifest.json>
"""
from __future__ import annotations

import argparse
import io
import re
import sys
import urllib.parse
from datetime import date
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from lib.common import BUILD, http_get, http_json, log, read_json, sha256_bytes, slug, write_json  # noqa: E402

from PIL import Image, ImageDraw, ImageFont, ImageOps  # noqa: E402

TARGET = (480, 360)
SOFT_CAP, HARD_CAP = 45_000, 80_000
MIN_SHORT_SIDE = 640
RAW_LIMIT = 12_000_000
BAD_TITLE = re.compile(r"\b(logo|icon|vector|diagram|map|flag|stamp|poster|cover|screenshot|scan|clipart|drawing of|coat of arms|signature|banner)\b", re.I)
OPENVERSE_ALLOWED = {"cc0": "CC0", "pdm": "Public Domain Mark"}
COMMONS_OK = re.compile(r"^(cc0|public domain|pd[- ])", re.I)


def font(size: int):
    for name in ("C:/Windows/Fonts/arial.ttf", "C:/Windows/Fonts/segoeui.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            continue
    return ImageFont.load_default()


def terms_of(query: str) -> list[str]:
    return [w for w in re.findall(r"[a-z]+", query.lower()) if len(w) > 2]


def relevance(query: str, title: str, tags: list[str]) -> int:
    words = terms_of(query)
    t = (title or "").lower()
    tg = " ".join(tags).lower()
    score = 0
    for w in words:
        stem = w[:-1] if w.endswith("s") and len(w) > 3 else w
        if re.search(r"\b" + re.escape(stem), t):
            score += 3
        if re.search(r"\b" + re.escape(stem), tg):
            score += 2
    if BAD_TITLE.search(t):
        score -= 4
    return score


def search_openverse(query: str, limit: int = 20) -> list[dict]:
    q = urllib.parse.urlencode({"q": query, "license": "cc0,pdm", "page_size": limit, "mature": "false"})
    data = http_json("https://api.openverse.org/v1/images/?" + q, "openverse", per_second=1.5)
    out = []
    for x in data.get("results", []):
        lic = (x.get("license") or "").lower()
        if lic not in OPENVERSE_ALLOWED or not x.get("url"):
            continue
        w, h = x.get("width") or 0, x.get("height") or 0
        tags = [t.get("name", "") for t in (x.get("tags") or [])]
        out.append({
            "source": "openverse", "sourceProvider": x.get("provider") or x.get("source"),
            "title": x.get("title") or "", "url": x["url"], "thumb": x.get("thumbnail"),
            "pageUrl": x.get("foreign_landing_url") or x.get("url"), "author": x.get("creator") or "",
            "license": OPENVERSE_ALLOWED[lic], "licenseUrl": x.get("license_url") or "",
            "width": w, "height": h, "tags": tags,
        })
    return out


def search_commons(query: str, limit: int = 20) -> list[dict]:
    q = urllib.parse.urlencode({
        "action": "query", "format": "json", "generator": "search", "gsrnamespace": 6,
        "gsrsearch": f"{query} filetype:bitmap", "gsrlimit": limit, "prop": "imageinfo",
        "iiprop": "url|size|mime|extmetadata", "iiurlwidth": 800,
    })
    data = http_json("https://commons.wikimedia.org/w/api.php?" + q, "commons", per_second=1.5)
    out = []
    for page in (data.get("query", {}).get("pages") or {}).values():
        info = (page.get("imageinfo") or [None])[0]
        if not info or info.get("mime") not in ("image/jpeg", "image/png"):
            continue
        meta = info.get("extmetadata") or {}
        lic = (meta.get("LicenseShortName") or {}).get("value", "")
        if not COMMONS_OK.match(lic):
            continue
        if (meta.get("NonFree") or {}).get("value", "").lower() == "true":
            continue
        if re.search(r"trademark|logo", (meta.get("Restrictions") or {}).get("value", ""), re.I):
            continue
        author = re.sub(r"<[^>]+>", "", (meta.get("Artist") or {}).get("value", "")).strip()
        title = page.get("title", "").replace("File:", "")
        out.append({
            "source": "commons", "sourceProvider": "wikimedia", "title": title,
            "url": info.get("thumburl") or info["url"], "thumb": None, "pageUrl": info.get("descriptionurl") or "",
            "author": author[:100], "license": lic[:40], "licenseUrl": (meta.get("LicenseUrl") or {}).get("value", ""),
            "width": info.get("width") or 0, "height": info.get("height") or 0,
            "tags": [c.strip() for c in (meta.get("Categories") or {}).get("value", "").split("|")][:12],
        })
    return out


def usable(c: dict) -> bool:
    w, h = c["width"], c["height"]
    if min(w, h) < MIN_SHORT_SIDE:
        return False
    ratio = w / h if h else 0
    return 0.5 <= ratio <= 2.0


def to_webp(raw: bytes, top_bias: float = 0.4) -> tuple[bytes, int] | None:
    try:
        img = Image.open(io.BytesIO(raw))
        img = ImageOps.exif_transpose(img).convert("RGB")
    except Exception:
        return None
    tw, th = TARGET
    sw, sh = img.size
    scale = max(tw / sw, th / sh)
    img = img.resize((max(tw, round(sw * scale)), max(th, round(sh * scale))), Image.LANCZOS)
    left = (img.width - tw) // 2
    top = round((img.height - th) * top_bias)
    img = img.crop((left, top, left + tw, top + th))
    for quality in (78, 72, 66, 60, 54):
        buf = io.BytesIO()
        img.save(buf, "WEBP", quality=quality, method=6)
        if buf.tell() <= SOFT_CAP:
            return buf.getvalue(), quality
    if buf.tell() <= HARD_CAP:
        return buf.getvalue(), 54
    return None


def set_dir(manifest: dict) -> Path:
    return BUILD / "vocab-images" / manifest["set"]["code"].lower()


def cmd_fetch(path: Path, per: int, only: set[str] | None) -> None:
    manifest = read_json(path)
    folder = set_dir(manifest)
    folder.mkdir(parents=True, exist_ok=True)
    state_path = folder / "cands.json"
    state = read_json(state_path) if state_path.exists() else {}
    for card in manifest["cards"]:
        term = card["term"]
        if only and term not in only:
            continue
        if card.get("image"):
            continue
        have = state.get(term, [])
        if len(have) >= per:
            continue
        query = card.get("imageQuery") or term
        pool: list[dict] = []
        for finder in (search_openverse, search_commons):
            try:
                pool += finder(query)
            except Exception as exc:
                log(f"  {term}: {finder.__name__} failed: {type(exc).__name__}")
        seen = {c["url"] for c in have}
        pool = [c for c in pool if c["url"] not in seen and usable(c)]
        for c in pool:
            c["score"] = relevance(query, c["title"], c["tags"])
        pool.sort(key=lambda c: -c["score"])
        for c in pool:
            if len(have) >= per:
                break
            if c["score"] <= 0:
                break
            try:
                raw = http_get(c["url"], "img-" + urllib.parse.urlparse(c["url"]).netloc, per_second=3, max_bytes=RAW_LIMIT)
            except Exception:
                if not c.get("thumb"):
                    continue
                try:
                    raw = http_get(c["thumb"], "openverse", per_second=1.5)
                except Exception:
                    continue
            converted = to_webp(raw, card.get("topBias", 0.4))
            if not converted:
                continue
            data, quality = converted
            k = len(have)
            name = f"{slug(term)}_{k}.webp"
            (folder / name).write_bytes(data)
            c.update({"file": name, "bytes": len(data), "quality": quality, "sha256": sha256_bytes(data), "query": query})
            have.append(c)
        state[term] = have
        log(f"{manifest['set']['code']} {term}: {len(have)} candidates")
        write_json(state_path, state)


def cmd_sheet(path: Path, rows: int, cols: int = 3) -> None:
    manifest = read_json(path)
    folder = set_dir(manifest)
    state = read_json(folder / "cands.json") if (folder / "cands.json").exists() else {}
    out_dir = BUILD / "review"
    out_dir.mkdir(parents=True, exist_ok=True)
    cards = [c for c in manifest["cards"] if not c.get("image")]
    if not cards:
        log("nothing to review")
        return
    tw, th, label_w, pad = 240, 180, 250, 6
    f_big, f_small = font(17), font(13)
    pages = [cards[i:i + rows] for i in range(0, len(cards), rows)]
    for n, page in enumerate(pages, 1):
        sheet = Image.new("RGB", (label_w + cols * (tw + pad), len(page) * (th + pad)), "white")
        d = ImageDraw.Draw(sheet)
        for r, card in enumerate(page):
            y = r * (th + pad)
            d.rectangle((0, y, label_w - 2, y + th - 1), fill=(240, 240, 245))
            d.text((8, y + 8), card["term"], fill="black", font=f_big)
            d.text((8, y + 34), (card.get("definition") or "")[:28], fill=(60, 60, 60), font=f_small)
            d.text((8, y + 54), (card.get("imageQuery") or "")[:30], fill=(110, 110, 110), font=f_small)
            for k, cand in enumerate(state.get(card["term"], [])[:cols]):
                x = label_w + k * (tw + pad)
                im = Image.open(folder / cand["file"]).convert("RGB").resize((tw, th))
                sheet.paste(im, (x, y))
                d.rectangle((x, y, x + 78, y + 22), fill="black")
                d.text((x + 4, y + 3), f"{k} {cand['license'][:4]}", fill="white", font=f_small)
        target = out_dir / f"{manifest['set']['code'].lower()}-{n}.png"
        sheet.save(target)
        print(target)


def cmd_pick(path: Path, picks: str) -> None:
    manifest = read_json(path)
    folder = set_dir(manifest)
    state = read_json(folder / "cands.json")
    final = BUILD / "vocab-images" / "final" / manifest["set"]["code"].lower()
    final.mkdir(parents=True, exist_ok=True)
    by_term = {c["term"]: c for c in manifest["cards"]}
    for item in [p for p in picks.split(",") if p.strip()]:
        term, _, k = item.rpartition("=")
        term = term.strip()
        if term not in by_term:
            raise SystemExit(f"unknown term: {term}")
        cand = state[term][int(k)]
        data = (folder / cand["file"]).read_bytes()
        name = f"{manifest['set']['code'].lower()}/{slug(term)}.webp"
        (final / f"{slug(term)}.webp").write_bytes(data)
        by_term[term]["image"] = {
            "file": name, "provider": cand["sourceProvider"], "pageUrl": cand["pageUrl"], "author": cand["author"],
            "license": cand["license"], "licenseUrl": cand["licenseUrl"], "query": cand["query"],
            "sha256": sha256_bytes(data), "width": TARGET[0], "height": TARGET[1], "reviewedAt": date.today().isoformat(),
        }
    write_json(path, manifest)
    print("picked", len([c for c in manifest["cards"] if c.get("image")]), "of", len(manifest["cards"]))


def cmd_status(path: Path) -> None:
    manifest = read_json(path)
    done = [c["term"] for c in manifest["cards"] if c.get("image")]
    print(f"{manifest['set']['code']}: {len(done)}/{len(manifest['cards'])} images")


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    for name in ("fetch", "sheet", "pick", "status"):
        p = sub.add_parser(name)
        p.add_argument("manifest", type=Path)
        if name == "fetch":
            p.add_argument("--per", type=int, default=5)
            p.add_argument("--only", default="")
        if name == "sheet":
            p.add_argument("--rows", type=int, default=8)
        if name == "pick":
            p.add_argument("picks")
    a = ap.parse_args()
    if a.cmd == "fetch":
        cmd_fetch(a.manifest, a.per, {t for t in a.only.split(",") if t} or None)
    elif a.cmd == "sheet":
        cmd_sheet(a.manifest, a.rows)
    elif a.cmd == "pick":
        cmd_pick(a.manifest, a.picks)
    else:
        cmd_status(a.manifest)


if __name__ == "__main__":
    main()
