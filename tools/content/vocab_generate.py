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


GARMENT = {"dress": (219, 39, 119), "skirt": (37, 99, 235), "trousers": (71, 85, 105), "shorts": (14, 165, 233),
           "sweater": (234, 88, 12), "gloves": (124, 58, 237), "belt": (120, 72, 36), "tie": (190, 24, 93)}


def garment(name: str) -> bytes:
    """Flat original drawing of a clothing item (kind "garment")."""
    img, d = canvas()
    s = S
    cx = W * s // 2
    fill = GARMENT[name]
    dark = shade(fill, 0.72)
    line = shade(fill, 0.5)

    def poly(pts, col=fill):
        pp = [(cx + x * s, y * s) for x, y in pts]
        d.polygon(pp, fill=col)
        d.line(pp + [pp[0]], fill=line, width=3 * s, joint="curve")

    if name == "dress":
        poly([(-30, 40), (-14, 52), (14, 52), (30, 40), (44, 70), (28, 100), (50, 320), (-50, 320), (-28, 100), (-44, 70)])
        poly([(-28, 100), (28, 100), (24, 124), (-24, 124)], dark)
        d.arc((cx - 14 * s, 36 * s, cx + 14 * s, 68 * s), 0, 180, fill=line, width=3 * s)
    elif name == "skirt":
        poly([(-46, 90), (46, 90), (110, 300), (-110, 300)])
        poly([(-46, 90), (46, 90), (46, 112), (-46, 112)], dark)
        for k in (-60, -20, 20, 60):
            d.line((cx + k * s * 0.55, 116 * s, cx + k * s, 296 * s), fill=line, width=2 * s)
    elif name == "trousers":
        poly([(-60, 40), (60, 40), (72, 330), (14, 330), (0, 130), (-14, 330), (-72, 330)])
        poly([(-60, 40), (60, 40), (60, 62), (-60, 62)], dark)
        d.line((cx, 62 * s, cx, 130 * s), fill=line, width=3 * s)
    elif name == "shorts":
        poly([(-70, 90), (70, 90), (86, 250), (18, 250), (0, 160), (-18, 250), (-86, 250)])
        poly([(-70, 90), (70, 90), (70, 114), (-70, 114)], dark)
        d.line((cx, 114 * s, cx, 160 * s), fill=line, width=3 * s)
    elif name == "sweater":
        poly([(-60, 50), (-24, 40), (0, 62), (24, 40), (60, 50), (150, 190), (112, 214), (74, 140), (74, 300), (-74, 300), (-74, 140), (-112, 214), (-150, 190)])
        poly([(-24, 40), (0, 62), (24, 40), (24, 56), (0, 76), (-24, 56)], dark)
        poly([(-74, 280), (74, 280), (74, 300), (-74, 300)], dark)
        for k in range(-3, 4):
            d.line((cx + k * 18 * s, 90 * s, cx + k * 18 * s, 270 * s), fill=shade(fill, 0.85), width=2 * s)
    elif name == "gloves":
        for sign in (-1, 1):
            ox = sign * 90
            pts = [(ox - 40, 330), (ox - 44, 190), (ox - 52, 120), (ox - 40, 112), (ox - 26, 160), (ox - 24, 70), (ox - 10, 66), (ox - 4, 150),
                   (ox, 54), (ox + 14, 54), (ox + 16, 150), (ox + 26, 74), (ox + 40, 78), (ox + 34, 170), (ox + 54, 150), (ox + 66, 164), (ox + 44, 230), (ox + 40, 330)]
            if sign == 1:
                pts = [(2 * ox - x, y) for x, y in pts]
            poly(pts)
            poly([(ox - 42, 290), (ox + 40, 290), (ox + 40, 330), (ox - 42, 330)], dark)
    elif name == "belt":
        d.rounded_rectangle((cx - 190 * s, 150 * s, cx + 190 * s, 210 * s), 12 * s, fill=fill, outline=line, width=3 * s)
        for k in range(1, 6):
            d.ellipse((cx + (60 + k * 22) * s - 4 * s, 180 * s - 4 * s, cx + (60 + k * 22) * s + 4 * s, 180 * s + 4 * s), fill=BG, outline=line)
        d.rounded_rectangle((cx - 52 * s, 126 * s, cx + 20 * s, 234 * s), 14 * s, fill=None, outline=(214, 175, 55), width=14 * s)
        d.rounded_rectangle((cx - 52 * s, 126 * s, cx + 20 * s, 234 * s), 14 * s, fill=None, outline=shade((214, 175, 55), 0.7), width=2 * s)
        d.line((cx - 10 * s, 140 * s, cx - 10 * s, 220 * s), fill=(214, 175, 55), width=8 * s)
    elif name == "tie":
        poly([(-26, 40), (26, 40), (20, 84), (-20, 84)], dark)
        poly([(-20, 84), (20, 84), (62, 280), (0, 322), (-62, 280)])
        d.line((cx - 18 * s, 130 * s, cx - 38 * s, 270 * s), fill=shade(fill, 1.25), width=5 * s)
        d.line((cx + 18 * s, 130 * s, cx + 38 * s, 270 * s), fill=shade(fill, 1.25), width=5 * s)
    return finish(img)


SKIN = (250, 213, 172)
HAIR_DARK = (60, 44, 38)
HAIR_GREY = (176, 178, 184)
HI_FILL = (255, 159, 28)
LO_FILL = (176, 182, 196)
LINK = (120, 126, 142)


