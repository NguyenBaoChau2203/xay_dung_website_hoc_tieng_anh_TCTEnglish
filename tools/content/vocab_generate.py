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



# ---------------------------------------------------------------- body diagrams (set A07)
FIG = (205, 214, 232)
HI = (255, 159, 28)
SKIN = (246, 214, 184)
FIG_LINE = (40, 44, 60)


def _cap(d, x0, y0, x1, y1, w, fill, line=True):
    """A capsule (thick rounded line) from (x0,y0) to (x1,y1) with width w, drawn in supersampled pixels."""
    if line:
        d.line([(x0, y0), (x1, y1)], fill=FIG_LINE, width=w + 2 * S)
    r = w // 2
    for (x, y) in ((x0, y0), (x1, y1)):
        d.ellipse((x - r - (S if line else 0), y - r - (S if line else 0), x + r + (S if line else 0), y + r + (S if line else 0)), fill=FIG_LINE if line else fill)
    d.line([(x0, y0), (x1, y1)], fill=fill, width=w)
    for (x, y) in ((x0, y0), (x1, y1)):
        d.ellipse((x - r, y - r, x + r, y + r), fill=fill)


def figure(part: str, back: bool = False) -> bytes:
    img, d = canvas()
    s = S
    cx = W * s // 2
    def c(name):
        return HI if name in part.split("+") else FIG
    # legs
    for sx in (-1, 1):
        _cap(d, cx + sx * 24 * s, 200 * s, cx + sx * 26 * s, 318 * s, 40 * s, c("leg"))
    # knees
    for sx in (-1, 1):
        kx, ky = cx + sx * 25 * s, 268 * s
        d.ellipse((kx - 17 * s, ky - 17 * s, kx + 17 * s, ky + 17 * s), fill=c("knee"), outline=FIG_LINE, width=2 * s)
    # feet
    for sx in (-1, 1):
        fx = cx + sx * 30 * s
        d.ellipse((fx - 28 * s, 322 * s, fx + 28 * s, 346 * s), fill=c("foot"), outline=FIG_LINE, width=2 * s)
    # arms
    for sx in (-1, 1):
        _cap(d, cx + sx * 58 * s, 98 * s, cx + sx * 84 * s, 160 * s, 24 * s, c("arm"))
        _cap(d, cx + sx * 84 * s, 160 * s, cx + sx * 96 * s, 222 * s, 22 * s, c("arm"))
        ex, ey = cx + sx * 84 * s, 160 * s
        d.ellipse((ex - 13 * s, ey - 13 * s, ex + 13 * s, ey + 13 * s), fill=c("elbow"), outline=FIG_LINE, width=2 * s)
        hx, hy = cx + sx * 98 * s, 236 * s
        d.ellipse((hx - 14 * s, hy - 14 * s, hx + 14 * s, hy + 14 * s), fill=FIG, outline=FIG_LINE, width=2 * s)
    # torso: upper part (chest) and lower part (stomach)
    d.rounded_rectangle((cx - 58 * s, 90 * s, cx + 58 * s, 205 * s), 22 * s, fill=FIG, outline=FIG_LINE, width=2 * s)
    d.rounded_rectangle((cx - 56 * s, 92 * s, cx + 56 * s, 146 * s), 20 * s, fill=c("chest") if not back else c("back"))
    d.rounded_rectangle((cx - 56 * s, 146 * s, cx + 56 * s, 203 * s), 18 * s, fill=c("stomach") if not back else c("back"))
    d.line([(cx - 56 * s, 146 * s), (cx + 56 * s, 146 * s)], fill=FIG_LINE if part not in ("chest", "stomach") else FIG_LINE, width=1)
    # shoulders
    for sx in (-1, 1):
        shx, shy = cx + sx * 56 * s, 102 * s
        d.ellipse((shx - 17 * s, shy - 17 * s, shx + 17 * s, shy + 17 * s), fill=c("shoulder"), outline=FIG_LINE, width=2 * s)
    # neck and head
    d.rectangle((cx - 12 * s, 66 * s, cx + 12 * s, 92 * s), fill=c("neck"), outline=FIG_LINE, width=2 * s)
    d.ellipse((cx - 30 * s, 20 * s, cx + 30 * s, 76 * s), fill=c("head"), outline=FIG_LINE, width=2 * s)
    if back:  # spine and shoulder blades tell the viewer this is the back
        d.line([(cx, 94 * s), (cx, 200 * s)], fill=FIG_LINE, width=2 * s)
        for sx in (-1, 1):
            d.ellipse((cx + sx * 30 * s - 14 * s, 104 * s, cx + sx * 30 * s + 14 * s, 140 * s), outline=FIG_LINE, width=2 * s)
    else:
        for sx in (-1, 1):
            d.ellipse((cx + sx * 11 * s - 3 * s, 42 * s, cx + sx * 11 * s + 3 * s, 48 * s), fill=FIG_LINE)
        d.arc((cx - 10 * s, 52 * s, cx + 10 * s, 64 * s), 20, 160, fill=FIG_LINE, width=2 * s)
    return finish(img)


