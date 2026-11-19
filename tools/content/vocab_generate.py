"""Draw original images for cards whose meaning is a colour or a geometric shape (set A22).

A search for the word "red" or "cube" returns random objects, so these cards get simple original
artwork drawn here with Pillow. The card carries `"generate": {"kind": "color"|"shape", "value": "..."}`;
the image provenance records provider `generated` and licence `Original work (generated)`.

Usage: py tools/content/vocab_generate.py <manifest.json>
"""
from __future__ import annotations

import io
import math
import sys
from datetime import date
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from lib.common import BUILD, read_json, sha256_bytes, slug, write_json  # noqa: E402

from PIL import Image, ImageDraw, ImageFilter  # noqa: E402

W, H, S = 480, 360, 3  # output size and supersampling factor
BG = (238, 240, 246)
OUTLINE = (40, 44, 60)
REPO_URL = "https://github.com/NguyenBaoChau2203/xay_dung_website_hoc_tieng_anh_TCTEnglish"

COLORS = {
    "red": (220, 38, 38), "blue": (37, 99, 235), "green": (22, 163, 74), "yellow": (250, 204, 21),
    "purple": (147, 51, 234), "pink": (244, 114, 182), "black": (22, 22, 24), "white": (255, 255, 255),
    "grey": (128, 128, 132), "brown": (120, 72, 36), "gold": (212, 175, 55), "silver": (192, 194, 198),
}
SHAPE_FILL = (255, 159, 28)
SHAPE_SIDE = (214, 120, 8)
SHAPE_TOP = (255, 196, 96)


def canvas() -> tuple[Image.Image, ImageDraw.ImageDraw]:
    img = Image.new("RGB", (W * S, H * S), BG)
    return img, ImageDraw.Draw(img)


def shade(rgb, k):
    return tuple(max(0, min(255, int(c * k))) for c in rgb)


def finish(img: Image.Image) -> bytes:
    out = img.resize((W, H), Image.LANCZOS)
    buf = io.BytesIO()
    out.save(buf, "WEBP", quality=82, method=6)
    return buf.getvalue()


def color_swatch(name: str) -> bytes:
    rgb = COLORS[name]
    img, d = canvas()
    x0, y0, x1, y1 = 90 * S, 60 * S, 390 * S, 300 * S
    shadow = Image.new("L", img.size, 0)
    ImageDraw.Draw(shadow).rounded_rectangle((x0 + 6 * S, y0 + 10 * S, x1 + 6 * S, y1 + 10 * S), 34 * S, fill=70)
    img.paste((120, 126, 150), mask=shadow.filter(ImageFilter.GaussianBlur(8 * S)))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((x0, y0, x1, y1), 34 * S, fill=rgb, outline=(210, 214, 224) if name == "white" else None, width=3 * S)
    if name in ("gold", "silver"):  # a soft diagonal highlight so the metallic colours read as shiny
        hl = Image.new("L", img.size, 0)
        ImageDraw.Draw(hl).polygon([(x0 + 40 * S, y1), (x0 + 120 * S, y1), (x0 + 220 * S, y0), (x0 + 140 * S, y0)], fill=90)
        mask = Image.new("L", img.size, 0)
        ImageDraw.Draw(mask).rounded_rectangle((x0, y0, x1, y1), 34 * S, fill=255)
        hl = Image.composite(hl, Image.new("L", img.size, 0), mask)
        img.paste((255, 255, 255), mask=hl)
    return finish(img)


def poly_star(cx, cy, r_out, r_in, n=5):
    pts = []
    for i in range(n * 2):
        ang = -math.pi / 2 + i * math.pi / n
        r = r_out if i % 2 == 0 else r_in
        pts.append((cx + r * math.cos(ang), cy + r * math.sin(ang)))
    return pts


