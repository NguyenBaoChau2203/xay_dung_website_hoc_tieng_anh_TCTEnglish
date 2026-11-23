#!/usr/bin/env python3
"""Download curated real stock photos for onboarding goal/topic covers (JPEG 480x360)."""
from __future__ import annotations

import io
import json
import urllib.request
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1] / "TCTEnglish" / "wwwroot" / "images" / "onboarding"
UA = {"User-Agent": "TCTEnglishLocalAssetPrep/1.0"}

TOPICS = {
    "A01": "https://images.unsplash.com/photo-1450778869180-41d0601e046e?auto=format&fit=crop&w=960&h=720&q=80",
    "A02": "https://images.pexels.com/photos/247502/pexels-photo-247502.jpeg?auto=compress&cs=tinysrgb&w=960&h=720&fit=crop",
    "A03": "https://images.unsplash.com/photo-1490474418585-ba9bad8fd0ea?auto=format&fit=crop&w=960&h=720&q=80",
    "A04": "https://images.unsplash.com/photo-1540420773420-3366772f4999?auto=format&fit=crop&w=960&h=720&q=80",
    "A05": "https://images.unsplash.com/photo-1546069901-ba9599a7e63c?auto=format&fit=crop&w=960&h=720&q=80",
    "A06": "https://images.unsplash.com/photo-1495474472287-4d71bcdd2085?auto=format&fit=crop&w=960&h=720&q=80",
    "A07": "https://images.unsplash.com/photo-1530210124550-912dc1381cb8?auto=format&fit=crop&w=960&h=720&q=80",
    "A08": "https://images.unsplash.com/photo-1489987707025-afc232f7ea0f?auto=format&fit=crop&w=960&h=720&q=80",
    "A09": "https://images.unsplash.com/photo-1609220136736-443140cffec6?auto=format&fit=crop&w=960&h=720&q=80",
    "A10": "https://images.unsplash.com/photo-1560448204-e02f11c3d0e2?auto=format&fit=crop&w=960&h=720&q=80",
    "A11": "https://images.unsplash.com/photo-1586023492125-27b2c045efd7?auto=format&fit=crop&w=960&h=720&q=80",
    "A12": "https://images.unsplash.com/photo-1556911220-bff31c812dba?auto=format&fit=crop&w=960&h=720&q=80",
    "A13": "https://images.unsplash.com/photo-1503676260728-1c00da094a0b?auto=format&fit=crop&w=960&h=720&q=80",
    "A14": "https://images.pexels.com/photos/6050329/pexels-photo-6050329.jpeg?auto=compress&cs=tinysrgb&w=960&h=720&fit=crop",
    "A15": "https://images.unsplash.com/photo-1449824913935-59a10b8d2000?auto=format&fit=crop&w=960&h=720&q=80",
    "A16": "https://images.unsplash.com/photo-1449965408869-eaa3f722e40d?auto=format&fit=crop&w=960&h=720&q=80",
    "A17": "https://images.unsplash.com/photo-1504608524841-42fe6f032b4b?auto=format&fit=crop&w=960&h=720&q=80",
    "A18": "https://images.unsplash.com/photo-1501785888041-af3ef285b470?auto=format&fit=crop&w=960&h=720&q=80",
    "A19": "https://images.pexels.com/photos/863988/pexels-photo-863988.jpeg?auto=compress&cs=tinysrgb&w=960&h=720&fit=crop",
    "A20": "https://images.unsplash.com/photo-1511512578047-dfb367046420?auto=format&fit=crop&w=960&h=720&q=80",
    "A21": "https://images.unsplash.com/photo-1511379938547-c1f69419868d?auto=format&fit=crop&w=960&h=720&q=80",
    "A22": "https://images.unsplash.com/photo-1550684848-fac1c5b4e853?auto=format&fit=crop&w=960&h=720&q=80",
    "A23": "https://images.unsplash.com/photo-1492562080023-ab3db95bfbce?auto=format&fit=crop&w=960&h=720&q=80",
    "A24": "https://images.unsplash.com/photo-1576091160399-112ba8d25d1d?auto=format&fit=crop&w=960&h=720&q=80",
    "B01": "https://images.unsplash.com/photo-1497366216548-37526070297c?auto=format&fit=crop&w=960&h=720&q=80",
    "B02": "https://images.unsplash.com/photo-1518770660439-4636190af475?auto=format&fit=crop&w=960&h=720&q=80",
    "B03": "https://images.unsplash.com/photo-1441974231531-c6227db76b6e?auto=format&fit=crop&w=960&h=720&q=80",
    "B04": "https://images.unsplash.com/photo-1436491865332-7a61a109cc05?auto=format&fit=crop&w=960&h=720&q=80",
    "B05": "https://images.unsplash.com/photo-1555529669-e69e7aa0ba9a?auto=format&fit=crop&w=960&h=720&q=80",
    "B06": "https://images.unsplash.com/photo-1556910103-1c02745aae4d?auto=format&fit=crop&w=960&h=720&q=80",
    "B07": "https://images.unsplash.com/photo-1571019614242-c5c5dee9f50b?auto=format&fit=crop&w=960&h=720&q=80",
    "B08": "https://images.unsplash.com/photo-1434030216411-0b793f4b4173?auto=format&fit=crop&w=960&h=720&q=80",
    "B09": "https://images.pexels.com/photos/518543/pexels-photo-518543.jpeg?auto=compress&cs=tinysrgb&w=960&h=720&fit=crop",
    "B10": "https://images.unsplash.com/photo-1480714378408-67cf0d13bc1b?auto=format&fit=crop&w=960&h=720&q=80",
    "B11": "https://images.unsplash.com/photo-1446776811953-b23d57bd21aa?auto=format&fit=crop&w=960&h=720&q=80",
    "B12": "https://images.unsplash.com/photo-1574629810360-7efbbe195018?auto=format&fit=crop&w=960&h=720&q=80",
    "B13": "https://images.unsplash.com/photo-1513364776144-60967b0f800f?auto=format&fit=crop&w=960&h=720&q=80",
    "B14": "https://images.unsplash.com/photo-1589829545856-d10d557cf95f?auto=format&fit=crop&w=960&h=720&q=80",
    "B15": "https://images.unsplash.com/photo-1529156069898-49953e39b3ac?auto=format&fit=crop&w=960&h=720&q=80",
    "B16": "https://images.unsplash.com/photo-1556761175-5973dc0f32e7?auto=format&fit=crop&w=960&h=720&q=80",
    "C01": "https://images.unsplash.com/photo-1529156069898-49953e39b3ac?auto=format&fit=crop&w=960&h=720&q=80",
    "C02": "https://images.pexels.com/photos/6129450/pexels-photo-6129450.jpeg?auto=compress&cs=tinysrgb&w=960&h=720&fit=crop",
    "C03": "https://images.unsplash.com/photo-1517248135467-4c7edcad34c4?auto=format&fit=crop&w=960&h=720&q=80",
    "C04": "https://images.unsplash.com/photo-1469854523086-cc02fe5d8800?auto=format&fit=crop&w=960&h=720&q=80",
    "C05": "https://images.unsplash.com/photo-1441986300917-64674bd600d8?auto=format&fit=crop&w=960&h=720&q=80",
    "C06": "https://images.unsplash.com/photo-1516321318423-f06f85e504b3?auto=format&fit=crop&w=960&h=720&q=80",
    "C07": "https://images.unsplash.com/photo-1566073771259-6a8506099945?auto=format&fit=crop&w=960&h=720&q=80",
    "C08": "https://images.unsplash.com/photo-1511632765486-a01980e01a18?auto=format&fit=crop&w=960&h=720&q=80",
}