def _person(d, cx, top, scale, gender, fill, hair=HAIR_DARK, grey=False, glasses=False, cane=False, bun=False, star=False):
    """Flat cartoon person. (cx, top) is the top of the head in 1x pixels; scale 1.0 is an adult."""
    s = S * scale
    lw = max(2, int(2 * s))
    r = 15 * s
    hx, hy = cx * S, top * S + r
    body_top = hy + r * 0.95
    bw = 34 * s
    bh = 44 * s
    hair = HAIR_GREY if grey else hair
    if gender == "f":
        d.ellipse((hx - r * 1.12, hy - r * 1.1, hx + r * 1.12, hy + r * 1.5), fill=hair)
        pts = [(hx - bw * 0.42, body_top), (hx + bw * 0.42, body_top), (hx + bw * 0.78, body_top + bh * 1.15), (hx - bw * 0.78, body_top + bh * 1.15)]
        d.polygon(pts, fill=fill)
        d.line(pts + [pts[0]], fill=OUTLINE, width=lw, joint="curve")
    else:
        d.rounded_rectangle((hx - bw * 0.5, body_top, hx + bw * 0.5, body_top + bh * 0.72), int(6 * s), fill=fill, outline=OUTLINE, width=lw)
        for lx in (-0.42, 0.08):
            d.rectangle((hx + bw * lx, body_top + bh * 0.72, hx + bw * (lx + 0.34), body_top + bh * 1.15), fill=shade(fill, 0.7), outline=OUTLINE)
    d.ellipse((hx - r, hy - r, hx + r, hy + r), fill=SKIN, outline=OUTLINE, width=lw)
    if gender == "f":
        d.pieslice((hx - r * 1.05, hy - r * 1.1, hx + r * 1.05, hy + r * 1.0), 180, 360, fill=hair)
        if bun:
            d.ellipse((hx - r * 0.45, hy - r * 1.7, hx + r * 0.45, hy - r * 0.8), fill=hair, outline=OUTLINE)
    else:
        d.pieslice((hx - r * 1.02, hy - r * 1.08, hx + r * 1.02, hy + r * 0.7), 180, 360, fill=hair)
    for ex in (-0.38, 0.38):
        d.ellipse((hx + ex * r - 2 * s, hy - 1 * s, hx + ex * r + 2 * s, hy + 3 * s), fill=OUTLINE)
    d.arc((hx - r * 0.4, hy + r * 0.1, hx + r * 0.4, hy + r * 0.62), 20, 160, fill=OUTLINE, width=lw)
    if glasses:
        for ex in (-0.4, 0.4):
            d.ellipse((hx + ex * r - 5 * s, hy - 4 * s, hx + ex * r + 5 * s, hy + 6 * s), outline=OUTLINE, width=max(2, int(1.4 * s)))
    if cane:
        x = hx + bw * 0.95
        d.line((x, body_top + bh * 0.2, x, body_top + bh * 1.2), fill=(120, 72, 36), width=int(3 * s))
    if star:
        d.polygon(poly_star(hx, hy - r * 2.0, 9 * s, 4 * s), fill=(250, 204, 21), outline=OUTLINE)
    return hx, body_top + bh * 1.15


def family_tree(role: str) -> bytes:
    """Family tree with the relative that the word names highlighted in orange; the star marks "you"."""
    img, d = canvas()
    lw = 3 * S
    ox = 60
    nodes = {
        "grandfather": (120 + ox, 0, "m"), "grandmother": (180 + ox, 0, "f"),
        "aunt": (80 + ox, 1, "f"), "uncle": (150 + ox, 1, "m"), "father": (220 + ox, 1, "m"), "mother": (290 + ox, 1, "f"),
        "cousin": (150 + ox, 2, "m"), "brother": (215 + ox, 2, "m"), "me": (255 + ox, 2, "x"), "sister": (295 + ox, 2, "f"),
    }
    ys = {0: 20, 1: 140, 2: 262}
    hi = {
        "mother": {"mother"}, "father": {"father"}, "parents": {"father", "mother"}, "brother": {"brother"}, "sister": {"sister"},
        "grandmother": {"grandmother"}, "grandfather": {"grandfather"}, "uncle": {"uncle"}, "aunt": {"aunt"}, "cousin": {"cousin"},
        "son": {"brother"}, "daughter": {"sister"}, "husband": {"father"}, "wife": {"mother"}, "family": set(nodes),
    }[role]

    def px(key):
        return (240 + (nodes[key][0] - 245) * 1.7) * S

    def line(x0, y0, x1, y1):
        d.line((x0, y0, x1, y1), fill=LINK, width=lw)

    line(px("grandfather"), (ys[0] + 66) * S, px("grandmother"), (ys[0] + 66) * S)
    line(px("father"), (ys[1] + 66) * S, px("mother"), (ys[1] + 66) * S)
    gx = (px("grandfather") + px("grandmother")) / 2
    by = (ys[0] + 66) * S
    bar = (ys[1] - 16) * S
    line(gx, by, gx, bar)
    line(px("aunt"), bar, px("father"), bar)
    for k in ("aunt", "uncle", "father"):
        line(px(k), bar, px(k), ys[1] * S)
    mx = (px("father") + px("mother")) / 2
    bar2 = (ys[2] - 14) * S
    line(mx, (ys[1] + 66) * S, mx, bar2)
    line(px("brother"), bar2, px("sister"), bar2)
    for k in ("brother", "me", "sister"):
        line(px(k), bar2, px(k), ys[2] * S)
    line(px("uncle"), (ys[1] + 90) * S, px("uncle"), ys[2] * S)
    for key, (x, lvl, g) in nodes.items():
        on = key in hi
        child = lvl == 2
        _person(d, 240 + (x - 245) * 1.7, ys[lvl] + (6 if child else 0), 1.0 if child else 1.25, "f" if g == "f" else "m", HI_FILL if on else LO_FILL,
                grey=(lvl == 0), glasses=(key in ("grandfather", "grandmother")), bun=(key == "grandmother"), star=(key == "me"))
    return finish(img)