def shape_image(name: str) -> bytes:
    img, d = canvas()
    cx, cy = W * S // 2, H * S // 2
    lw = 5 * S
    fill, side, top = SHAPE_FILL, SHAPE_SIDE, SHAPE_TOP
    if name == "circle":
        r = 120 * S
        d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=fill, outline=OUTLINE, width=lw)
    elif name == "square":
        r = 115 * S
        d.rectangle((cx - r, cy - r, cx + r, cy + r), fill=fill, outline=OUTLINE, width=lw)
    elif name == "triangle":
        d.polygon([(cx, cy - 125 * S), (cx + 140 * S, cy + 105 * S), (cx - 140 * S, cy + 105 * S)], fill=fill, outline=OUTLINE, width=lw)
    elif name == "rectangle":
        d.rectangle((cx - 165 * S, cy - 90 * S, cx + 165 * S, cy + 90 * S), fill=fill, outline=OUTLINE, width=lw)
    elif name == "oval":
        d.ellipse((cx - 170 * S, cy - 105 * S, cx + 170 * S, cy + 105 * S), fill=fill, outline=OUTLINE, width=lw)
    elif name == "star":
        d.polygon(poly_star(cx, cy + 10 * S, 140 * S, 58 * S), fill=(250, 204, 21), outline=OUTLINE, width=lw)
    elif name == "diamond":
        d.polygon([(cx, cy - 135 * S), (cx + 105 * S, cy), (cx, cy + 135 * S), (cx - 105 * S, cy)], fill=fill, outline=OUTLINE, width=lw)
    elif name == "cube":
        a = 95 * S
        dx, dy = 52 * S, 40 * S
        fx0, fy0 = cx - a - dx // 2, cy - a + dy // 2
        front = (fx0, fy0, fx0 + 2 * a, fy0 + 2 * a)
        d.polygon([(front[0], front[1]), (front[0] + dx, front[1] - dy), (front[2] + dx, front[1] - dy), (front[2], front[1])], fill=top, outline=OUTLINE, width=lw)
        d.polygon([(front[2], front[1]), (front[2] + dx, front[1] - dy), (front[2] + dx, front[3] - dy), (front[2], front[3])], fill=side, outline=OUTLINE, width=lw)
        d.rectangle(front, fill=fill, outline=OUTLINE, width=lw)
    elif name == "sphere":
        r = 125 * S
        for i in range(r, 0, -2):  # radial gradient with a highlight at the upper left
            k = i / r
            c = tuple(int(fill[j] * (0.55 + 0.45 * (1 - k)) + 255 * 0.25 * (1 - k) ** 2) for j in range(3))
            ox, oy = int((1 - k) * r * 0.35), int((1 - k) * r * 0.35)
            d.ellipse((cx - i - ox, cy - i - oy, cx + i - ox, cy + i - oy), fill=tuple(min(255, v) for v in c))
        d.ellipse((cx - r, cy - r, cx + r, cy + r), outline=OUTLINE, width=lw)
    elif name == "cone":
        rx, ry, h = 105 * S, 30 * S, 215 * S
        by = cy + h // 2 - 10 * S
        d.ellipse((cx - rx, by - ry, cx + rx, by + ry), fill=side, outline=OUTLINE, width=lw)
        d.polygon([(cx, by - h), (cx + rx, by), (cx - rx, by)], fill=fill)
        d.pieslice((cx - rx, by - ry, cx + rx, by + ry), 0, 180, fill=fill)
        d.line([(cx, by - h), (cx + rx, by)], fill=OUTLINE, width=lw)
        d.line([(cx, by - h), (cx - rx, by)], fill=OUTLINE, width=lw)
        d.arc((cx - rx, by - ry, cx + rx, by + ry), 0, 180, fill=OUTLINE, width=lw)
    elif name == "cylinder":
        rx, ry, h = 115 * S, 32 * S, 190 * S
        top_y, bot_y = cy - h // 2, cy + h // 2
        d.ellipse((cx - rx, bot_y - ry, cx + rx, bot_y + ry), fill=fill, outline=OUTLINE, width=lw)
        d.rectangle((cx - rx, top_y, cx + rx, bot_y), fill=fill)
        d.line([(cx - rx, top_y), (cx - rx, bot_y)], fill=OUTLINE, width=lw)
        d.line([(cx + rx, top_y), (cx + rx, bot_y)], fill=OUTLINE, width=lw)
        d.arc((cx - rx, bot_y - ry, cx + rx, bot_y + ry), 0, 180, fill=OUTLINE, width=lw)
        d.ellipse((cx - rx, top_y - ry, cx + rx, top_y + ry), fill=top, outline=OUTLINE, width=lw)
    elif name == "spiral":
        pts = []
        for i in range(0, 1500):
            t = i / 1500 * 4.5 * 2 * math.pi
            r = (t / (4.5 * 2 * math.pi)) * 135 * S
            pts.append((cx + r * math.cos(t), cy + r * math.sin(t)))
        d.line(pts, fill=(37, 99, 235), width=9 * S, joint="curve")
    elif name == "crescent":
        r = 120 * S
        mask = Image.new("L", img.size, 0)
        md = ImageDraw.Draw(mask)
        md.ellipse((cx - r, cy - r, cx + r, cy + r), fill=255)
        md.ellipse((cx - r + 55 * S, cy - r - 10 * S, cx + r + 55 * S, cy + r - 10 * S), fill=0)
        img.paste((250, 204, 21), mask=mask)
        edge = mask.filter(ImageFilter.FIND_EDGES).filter(ImageFilter.MaxFilter(2 * S + 1))
        img.paste(OUTLINE, mask=edge)
    else:
        raise SystemExit(f"unknown shape: {name}")
    return finish(img)


def main() -> None:
    path = Path(sys.argv[1])
    manifest = read_json(path)
    code = manifest["set"]["code"].lower()
    final = BUILD / "vocab-images" / "final" / code
    final.mkdir(parents=True, exist_ok=True)
    done = 0
    for card in manifest["cards"]:
        spec = card.get("generate")
        if not spec or card.get("image"):
            continue
        data = color_swatch(spec["value"]) if spec["kind"] == "color" else shape_image(spec["value"])
        name = f"{slug(card['term'])}.webp"
        (final / name).write_bytes(data)
        card["image"] = {
            "file": f"{code}/{name}", "provider": "generated", "pageUrl": REPO_URL, "author": "TCT English content tool",
            "license": "Original work (generated)", "licenseUrl": "", "query": f"generate:{spec['kind']}:{spec['value']}",
            "sha256": sha256_bytes(data), "width": W, "height": H, "reviewedAt": date.today().isoformat(),
        }
        done += 1
    write_json(path, manifest)
    print(f"{manifest['set']['code']}: generated {done} images in {final}")


if __name__ == "__main__":
    main()