GOALS = {
    "communication": "https://images.unsplash.com/photo-1573497019940-1c28c88b4f3e?auto=format&fit=crop&w=960&h=720&q=80",
    "travel": "https://images.unsplash.com/photo-1488646953014-85cb44e25828?auto=format&fit=crop&w=960&h=720&q=80",
    "work": "https://images.unsplash.com/photo-1497366216548-37526070297c?auto=format&fit=crop&w=960&h=720&q=80",
    "study": "https://images.unsplash.com/photo-1434030216411-0b793f4b4173?auto=format&fit=crop&w=960&h=720&q=80",
    "hobbies": "https://images.unsplash.com/photo-1511379938547-c1f69419868d?auto=format&fit=crop&w=960&h=720&q=80",
    "beginner": "https://images.unsplash.com/photo-1503676260728-1c00da094a0b?auto=format&fit=crop&w=960&h=720&q=80",
}


def save_cover(url: str, dest: Path) -> bool:
    try:
        req = urllib.request.Request(url, headers=UA)
        with urllib.request.urlopen(req, timeout=45) as resp:
            data = resp.read()
        img = Image.open(io.BytesIO(data)).convert("RGB")
        w, h = img.size
        target_ratio = 4 / 3
        if w / h > target_ratio:
            nw = int(h * target_ratio)
            left = (w - nw) // 2
            img = img.crop((left, 0, left + nw, h))
        else:
            nh = int(w / target_ratio)
            top = (h - nh) // 2
            img = img.crop((0, top, w, top + nh))
        img = img.resize((480, 360), Image.Resampling.LANCZOS)
        dest.parent.mkdir(parents=True, exist_ok=True)
        img.save(dest, "JPEG", quality=82, optimize=True)
        return True
    except Exception as exc:  # noqa: BLE001 — report and continue
        print("FAIL", dest.name, exc)
        return False


def main() -> None:
    ok = fail = skipped = 0
    for key, url in {**{f"covers/{k}": v for k, v in TOPICS.items()}, **{f"goals/{k}": v for k, v in GOALS.items()}}.items():
        dest = ROOT / f"{key}.jpg"
        if dest.is_file() and dest.stat().st_size > 5000:
            skipped += 1
            print("SKIP", dest.relative_to(ROOT))
            continue
        if save_cover(url, dest):
            ok += 1
            print("OK", dest.relative_to(ROOT), dest.stat().st_size)
        else:
            fail += 1
    print(json.dumps({"ok": ok, "fail": fail, "skipped": skipped}))
    if fail:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