def people_scene(kind: str) -> bytes:
    """Single people (baby, child, boy ...) and small scenes (friend, neighbour, twins)."""
    img, d = canvas()
    s = S
    cx = W // 2
    if kind in ("man", "woman"):
        _person(d, cx, 14, 3.0, "m" if kind == "man" else "f", (37, 99, 235) if kind == "man" else (219, 39, 119))
    elif kind in ("boy", "girl"):
        _person(d, cx, 40, 2.7, "m" if kind == "boy" else "f", (22, 163, 74) if kind == "boy" else (234, 88, 12))
    elif kind == "child":
        _person(d, cx - 75, 70, 2.3, "m", (14, 165, 233))
        _person(d, cx + 75, 70, 2.3, "f", (234, 88, 12))
    elif kind == "teenager":
        _person(d, cx - 20, 18, 3.0, "m", (124, 58, 237))
        d.rounded_rectangle(((cx + 40) * s, 130 * s, (cx + 104) * s, 230 * s), 10 * s, fill=(250, 204, 21), outline=OUTLINE, width=3 * s)
    elif kind == "baby":
        hx, hy, r = cx * s, 150 * s, 62 * s
        d.ellipse((hx - 120 * s, 200 * s, hx + 120 * s, 340 * s), fill=(253, 224, 71), outline=OUTLINE, width=3 * s)
        d.ellipse((hx - r, hy - r, hx + r, hy + r), fill=SKIN, outline=OUTLINE, width=3 * s)
        d.arc((hx - 26 * s, hy + 6 * s, hx + 26 * s, hy + 44 * s), 20, 160, fill=OUTLINE, width=3 * s)
        for ex in (-22, 22):
            d.ellipse((hx + ex * s - 5 * s, hy - 12 * s, hx + ex * s + 5 * s, hy), fill=OUTLINE)
        d.arc((hx - 14 * s, hy - 52 * s, hx + 14 * s, hy - 30 * s), 180, 330, fill=OUTLINE, width=3 * s)
    elif kind == "twins":
        _person(d, cx - 75, 40, 2.7, "m", (37, 99, 235))
        _person(d, cx + 75, 40, 2.7, "m", (37, 99, 235))
    elif kind == "friend":
        a = _person(d, cx - 78, 40, 2.7, "m", (22, 163, 74))
        b = _person(d, cx + 78, 40, 2.7, "f", (219, 39, 119))
        d.line((a[0] + 30 * s, 205 * s, b[0] - 30 * s, 205 * s), fill=OUTLINE, width=9 * s)
    elif kind == "teacher":
        d.rounded_rectangle((20 * s, 30 * s, 300 * s, 250 * s), 12 * s, fill=(22, 101, 52), outline=(120, 72, 36), width=8 * s)
        for k, wd in enumerate((200, 150, 180)):
            d.line((50 * s, (80 + k * 40) * s, (50 + wd) * s, (80 + k * 40) * s), fill=(240, 244, 240), width=4 * s)
        a = _person(d, 380, 40, 2.5, "f", (124, 58, 237))
        d.line((a[0] - 48 * s, 200 * s, 270 * s, 150 * s), fill=(120, 72, 36), width=6 * s)
    elif kind == "student":
        _person(d, cx - 20, 30, 2.8, "m", (14, 165, 233))
        d.rounded_rectangle(((cx + 36) * s, 130 * s, (cx + 96) * s, 230 * s), 12 * s, fill=(234, 88, 12), outline=OUTLINE, width=3 * s)
        d.rectangle(((cx - 90) * s, 170 * s, (cx - 40) * s, 215 * s), fill=(253, 224, 71), outline=OUTLINE, width=3 * s)
        d.line(((cx - 65) * s, 170 * s, (cx - 65) * s, 215 * s), fill=OUTLINE, width=2 * s)
    elif kind == "neighbour":
        d.rectangle((0, 280 * s, W * s, H * s), fill=(187, 222, 176))
        for x0, col in ((20, (250, 204, 21)), (330, (125, 211, 252))):
            d.rectangle((x0 * s, 130 * s, (x0 + 130) * s, 290 * s), fill=col, outline=OUTLINE, width=3 * s)
            d.polygon([((x0 - 10) * s, 130 * s), ((x0 + 65) * s, 60 * s), ((x0 + 140) * s, 130 * s)], fill=(220, 38, 38), outline=OUTLINE)
            d.rectangle(((x0 + 50) * s, 210 * s, (x0 + 82) * s, 290 * s), fill=(120, 72, 36), outline=OUTLINE, width=2 * s)
        d.rectangle((170 * s, 240 * s, 310 * s, 252 * s), fill=(214, 175, 90), outline=OUTLINE)
        for x in range(176, 310, 28):
            d.rectangle((x * s, 222 * s, (x + 12) * s, 290 * s), fill=(214, 175, 90), outline=OUTLINE)
        _person(d, 110, 150, 1.4, "m", (22, 163, 74))
        _person(d, 370, 150, 1.4, "f", (219, 39, 119))
    return finish(img)


STEEL = (170, 178, 192)
STEEL_D = (96, 104, 120)


def kitchenware(name: str) -> bytes:
    """Flat original drawing of a kitchen item (kind "kitchen")."""
    img, d = canvas()
    s = S
    cx, cy = W * s // 2, H * s // 2
    L = OUTLINE
    w3 = 3 * s

    def ell(x0, y0, x1, y1, fill, width=3):
        d.ellipse((cx + x0 * s, cy + y0 * s, cx + x1 * s, cy + y1 * s), fill=fill, outline=L, width=width * s)

    def rect(x0, y0, x1, y1, fill, r=0, width=3):
        d.rounded_rectangle((cx + x0 * s, cy + y0 * s, cx + x1 * s, cy + y1 * s), r * s, fill=fill, outline=L, width=width * s)

    def poly(pts, fill):
        pp = [(cx + x * s, cy + y * s) for x, y in pts]
        d.polygon(pp, fill=fill)
        d.line(pp + [pp[0]], fill=L, width=w3, joint="curve")

    if name == "frying pan":
        rect(40, -12, 190, 12, (120, 72, 36), 10)
        ell(-130, -90, 50, 90, (60, 64, 76))
        ell(-112, -72, 32, 72, (38, 40, 50))
    elif name == "knife":
        poly([(-170, 8), (60, -26), (90, -14), (60, 14), (-170, 24)], STEEL)
        d.line((cx - 160 * s, cy + 8 * s, cx + 60 * s, cy - 18 * s), fill=(235, 238, 244), width=4 * s)
        rect(60, -22, 180, 18, (120, 72, 36), 12)
    elif name == "fork":
        rect(-24, 20, 24, 130, STEEL, 12)
        rect(-6, 100, 6, 100, STEEL)
        poly([(-46, -130), (-34, -130), (-34, -40), (-20, -40), (-20, -130), (-8, -130), (-8, -40), (8, -40), (8, -130), (20, -130), (20, -40), (34, -40), (34, -130), (46, -130), (46, -20), (28, 20), (-28, 20), (-46, -20)], STEEL)
        poly([(-18, 10), (18, 10), (14, 150), (-14, 150)], STEEL)
    elif name == "plate":
        ell(-140, -105, 140, 105, (248, 250, 252))
        ell(-98, -72, 98, 72, (226, 232, 240), 2)
    elif name == "kettle":
        poly([(-90, 90), (90, 90), (76, -10), (40, -60), (-40, -60), (-76, -10)], STEEL)
        poly([(-30, -60), (30, -60), (22, -86), (-22, -86)], STEEL_D)
        ell(-12, -104, 12, -84, (60, 64, 76))
        d.arc((cx - 90 * s, cy - 120 * s, cx + 100 * s, cy + 30 * s), 205, 335, fill=L, width=8 * s)
        poly([(76, -8), (130, -52), (140, -40), (88, 20)], STEEL)
        poly([(-90, 90), (90, 90), (84, 110), (-84, 110)], STEEL_D)
    elif name == "oven":
        rect(-130, -100, 130, 110, (226, 232, 240), 12)
        rect(-130, -100, 130, -60, (96, 104, 120), 8)
        for k in (-80, -40, 0, 40, 80):
            ell(k - 9, -90, k + 9, -70, (248, 250, 252), 2)
        rect(-104, -40, 104, 84, (38, 40, 50), 10)
        rect(-84, -22, 84, 62, (255, 190, 90), 8)
        rect(-70, -56, 70, -50, STEEL_D, 3)
    elif name == "spatula":
        rect(-14, -10, 14, 150, (120, 72, 36), 10)
        poly([(-70, -130), (70, -130), (70, -62), (14, -16), (-14, -16), (-70, -62)], (236, 120, 40))
        for k in (-36, 0, 36):
            d.line((cx + k * s, cy - 120 * s, cx + k * s, cy - 70 * s), fill=L, width=3 * s)
    elif name == "blender":
        poly([(-70, -120), (70, -120), (52, 40), (-52, 40)], (200, 230, 250))
        poly([(-70, -120), (70, -120), (74, -136), (-74, -136)], (60, 64, 76))
        poly([(-52, 40), (52, 40), (80, 120), (-80, 120)], (60, 64, 76))
        ell(-12, 66, 12, 90, (236, 120, 40), 2)
        poly([(-60, -20), (60, -20), (52, 40), (-52, 40)], (236, 90, 90))
        d.line((cx - 44 * s, cy + 30 * s, cx + 44 * s, cy + 30 * s), fill=L, width=3 * s)
    elif name == "tray":
        poly([(-170, 10), (170, 10), (140, 80), (-140, 80)], (120, 72, 36))
        poly([(-150, 0), (150, 0), (170, 10), (-170, 10)], (170, 110, 60))
        ell(-80, -60, 20, -2, (248, 250, 252), 2)
        rect(40, -70, 100, -2, (200, 230, 250), 6, 2)
    elif name == "apron":
        poly([(-30, -130), (30, -130), (44, -60), (100, 120), (-100, 120), (-44, -60)], (37, 99, 235))
        rect(-48, 20, 48, 84, (96, 140, 240), 8)
        d.arc((cx - 30 * s, cy - 156 * s, cx + 30 * s, cy - 90 * s), 180, 360, fill=L, width=4 * s)
        d.line((cx - 44 * s, cy - 60 * s, cx - 128 * s, cy - 40 * s), fill=L, width=5 * s)
        d.line((cx + 44 * s, cy - 60 * s, cx + 128 * s, cy - 40 * s), fill=L, width=5 * s)
    else:
        raise SystemExit(f"unknown kitchen item: {name}")
    return finish(img)