def face_closeup(part: str) -> bytes:
    img, d = canvas()
    s = S
    cx, cy, r = W * s // 2, 186 * s, 150 * s
    on = lambda name: HI if name == part else None
    # ears
    for sx in (-1, 1):
        d.ellipse((cx + sx * r - 26 * s, cy - 30 * s, cx + sx * r + 26 * s, cy + 40 * s), fill=on("ear") or SKIN, outline=FIG_LINE, width=3 * s)
    d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=on("face") or SKIN, outline=FIG_LINE, width=3 * s)
    # hair
    hair = HI if part == "hair" else (70, 52, 44)
    d.chord((cx - r - 4 * s, cy - r - 4 * s, cx + r + 4 * s, cy + r + 4 * s), 196, 344, fill=hair, outline=FIG_LINE, width=3 * s)
    # eyes
    for sx in (-1, 1):
        ex, ey = cx + sx * 55 * s, cy - 20 * s
        d.ellipse((ex - 22 * s, ey - 14 * s, ex + 22 * s, ey + 14 * s), fill="white", outline=FIG_LINE, width=3 * s)
        d.ellipse((ex - 8 * s, ey - 8 * s, ex + 8 * s, ey + 8 * s), fill=FIG_LINE)
    # nose
    d.polygon([(cx, cy - 12 * s), (cx - 16 * s, cy + 38 * s), (cx + 16 * s, cy + 38 * s)], fill=on("nose") or SKIN, outline=FIG_LINE, width=3 * s)
    # mouth
    if part in ("tongue",):
        d.pieslice((cx - 48 * s, cy + 52 * s, cx + 48 * s, cy + 118 * s), 0, 180, fill=(120, 40, 50), outline=FIG_LINE, width=3 * s)
        d.pieslice((cx - 24 * s, cy + 84 * s, cx + 24 * s, cy + 124 * s), 0, 180, fill=(240, 120, 140), outline=FIG_LINE, width=3 * s)
    else:
        d.arc((cx - 52 * s, cy + 46 * s, cx + 52 * s, cy + 100 * s), 20, 160, fill=FIG_LINE, width=5 * s)
    return finish(img)


def tooth_icon() -> bytes:
    img, d = canvas()
    s = S
    cx = W * s // 2
    pts = [(cx - 70 * s, 70 * s), (cx - 30 * s, 50 * s), (cx, 66 * s), (cx + 30 * s, 50 * s), (cx + 70 * s, 70 * s),
           (cx + 78 * s, 140 * s), (cx + 55 * s, 200 * s), (cx + 40 * s, 300 * s), (cx + 14 * s, 300 * s), (cx + 6 * s, 220 * s),
           (cx - 6 * s, 220 * s), (cx - 14 * s, 300 * s), (cx - 40 * s, 300 * s), (cx - 55 * s, 200 * s), (cx - 78 * s, 140 * s)]
    d.polygon(pts, fill="white", outline=FIG_LINE)
    d.line(pts + [pts[0]], fill=FIG_LINE, width=5 * s, joint="curve")
    return finish(img)


def hand_closeup(part: str) -> bytes:
    """Stylised hand (finger) or foot (toe) with the digits highlighted."""
    img, d = canvas()
    s = S
    cx = W * s // 2
    if part == "finger":
        d.rounded_rectangle((cx - 80 * s, 190 * s, cx + 80 * s, 330 * s), 30 * s, fill=FIG, outline=FIG_LINE, width=3 * s)
        for i, (dx, top) in enumerate(((-66, 110), (-30, 60), (6, 48), (42, 62), (78, 118))):
            _cap(d, cx + dx * s, 200 * s, cx + dx * s, top * s + 18 * s, 28 * s, HI)
        _cap(d, cx - 95 * s, 250 * s, cx - 140 * s, 200 * s, 30 * s, FIG)
    else:  # toe: a foot seen from above with five highlighted toes
        d.ellipse((cx - 70 * s, 122 * s, cx + 70 * s, 330 * s), fill=FIG, outline=FIG_LINE, width=3 * s)
        for dx, dy, rr in ((-48, 120, 20), (-20, 100, 17), (8, 94, 15), (34, 100, 14), (56, 114, 12)):
            d.ellipse((cx + (dx - rr) * s, (dy - rr + 20) * s, cx + (dx + rr) * s, (dy + rr + 20) * s), fill=HI, outline=FIG_LINE, width=3 * s)
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
        kind, val = spec["kind"], spec["value"]
        if kind == "color":
            data = color_swatch(val)
        elif kind == "shape":
            data = shape_image(val)
        elif kind == "body":
            data = figure(val, back=(val == "back"))
        elif kind == "face":
            data = face_closeup(val)
        elif kind == "tooth":
            data = tooth_icon()
        elif kind == "hand":
            data = hand_closeup(val)
        else:
            raise SystemExit(f"unknown generate kind: {kind}")
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