JOBS = {
    # name: (gender, body colour, headgear, prop)
    "nurse": ("f", (248, 250, 252), "nurse", "cross"), "doctor": ("m", (248, 250, 252), "none", "stethoscope"),
    "farmer": ("m", (22, 163, 74), "straw", "wheat"), "chef": ("m", (248, 250, 252), "chef", "pan"),
    "pilot": ("m", (30, 58, 138), "cap", "plane"), "mechanic": ("m", (14, 116, 144), "cap", "wrench"),
    "driver": ("m", (71, 85, 105), "cap", "wheel"), "police officer": ("m", (30, 58, 138), "cap", "badge"),
    "firefighter": ("m", (185, 28, 28), "helmet_red", "flame"), "dentist": ("f", (14, 165, 233), "none", "tooth"),
    "engineer": ("m", (234, 88, 12), "helmet_white", "blueprint"), "lawyer": ("f", (51, 65, 85), "none", "scales"),
    "waiter": ("m", (248, 250, 252), "none", "tray"), "cashier": ("f", (219, 39, 119), "none", "register"),
    "soldier": ("m", (77, 102, 56), "helmet_green", "flag"), "builder": ("m", (250, 204, 21), "helmet_yellow", "brick"),
    "electrician": ("m", (37, 99, 235), "helmet_yellow", "bolt"), "fisherman": ("m", (14, 116, 144), "bucket", "fish"),
    "baker": ("f", (248, 250, 252), "chef", "bread"), "photographer": ("f", (71, 85, 105), "none", "camera"),
    "tailor": ("f", (124, 58, 237), "none", "scissors"), "scientist": ("f", (248, 250, 252), "none", "flask"),
    "barber": ("m", (51, 65, 85), "none", "scissors"), "vet": ("f", (22, 163, 74), "none", "paw"),
    "postman": ("m", (220, 38, 38), "cap", "letter"),
}


def job_scene(name: str) -> bytes:
    """Cartoon person with the headgear and tool of a job (kind "job")."""
    gender, body, hat, prop = JOBS[name]
    img, d = canvas()
    s = S
    cx = 170
    _person(d, cx, 50, 2.6, gender, body)
    hx, hy, r = cx * s, (50 + 39) * s, 39 * s
    L = OUTLINE

    def box(x0, y0, x1, y1, fill, rr=0, w=3):
        d.rounded_rectangle((x0 * s, y0 * s, x1 * s, y1 * s), rr * s, fill=fill, outline=L, width=w * s)

    def oval(x0, y0, x1, y1, fill, w=3):
        d.ellipse((x0 * s, y0 * s, x1 * s, y1 * s), fill=fill, outline=L, width=w * s)

    def poly(pts, fill):
        pp = [(x * s, y * s) for x, y in pts]
        d.polygon(pp, fill=fill)
        d.line(pp + [pp[0]], fill=L, width=3 * s, joint="curve")

    # headgear
    top = 89 - 39
    if hat == "chef":
        box(cx - 34, top - 36, cx + 34, top + 6, (255, 255, 255), 16)
        box(cx - 38, top, cx + 38, top + 14, (255, 255, 255), 5)
    elif hat == "nurse":
        box(cx - 30, top - 6, cx + 30, top + 14, (255, 255, 255), 5)
        box(cx - 3, top - 2, cx + 3, top + 10, (220, 38, 38), 1, 1)
        box(cx - 8, top + 2, cx + 8, top + 6, (220, 38, 38), 1, 1)
    elif hat == "cap":
        d.pieslice((hx - r * 1.08, hy - r * 1.25, hx + r * 1.08, hy + r * 0.55), 180, 360, fill=body if body != (248, 250, 252) else (51, 65, 85), outline=L, width=3 * s)
        box(cx - 44, top + 12, cx + 44, top + 24, (30, 41, 59), 5)
    elif hat.startswith("helmet"):
        col = {"helmet_red": (220, 38, 38), "helmet_white": (255, 255, 255), "helmet_green": (77, 102, 56), "helmet_yellow": (250, 204, 21)}[hat]
        d.pieslice((hx - r * 1.12, hy - r * 1.3, hx + r * 1.12, hy + r * 0.7), 180, 360, fill=col, outline=L, width=3 * s)
        box(cx - 46, top + 14, cx + 46, top + 26, col, 5)
    elif hat == "straw":
        oval(cx - 62, top + 4, cx + 62, top + 30, (234, 179, 8))
        d.pieslice((hx - r * 0.85, hy - r * 1.3, hx + r * 0.85, hy + r * 0.2), 180, 360, fill=(250, 204, 21), outline=L, width=3 * s)
    elif hat == "bucket":
        poly([(cx - 40, top + 24), (cx - 32, top - 6), (cx + 32, top - 6), (cx + 40, top + 24)], (253, 224, 71))
        box(cx - 52, top + 20, cx + 52, top + 30, (253, 224, 71), 5)
    # tools
    px, py = 330, 190
    if prop == "stethoscope":
        d.arc((cx * s - 34 * s, 118 * s, cx * s + 34 * s, 190 * s), 0, 180, fill=(40, 44, 60), width=5 * s)
        oval(cx - 8, 186, cx + 8, 202, (170, 178, 192))
        return finish(img)
    if prop == "cross":
        box(px - 60, py - 50, px + 60, py + 50, (255, 255, 255), 8)
        box(px - 8, py - 34, px + 8, py + 34, (220, 38, 38), 2, 1)
        box(px - 34, py - 8, px + 34, py + 8, (220, 38, 38), 2, 1)
    elif prop == "wheat":
        for k in (-30, 0, 30):
            d.line(((px + k) * s, 120 * s, (px + k) * s, 260 * s), fill=(120, 72, 36), width=4 * s)
            for j in range(5):
                oval(px + k - 9, 110 + j * 14, px + k + 9, 126 + j * 14, (250, 204, 21), 2)
    elif prop == "pan":
        oval(px - 70, py - 40, px + 70, py + 40, (60, 64, 76))
        oval(px - 56, py - 30, px + 56, py + 30, (38, 40, 50), 2)
        box(px + 60, py - 7, px + 130, py + 7, (120, 72, 36), 6)
    elif prop == "plane":
        poly([(px - 90, py), (px + 80, py - 12), (px + 100, py), (px + 80, py + 12)], (226, 232, 240))
        poly([(px - 10, py - 4), (px + 30, py - 70), (px + 50, py - 70), (px + 30, py - 4)], (148, 163, 184))
        poly([(px - 10, py + 4), (px + 30, py + 70), (px + 50, py + 70), (px + 30, py + 4)], (148, 163, 184))
        poly([(px - 90, py), (px - 100, py - 34), (px - 76, py - 6)], (220, 38, 38))
    elif prop == "wrench":
        d.line((px - 60 * s, (py + 60) * s, (px + 50) * s, (py - 50) * s), fill=L, width=22 * s)
        d.line((px - 60 * s, (py + 60) * s, (px + 50) * s, (py - 50) * s), fill=(170, 178, 192), width=14 * s)
        oval(px + 28, py - 80, px + 82, py - 26, (170, 178, 192))
        oval(px + 44, py - 64, px + 66, py - 42, BG, 2)
    elif prop == "wheel":
        oval(px - 70, py - 70, px + 70, py + 70, BG, 12)
        oval(px - 16, py - 16, px + 16, py + 16, (71, 85, 105))
        for ang in (90, 210, 330):
            x2 = px + 62 * math.cos(math.radians(ang)); y2 = py + 62 * math.sin(math.radians(ang))
            d.line((px * s, py * s, x2 * s, y2 * s), fill=L, width=8 * s)
    elif prop == "badge":
        poly([(px + dx, py + dy) for dx, dy in [(0, -70), (20, -22), (70, -22), (30, 10), (44, 60), (0, 30), (-44, 60), (-30, 10), (-70, -22), (-20, -22)]], (250, 204, 21))
    elif prop == "flame":
        poly([(px, py - 80), (px + 50, py - 10), (px + 36, py + 50), (px - 36, py + 50), (px - 50, py - 10), (px - 20, py - 30)], (249, 115, 22))
        poly([(px, py - 20), (px + 24, py + 14), (px + 14, py + 50), (px - 14, py + 50), (px - 24, py + 14)], (253, 224, 71))
    elif prop == "tooth":
        poly([(px - 50, py - 50), (px - 20, py - 62), (px, py - 46), (px + 20, py - 62), (px + 50, py - 50), (px + 56, py), (px + 36, py + 50), (px + 22, py + 80), (px + 8, py + 80), (px, py + 30), (px - 8, py + 80), (px - 22, py + 80), (px - 36, py + 50), (px - 56, py)], (255, 255, 255))
    elif prop == "blueprint":
        box(px - 90, py - 50, px + 90, py + 50, (59, 130, 246), 6)
        for k in (-26, 0, 26):
            d.line(((px - 70) * s, (py + k) * s, (px + 70) * s, (py + k) * s), fill=(255, 255, 255), width=3 * s)
        box(px - 94, py - 56, px - 78, py + 56, (226, 232, 240), 8)
    elif prop == "scales":
        d.line((px * s, (py - 70) * s, px * s, (py + 70) * s), fill=L, width=6 * s)
        d.line(((px - 70) * s, (py - 56) * s, (px + 70) * s, (py - 56) * s), fill=L, width=6 * s)
        for sx in (-70, 70):
            poly([(px + sx - 30, py - 6), (px + sx + 30, py - 6), (px + sx, py + 20)], (250, 204, 21))
            d.line(((px + sx) * s, (py - 56) * s, (px + sx - 30) * s, (py - 6) * s), fill=L, width=2 * s)
            d.line(((px + sx) * s, (py - 56) * s, (px + sx + 30) * s, (py - 6) * s), fill=L, width=2 * s)
        box(px - 36, py + 64, px + 36, py + 76, (120, 72, 36), 4)
    elif prop == "tray":
        poly([(px - 100, py), (px + 100, py), (px + 80, py + 24), (px - 80, py + 24)], (170, 178, 192))
        oval(px - 50, py - 40, px + 10, py - 6, (255, 255, 255), 2)
        box(px + 28, py - 52, px + 68, py - 2, (200, 230, 250), 6, 2)
    elif prop == "register":
        box(px - 80, py - 10, px + 80, py + 60, (71, 85, 105), 8)
        box(px - 56, py - 60, px + 30, py - 10, (200, 230, 250), 4)
        for k in range(4):
            box(px - 60 + k * 34, py + 14, px - 36 + k * 34, py + 32, (226, 232, 240), 3, 2)
    elif prop == "flag":
        d.line((px * s, (py - 90) * s, px * s, (py + 80) * s), fill=L, width=6 * s)
        poly([(px, py - 90), (px + 100, py - 70), (px, py - 36)], (220, 38, 38))
    elif prop == "brick":
        for row in range(3):
            for col in range(2 + (row % 2)):
                x0 = px - 70 + col * 62 + (row % 2) * -31
                box(x0, py - 20 + row * 30, x0 + 58, py + 4 + row * 30, (194, 65, 12), 3, 2)
    elif prop == "bolt":
        poly([(px + 10, py - 90), (px - 50, py + 6), (px - 6, py + 6), (px - 22, py + 80), (px + 50, py - 20), (px + 4, py - 20)], (250, 204, 21))
    elif prop == "fish":
        poly([(px - 80, py), (px - 20, py - 40), (px + 50, py), (px - 20, py + 40)], (96, 165, 250))
        poly([(px + 50, py), (px + 96, py - 36), (px + 96, py + 36)], (59, 130, 246))
        oval(px - 62, py - 10, px - 50, py + 2, (255, 255, 255), 2)
    elif prop == "bread":
        oval(px - 90, py - 34, px + 90, py + 40, (217, 119, 6))
        for k in (-40, 0, 40):
            d.line(((px + k - 10) * s, (py - 22) * s, (px + k + 10) * s, (py + 10) * s), fill=(146, 64, 14), width=5 * s)
    elif prop == "camera":
        box(px - 80, py - 40, px + 80, py + 50, (51, 65, 85), 10)
        oval(px - 36, py - 34, px + 36, py + 38, (30, 41, 59))
        oval(px - 22, py - 20, px + 22, py + 24, (96, 165, 250))
        box(px - 70, py - 52, px - 30, py - 38, (51, 65, 85), 4)
    elif prop == "scissors":
        for sx in (-1, 1):
            d.line((px * s, py * s, (px + sx * 40) * s, (py - 80) * s), fill=(170, 178, 192), width=10 * s)
        oval(px - 50, py + 8, px - 8, py + 50, BG, 7)
        oval(px + 8, py + 8, px + 50, py + 50, BG, 7)
    elif prop == "flask":
        poly([(px - 24, py - 80), (px + 24, py - 80), (px + 24, py - 30), (px + 70, py + 50), (px - 70, py + 50), (px - 24, py - 30)], (200, 230, 250))
        poly([(px - 46, py + 10), (px + 46, py + 10), (px + 70, py + 50), (px - 70, py + 50)], (74, 222, 128))
    elif prop == "paw":
        oval(px - 40, py - 6, px + 40, py + 60, (120, 72, 36))
        for dx, dy in ((-52, -22), (-20, -52), (20, -52), (52, -22)):
            oval(px + dx - 14, py + dy - 16, px + dx + 14, py + dy + 16, (120, 72, 36))
    elif prop == "letter":
        box(px - 80, py - 50, px + 80, py + 50, (255, 255, 255), 6)
        d.line(((px - 78) * s, (py - 48) * s, px * s, (py + 6) * s), fill=L, width=3 * s)
        d.line(((px + 78) * s, (py - 48) * s, px * s, (py + 6) * s), fill=L, width=3 * s)
    return finish(img)


def place_scene(name: str) -> bytes:
    """Flat original drawing of a building or place (kind "place")."""
    img, d = canvas()
    s = S
    L = OUTLINE
    d.rectangle((0, 0, W * s, 270 * s), fill=(214, 236, 252))  # sky
    d.rectangle((0, 270 * s, W * s, H * s), fill=(176, 214, 160))  # grass
    d.rectangle((0, 296 * s, W * s, H * s), fill=(150, 156, 168))  # road

    def box(x0, y0, x1, y1, fill, rr=0, w=3):
        d.rounded_rectangle((x0 * s, y0 * s, x1 * s, y1 * s), rr * s, fill=fill, outline=L, width=w * s)

    def poly(pts, fill):
        pp = [(x * s, y * s) for x, y in pts]
        d.polygon(pp, fill=fill)
        d.line(pp + [pp[0]], fill=L, width=3 * s, joint="curve")

    def oval(x0, y0, x1, y1, fill, w=3):
        d.ellipse((x0 * s, y0 * s, x1 * s, y1 * s), fill=fill, outline=L, width=w * s)

    def windows(x0, y0, cols, rows, gx=44, gy=44, ww=26, wh=28, col=(147, 197, 253)):
        for r_ in range(rows):
            for c_ in range(cols):
                box(x0 + c_ * gx, y0 + r_ * gy, x0 + c_ * gx + ww, y0 + r_ * gy + wh, col, 2, 2)

    def door(x, w=34, h=56, col=(120, 72, 36)):
        box(x, 270 - h, x + w, 270, col, 3, 2)

    def cross(cx_, cy_, size, col):
        box(cx_ - size, cy_ - size * 0.32, cx_ + size, cy_ + size * 0.32, col, 1, 1)
        box(cx_ - size * 0.32, cy_ - size, cx_ + size * 0.32, cy_ + size, col, 1, 1)

    if name == "hospital":
        box(80, 60, 400, 270, (248, 250, 252), 4)
        windows(110, 140, 6, 2, 50, 46)
        door(223, 34, 56, (147, 197, 253))
        box(190, 30, 290, 70, (248, 250, 252), 6)
        cross(240, 50, 16, (220, 38, 38))
    elif name == "pharmacy":
        box(90, 110, 390, 270, (254, 243, 199), 4)
        poly([(80, 110), (400, 110), (400, 150), (80, 150)], (22, 163, 74))
        for k in range(8):
            if k % 2 == 0:
                poly([(80 + k * 40, 110), (120 + k * 40, 110), (120 + k * 40, 150), (80 + k * 40, 150)], (248, 250, 252))
        box(120, 170, 220, 250, (147, 197, 253), 3, 2)
        door(300, 40, 70, (147, 197, 253))
        cross(240, 80, 22, (22, 163, 74))
    elif name == "post office":
        box(80, 90, 400, 270, (254, 226, 226), 4)
        poly([(70, 90), (240, 40), (410, 90)], (185, 28, 28))
        windows(110, 140, 3, 1, 56, 40, 36, 44)
        door(300, 40, 66, (120, 72, 36))
        box(214, 58, 266, 86, (255, 255, 255), 3, 2)
        d.line((214 * s, 58 * s, 240 * s, 74 * s), fill=L, width=2 * s)
        d.line((266 * s, 58 * s, 240 * s, 74 * s), fill=L, width=2 * s)
        box(414, 220, 446, 270, (220, 38, 38), 6)
        box(420, 232, 440, 238, L, 1, 1)
    elif name == "police station":
        box(80, 90, 400, 270, (203, 213, 225), 4)
        box(80, 90, 400, 120, (30, 58, 138), 3)
        windows(110, 150, 6, 1, 50, 40, 32, 36)
        door(223, 34, 60, (30, 58, 138))
        pts = [(240 + dx * 0.55, 105 + dy * 0.55) for dx, dy in [(0, -28), (8, -9), (28, -9), (12, 3), (18, 24), (0, 11), (-18, 24), (-12, 3), (-28, -9), (-8, -9)]]
        poly(pts, (250, 204, 21))
    elif name == "hotel":
        box(150, 30, 330, 270, (253, 230, 138), 4)
        windows(164, 70, 4, 4, 40, 44, 28, 30)
        door(222, 36, 52, (120, 72, 36))
        box(214, 40, 266, 60, (255, 255, 255), 3, 2)
        box(222, 44, 226, 56, L, 1, 1)
        box(254, 44, 258, 56, L, 1, 1)
        box(222, 48, 258, 52, L, 1, 1)
    elif name == "cafe":
        box(120, 120, 360, 270, (254, 215, 170), 4)
        for k in range(6):
            poly([(110 + k * 42, 120), (152 + k * 42, 120), (152 + k * 42, 162), (110 + k * 42, 162)], (220, 38, 38) if k % 2 == 0 else (255, 255, 255))
        box(150, 180, 250, 250, (147, 197, 253), 3, 2)
        door(280, 40, 70, (120, 72, 36))
        oval(190, 70, 240, 108, (255, 255, 255))
        box(238, 80, 252, 98, (255, 255, 255), 6, 3)
        for k in (0, 1):
            d.line(((205 + k * 18) * s, 56 * s, (209 + k * 18) * s, 40 * s), fill=(150, 156, 168), width=3 * s)
    elif name == "museum":
        poly([(70, 90), (240, 30), (410, 90)], (226, 232, 240))
        for k in range(6):
            box(100 + k * 52, 96, 120 + k * 52, 250, (248, 250, 252), 2)
        box(70, 90, 410, 100, (226, 232, 240), 1)
        for k in range(3):
            box(60 - k * 6, 250 + k * 7, 420 + k * 6, 257 + k * 7, (203, 213, 225), 1, 2)
    elif name == "temple":
        d.rectangle((0, 270 * s, W * s, H * s), fill=(176, 214, 160))
        box(150, 200, 330, 270, (220, 38, 38), 3)
        door(222, 36, 52, (120, 72, 36))
        for k, (y, w) in enumerate(((200, 110), (140, 90), (88, 70))):
            poly([(240 - w - 20, y), (240 + w + 20, y), (240 + w, y - 14), (240 - w, y - 14)], (180, 83, 9))
            if k < 2:
                box(240 - w + 20, y - 54, 240 + w - 20, y - 14, (220, 38, 38), 2, 2)
        poly([(236, 74), (244, 74), (244, 44), (240, 36), (236, 44)], (250, 204, 21))
    elif name == "shopping mall":
        box(50, 100, 430, 270, (226, 232, 240), 4)
        box(70, 130, 410, 250, (147, 197, 253), 3)
        for k in range(1, 8):
            d.line(((70 + k * 42.5) * s, 130 * s, (70 + k * 42.5) * s, 250 * s), fill=L, width=2 * s)
        box(50, 100, 430, 126, (124, 58, 237), 3)
        box(214, 140, 266, 250, (200, 230, 250), 3, 2)
        box(208, 68, 272, 100, (250, 204, 21), 6)
        poly([(226, 78), (254, 78), (258, 96), (222, 96)], (255, 255, 255))
        d.arc((231 * s, 66 * s, 249 * s, 84 * s), 180, 360, fill=L, width=3 * s)
    elif name == "market":
        d.rectangle((0, 270 * s, W * s, H * s), fill=(214, 196, 160))
        for k, col in enumerate(((220, 38, 38), (22, 163, 74), (37, 99, 235))):
            x0 = 40 + k * 142
            for j in range(4):
                poly([(x0 + j * 26, 130), (x0 + j * 26 + 26, 130), (x0 + j * 26 + 26, 166), (x0 + j * 26, 166)], col if j % 2 == 0 else (255, 255, 255))
            d.line((x0 * s, 166 * s, x0 * s, 270 * s), fill=L, width=4 * s)
            d.line(((x0 + 104) * s, 166 * s, (x0 + 104) * s, 270 * s), fill=L, width=4 * s)
            box(x0 - 4, 220, x0 + 108, 270, (180, 120, 60), 3)
            for j in range(4):
                oval(x0 + 6 + j * 26, 198, x0 + 28 + j * 26, 220, [(239, 68, 68), (250, 204, 21), (74, 222, 128), (251, 146, 60)][(j + k) % 4], 2)
    elif name == "gas station":
        box(60, 70, 420, 96, (220, 38, 38), 4)
        for x in (110, 350):
            box(x, 96, x + 14, 270, (203, 213, 225), 2)
        for x in (200, 250):
            box(x, 160, x + 40, 270, (22, 163, 74), 6)
            box(x + 6, 172, x + 34, 196, (255, 255, 255), 3, 2)
            d.line(((x + 40) * s, 190 * s, (x + 62) * s, 210 * s), fill=L, width=5 * s)
    else:
        raise SystemExit(f"unknown place: {name}")
    return finish(img)


def vehicle_scene(name: str) -> bytes:
    """Flat original drawing of a vehicle (kind "vehicle")."""
    img, d = canvas()
    s = S
    L = OUTLINE

    def oval(x0, y0, x1, y1, fill, w=3):
        d.ellipse((x0 * s, y0 * s, x1 * s, y1 * s), fill=fill, outline=L, width=w * s)

    def poly(pts, fill):
        pp = [(x * s, y * s) for x, y in pts]
        d.polygon(pp, fill=fill)
        d.line(pp + [pp[0]], fill=L, width=3 * s, joint="curve")

    d.rectangle((0, 290 * s, W * s, H * s), fill=(176, 214, 160))
    if name == "cyclo":
        for cx in (90, 300):
            oval(cx - 46, 244 - 46 + 36, cx + 46, 244 + 46 + 36, BG, 5)
            oval(cx - 6, 274, cx + 6, 286, L, 1)
        oval(360, 230, 440, 310, BG, 5)
        poly([(130, 150), (250, 150), (262, 270), (104, 270)], (220, 38, 38))  # passenger seat in front
        poly([(130, 150), (250, 150), (210, 110), (150, 110)], (250, 204, 21))  # canopy
        d.line((300 * s, 280 * s, 400 * s, 270 * s), fill=L, width=5 * s)
        d.line((400 * s, 270 * s, 392 * s, 214 * s), fill=L, width=5 * s)
        oval(376, 150, 410, 184, (250, 213, 172))  # rider
        poly([(372, 184), (414, 184), (408, 232), (378, 232)], (37, 99, 235))
        d.line((250 * s, 268 * s, 300 * s, 280 * s), fill=L, width=5 * s)
    else:
        raise SystemExit(f"unknown vehicle: {name}")
    return finish(img)


def weather_icon(name: str) -> bytes:
    """Flat original weather icon (kind "weather")."""
    img, d = canvas()
    s = S
    L = OUTLINE
    d.rectangle((0, 0, W * s, H * s), fill=(214, 236, 252))

    def oval(x0, y0, x1, y1, fill, w=3):
        d.ellipse((x0 * s, y0 * s, x1 * s, y1 * s), fill=fill, outline=L, width=w * s)

    def poly(pts, fill):
        pp = [(x * s, y * s) for x, y in pts]
        d.polygon(pp, fill=fill)
        d.line(pp + [pp[0]], fill=L, width=3 * s, joint="curve")

    def sun(cx, cy, r, rays=True):
        if rays:
            for k in range(12):
                a = math.radians(k * 30)
                d.line(((cx + math.cos(a) * (r + 12)) * s, (cy + math.sin(a) * (r + 12)) * s, (cx + math.cos(a) * (r + 40)) * s, (cy + math.sin(a) * (r + 40)) * s), fill=(245, 158, 11), width=8 * s)
        oval(cx - r, cy - r, cx + r, cy + r, (250, 204, 21))

    def cloud(cx, cy, k=1.0, col=(255, 255, 255)):
        for dx, dy, rr in ((-50, 10, 38), (0, -14, 50), (52, 8, 40), (14, 22, 36), (-22, 24, 34)):
            oval(cx + (dx - rr) * k, cy + (dy - rr) * k, cx + (dx + rr) * k, cy + (dy + rr) * k, col)
        d.rounded_rectangle(((cx - 74 * k) * s, (cy + 4 * k) * s, (cx + 84 * k) * s, (cy + 54 * k) * s), int(24 * k * s), fill=col)

    def drops(cx, cy, n=5, col=(37, 99, 235)):
        for i in range(n):
            x = cx - 70 + i * 36
            y = cy + (i % 2) * 22
            poly([(x, y), (x - 8, y + 18), (x + 8, y + 18)], col)

    def bolt(cx, cy, k=1.0):
        poly([(cx + 10 * k, cy - 50 * k), (cx - 30 * k, cy + 6 * k), (cx - 2 * k, cy + 6 * k), (cx - 16 * k, cy + 52 * k), (cx + 30 * k, cy - 8 * k), (cx + 2 * k, cy - 8 * k)], (250, 204, 21))

    cx, cy = 240, 150
    if name == "sun":
        sun(cx, 180, 70)
    elif name == "weather":
        sun(190, 130, 56)
        cloud(280, 190, 1.2)
    elif name == "rain":
        cloud(cx, 120, 1.3, (203, 213, 225))
        drops(cx, 220, 6)
    elif name == "storm":
        cloud(cx, 100, 1.4, (100, 116, 139))
        drops(cx - 20, 230, 5)
        bolt(cx + 70, 200, 1.3)
    elif name == "thunder":
        cloud(cx, 90, 1.3, (148, 163, 184))
        bolt(cx, 215, 1.8)
    elif name == "wind":
        for k, (y, ln) in enumerate(((120, 260), (180, 320), (240, 220))):
            x0 = 80
            d.line((x0 * s, y * s, (x0 + ln) * s, y * s), fill=(100, 116, 139), width=10 * s)
            d.arc(((x0 + ln - 34) * s, (y - 54) * s, (x0 + ln + 34) * s, (y + 14) * s), 270, 450, fill=(100, 116, 139), width=10 * s)
    elif name == "breeze":
        for k, y in enumerate((140, 200, 250)):
            pts = [(70 + i * 12, y + math.sin(i / 2.2 + k) * 14) for i in range(32)]
            d.line([(x * s, yy * s) for x, yy in pts], fill=(100, 116, 139), width=7 * s, joint="curve")
        poly([(330, 120), (380, 90), (400, 130), (360, 160)], (74, 222, 128))
        d.line((340 * s, 150 * s, 392 * s, 104 * s), fill=L, width=3 * s)
    elif name == "temperature":
        d.rounded_rectangle((210 * s, 40 * s, 270 * s, 250 * s), 30 * s, fill=(255, 255, 255), outline=L, width=4 * s)
        oval(190, 220, 290, 320, (255, 255, 255), 4)
        oval(204, 234, 276, 306, (220, 38, 38), 2)
        d.rounded_rectangle((232 * s, 110 * s, 248 * s, 250 * s), 8 * s, fill=(220, 38, 38))
        for k in range(6):
            d.line((276 * s, (70 + k * 28) * s, 300 * s, (70 + k * 28) * s), fill=L, width=4 * s)
    elif name == "puddle":
        d.rectangle((0, 150 * s, W * s, H * s), fill=(203, 213, 225))
        oval(90, 210, 390, 310, (147, 197, 253), 4)
        oval(160, 232, 320, 290, (186, 220, 253), 2)
        oval(205, 246, 275, 276, (219, 234, 254), 2)
        poly([(240, 70), (222, 112), (258, 112)], (37, 99, 235))
        d.line((120 * s, 70 * s, 100 * s, 130 * s), fill=(37, 99, 235), width=5 * s)
        d.line((370 * s, 60 * s, 350 * s, 120 * s), fill=(37, 99, 235), width=5 * s)
    elif name == "season":
        for k, (col, a0) in enumerate(((  (74, 222, 128), 180), ((250, 204, 21), 270), ((251, 146, 60), 0), ((147, 197, 253), 90))):
            d.pieslice((140 * s, 40 * s, 340 * s, 240 * s), a0, a0 + 90, fill=col, outline=L, width=4 * s)
        oval(210, 110, 270, 170, (255, 255, 255), 4)
    else:
        raise SystemExit(f"unknown weather icon: {name}")
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
        elif kind == "garment":
            data = garment(val)
        elif kind == "family":
            data = family_tree(val)
        elif kind == "people":
            data = people_scene(val)
        elif kind == "kitchen":
            data = kitchenware(val)
        elif kind == "job":
            data = job_scene(val)
        elif kind == "place":
            data = place_scene(val)
        elif kind == "vehicle":
            data = vehicle_scene(val)
        elif kind == "weather":
            data = weather_icon(val)
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
