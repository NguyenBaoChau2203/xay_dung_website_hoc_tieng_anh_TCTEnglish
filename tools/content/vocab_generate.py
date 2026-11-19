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
    "purple": (147, 51, 234), "orange": (249, 115, 22), "pink": (244, 114, 182), "black": (22, 22, 24), "white": (255, 255, 255),
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
    elif kind == "gymnast":
        _person(d, cx - 40, 40, 2.6, "f", (124, 58, 237))
        pts = []
        for i in range(60):
            t = i / 59
            pts.append(((cx + 20 + t * 150) * s, (120 - math.sin(t * 9) * 36 * (1 - t * 0.4) - t * 30) * s))
        d.line(pts, fill=(236, 72, 153), width=7 * s, joint="curve")
        d.line((((cx + 20) * s, 128 * s, (cx - 4) * s, 160 * s)), fill=OUTLINE, width=4 * s)
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


YELLOW = (250, 204, 21)
EMOTIONS = {
    # eyes, brows, mouth, face colour, extras
    "happy": ("open", "up", "big", YELLOW, ["blush"]),
    "sad": ("droop", "worry", "frown", (147, 197, 253), ["tear"]),
    "angry": ("open", "angry", "grimace", (239, 68, 68), ["steam"]),
    "surprised": ("wide", "high", "o", YELLOW, ["lines"]),
    "bored": ("half", "flat", "flat", (203, 213, 225), []),
    "tired": ("half", "flat", "wavy", (203, 213, 225), ["bags", "sweat"]),
    "scared": ("wide_small", "worry", "grimace", (196, 181, 253), ["sweat"]),
    "excited": ("star", "up", "big", YELLOW, ["sparkle"]),
    "nervous": ("side", "worry", "wavy", YELLOW, ["sweat"]),
    "worried": ("open", "worry", "frown", YELLOW, ["sweat"]),
    "proud": ("arc", "up", "smirk", YELLOW, ["sparkle"]),
    "shy": ("down", "up", "small", (252, 165, 165), ["blush"]),
    "lonely": ("droop", "worry", "frown", (148, 163, 184), ["cloud"]),
    "jealous": ("side", "angry", "smirk", (134, 239, 172), []),
    "confused": ("open", "one", "wavy", YELLOW, ["question"]),
    "embarrassed": ("down", "worry", "wavy", (252, 165, 165), ["blush", "sweat"]),
    "relaxed": ("arc_down", "flat", "smile", YELLOW, ["blush"]),
    "curious": ("side_wide", "one", "small_o", YELLOW, ["question"]),
    "disappointed": ("down", "worry", "frown", (203, 213, 225), []),
    "grateful": ("arc", "up", "smile", YELLOW, ["blush", "hearts"]),
    "upset": ("droop", "worry", "frown", (252, 165, 165), ["tear", "tear2"]),
    "calm": ("arc_down", "flat", "smile", (186, 230, 253), []),
    "cheerful": ("arc", "up", "big", YELLOW, ["sun"]),
    "sleepy": ("closed", "flat", "small_o", (196, 181, 253), ["zzz"]),
    "stressed": ("wide_small", "angry", "grimace", (253, 186, 116), ["sweat", "zig"]),
}


def emotion_face(name: str) -> bytes:
    """Flat original emoji-style face for an emotion word (kind "emotion")."""
    eyes, brows, mouth, col, extras = EMOTIONS[name]
    img, d = canvas()
    s = S
    L = OUTLINE
    cx, cy, r = 240, 190, 130

    def oval(x0, y0, x1, y1, fill, w=0):
        d.ellipse((x0 * s, y0 * s, x1 * s, y1 * s), fill=fill, outline=L if w else None, width=w * s)

    def line(x0, y0, x1, y1, w=7):
        d.line((x0 * s, y0 * s, x1 * s, y1 * s), fill=L, width=w * s)

    def arc(x0, y0, x1, y1, a0, a1, w=7):
        d.arc((x0 * s, y0 * s, x1 * s, y1 * s), a0, a1, fill=L, width=w * s)

    def star(x, y, rr, fill):
        d.polygon(poly_star(x * s, y * s, rr * s, rr * 0.45 * s), fill=fill, outline=L)

    if "cloud" in extras:
        for dx, dy, rr in ((-40, 0, 28), (0, -12, 34), (40, 0, 28)):
            oval(cx + dx - rr, 36 + dy - rr + 20, cx + dx + rr, 36 + dy + rr + 20, (203, 213, 225), 3)
        for k in (-30, 0, 30):
            line(cx + k, 86, cx + k - 6, 108, 4)
    if "sun" in extras:
        for k in range(12):
            a = math.radians(k * 30)
            d.line(((cx + math.cos(a) * (r + 12)) * s, (cy + math.sin(a) * (r + 12)) * s, (cx + math.cos(a) * (r + 32)) * s, (cy + math.sin(a) * (r + 32)) * s), fill=(245, 158, 11), width=8 * s)
    oval(cx - r, cy - r, cx + r, cy + r, col, 5)
    # cheeks
    if "blush" in extras:
        oval(cx - 100, cy + 14, cx - 56, cy + 44, (248, 113, 113))
        oval(cx + 56, cy + 14, cx + 100, cy + 44, (248, 113, 113))
    # eyes
    ex = (-48, 48)
    ey = cy - 30
    for i, x in enumerate(ex):
        X = cx + x
        if eyes == "open":
            oval(X - 12, ey - 14, X + 12, ey + 14, L)
        elif eyes == "droop":
            oval(X - 11, ey - 6, X + 11, ey + 20, L)
        elif eyes == "wide":
            oval(X - 22, ey - 24, X + 22, ey + 20, (255, 255, 255), 4)
            oval(X - 8, ey - 8, X + 8, ey + 8, L)
        elif eyes == "wide_small":
            oval(X - 22, ey - 24, X + 22, ey + 20, (255, 255, 255), 4)
            oval(X - 4, ey - 4, X + 4, ey + 4, L)
        elif eyes == "half":
            oval(X - 15, ey - 14, X + 15, ey + 16, (255, 255, 255), 4)
            oval(X - 7, ey - 2, X + 7, ey + 12, L)
            d.rectangle(((X - 18) * s, (ey - 18) * s, (X + 18) * s, (ey - 2) * s), fill=col)
            line(X - 18, ey - 2, X + 18, ey - 2, 6)
        elif eyes == "star":
            star(X, ey, 22, (253, 224, 71))
        elif eyes == "side":
            oval(X - 16, ey - 14, X + 16, ey + 14, (255, 255, 255), 4)
            oval(X + 2, ey - 6, X + 14, ey + 6, L)
        elif eyes == "side_wide":
            oval(X - 20, ey - 20, X + 20, ey + 20, (255, 255, 255), 4)
            oval(X + 4, ey - 8, X + 16, ey + 4, L)
        elif eyes == "down":
            oval(X - 14, ey - 12, X + 14, ey + 14, (255, 255, 255), 4)
            oval(X - 6, ey + 2, X + 6, ey + 12, L)
        elif eyes == "arc":
            arc(X - 18, ey - 12, X + 18, ey + 24, 200, 340, 8)
        elif eyes == "arc_down":
            arc(X - 18, ey - 20, X + 18, ey + 14, 20, 160, 8)
        elif eyes == "closed":
            line(X - 16, ey, X + 16, ey, 8)
    # brows
    by = cy - 70
    if brows == "up":
        arc(cx - 76, by - 6, cx - 20, by + 30, 200, 330, 6)
        arc(cx + 20, by - 6, cx + 76, by + 30, 210, 340, 6)
    elif brows == "high":
        arc(cx - 76, by - 22, cx - 20, by + 14, 200, 330, 6)
        arc(cx + 20, by - 22, cx + 76, by + 14, 210, 340, 6)
    elif brows == "flat":
        line(cx - 72, by + 4, cx - 24, by + 4, 6)
        line(cx + 24, by + 4, cx + 72, by + 4, 6)
    elif brows == "worry":
        line(cx - 72, by + 14, cx - 24, by - 8, 6)
        line(cx + 24, by - 8, cx + 72, by + 14, 6)
    elif brows == "angry":
        line(cx - 72, by - 8, cx - 24, by + 14, 7)
        line(cx + 24, by + 14, cx + 72, by - 8, 7)
    elif brows == "one":
        line(cx - 72, by + 6, cx - 24, by + 6, 6)
        arc(cx + 20, by - 26, cx + 76, by + 10, 200, 340, 6)
    # mouth
    my = cy + 52
    if mouth == "smile":
        arc(cx - 40, my - 30, cx + 40, my + 20, 20, 160, 7)
    elif mouth == "big":
        d.pieslice(((cx - 56) * s, (my - 40) * s, (cx + 56) * s, (my + 44) * s), 0, 180, fill=L)
    elif mouth == "frown":
        arc(cx - 40, my, cx + 40, my + 50, 200, 340, 7)
    elif mouth == "flat":
        line(cx - 32, my + 16, cx + 32, my + 16, 7)
    elif mouth == "o":
        oval(cx - 18, my - 4, cx + 18, my + 44, L)
    elif mouth == "small_o":
        oval(cx - 10, my + 4, cx + 10, my + 30, L)
    elif mouth == "small":
        arc(cx - 20, my - 4, cx + 20, my + 22, 20, 160, 6)
    elif mouth == "wavy":
        pts = [(cx - 40 + i * 8, my + 18 + math.sin(i * 1.3) * 7) for i in range(11)]
        d.line([(x * s, y * s) for x, y in pts], fill=L, width=6 * s, joint="curve")
    elif mouth == "smirk":
        arc(cx - 36, my - 14, cx + 48, my + 28, 20, 150, 7)
        line(cx + 44, my + 8, cx + 54, my - 2, 6)
    elif mouth == "grimace":
        d.rounded_rectangle(((cx - 44) * s, (my + 2) * s, (cx + 44) * s, (my + 38) * s), 10 * s, fill=(255, 255, 255), outline=L, width=5 * s)
        for k in range(-30, 40, 15):
            line(cx + k, my + 2, cx + k, my + 38, 3)
        line(cx - 44, my + 20, cx + 44, my + 20, 3)
    # extras
    if "tear" in extras:
        d.polygon([((cx - 70) * s, (ey + 22) * s), ((cx - 80) * s, (ey + 52) * s), ((cx - 60) * s, (ey + 52) * s)], fill=(96, 165, 250), outline=L)
    if "tear2" in extras:
        d.polygon([((cx + 70) * s, (ey + 22) * s), ((cx + 60) * s, (ey + 52) * s), ((cx + 80) * s, (ey + 52) * s)], fill=(96, 165, 250), outline=L)
    if "sweat" in extras:
        d.polygon([((cx + 100) * s, (cy - 100) * s), ((cx + 88) * s, (cy - 64) * s), ((cx + 112) * s, (cy - 64) * s)], fill=(147, 197, 253), outline=L)
    if "steam" in extras:
        for k in (-60, 60):
            oval(cx + k - 16, 28, cx + k + 16, 60, (255, 255, 255), 3)
    if "lines" in extras:
        for a in (-60, -30, 30, 60):
            x0 = cx + math.sin(math.radians(a)) * (r + 14)
            y0 = cy - math.cos(math.radians(a)) * (r + 14)
            x1 = cx + math.sin(math.radians(a)) * (r + 38)
            y1 = cy - math.cos(math.radians(a)) * (r + 38)
            line(x0, y0, x1, y1, 5)
    if "bags" in extras:
        arc(cx - 66, ey + 6, cx - 30, ey + 40, 20, 160, 4)
        arc(cx + 30, ey + 6, cx + 66, ey + 40, 20, 160, 4)
    if "sparkle" in extras:
        star(cx + 130, 70, 18, (253, 224, 71))
        star(cx - 140, 90, 12, (253, 224, 71))
    if "question" in extras:
        d.text(((cx + 100) * s, 20 * s), "?", fill=L, font=_big_font(90 * s))
    if "hearts" in extras:
        for hx, hy, hr in ((cx + 124, 60, 16), (cx - 130, 80, 12)):
            oval(hx - hr, hy - hr, hx, hy, (244, 63, 94))
            oval(hx, hy - hr, hx + hr, hy, (244, 63, 94))
            d.polygon([((hx - hr) * s, (hy - hr * 0.4) * s), ((hx + hr) * s, (hy - hr * 0.4) * s), (hx * s, (hy + hr * 1.1) * s)], fill=(244, 63, 94))
    if "zzz" in extras:
        d.text(((cx + 100) * s, 20 * s), "Z", fill=L, font=_big_font(70 * s))
        d.text(((cx + 140) * s, (-4) * s), "z", fill=L, font=_big_font(46 * s))
    if "zig" in extras:
        pts = [(cx - 60 + i * 20, 24 + (12 if i % 2 else 0)) for i in range(7)]
        d.line([(x * s, y * s) for x, y in pts], fill=L, width=5 * s, joint="curve")
    return finish(img)


def _big_font(px: int):
    from PIL import ImageFont

    for name in ("arialbd.ttf", "arial.ttf", "DejaVuSans-Bold.ttf"):
        try:
            return ImageFont.truetype(name, int(px))
        except OSError:
            continue
    return ImageFont.load_default()


def health_icon(name: str) -> bytes:
    """Flat original drawing for a health or illness word (kind "health")."""
    img, d = canvas()
    s = S
    L = OUTLINE
    RED = (220, 38, 38)

    def oval(x0, y0, x1, y1, fill, w=3):
        d.ellipse((x0 * s, y0 * s, x1 * s, y1 * s), fill=fill, outline=L if w else None, width=w * s)

    def box(x0, y0, x1, y1, fill, rr=0, w=3):
        d.rounded_rectangle((x0 * s, y0 * s, x1 * s, y1 * s), rr * s, fill=fill, outline=L, width=w * s)

    def poly(pts, fill):
        pp = [(x * s, y * s) for x, y in pts]
        d.polygon(pp, fill=fill)
        d.line(pp + [pp[0]], fill=L, width=3 * s, joint="curve")

    def line(x0, y0, x1, y1, w=6, col=L):
        d.line((x0 * s, y0 * s, x1 * s, y1 * s), fill=col, width=w * s)

    def arc(x0, y0, x1, y1, a0, a1, w=6):
        d.arc((x0 * s, y0 * s, x1 * s, y1 * s), a0, a1, fill=L, width=w * s)

    def face(cx, cy, r, col, mood):
        oval(cx - r, cy - r, cx + r, cy + r, col, 5)
        ey = cy - r * 0.2
        for sx in (-1, 1):
            if mood in ("sneeze",):
                line(cx + sx * r * 0.4 - 10, ey, cx + sx * r * 0.4 + 10, ey, 6)
            else:
                oval(cx + sx * r * 0.4 - 8, ey - 9, cx + sx * r * 0.4 + 8, ey + 9, L, 0)
        if mood in ("sad", "hot", "cold", "ill", "pain"):
            arc(cx - r * 0.3, cy + r * 0.35, cx + r * 0.3, cy + r * 0.75, 200, 340, 6)
            line(cx - r * 0.7, cy - r * 0.55, cx - r * 0.2, cy - r * 0.4, 6)
            line(cx + r * 0.2, cy - r * 0.4, cx + r * 0.7, cy - r * 0.55, 6)
        elif mood == "open":
            oval(cx - r * 0.25, cy + r * 0.3, cx + r * 0.25, cy + r * 0.75, L, 0)
        elif mood == "sneeze":
            oval(cx - r * 0.3, cy + r * 0.3, cx + r * 0.3, cy + r * 0.8, L, 0)
        elif mood == "smile":
            arc(cx - r * 0.35, cy + r * 0.1, cx + r * 0.35, cy + r * 0.65, 20, 160, 6)

    SK = (253, 224, 71)
    if name == "fever":
        face(210, 170, 110, (252, 165, 165), "hot")
        box(210, 244, 320, 258, (255, 255, 255), 6, 3)
        oval(300, 238, 328, 266, RED, 3)
        poly([(120, 40), (108, 70), (132, 70)], (147, 197, 253))
        poly([(300, 56), (288, 86), (312, 86)], (147, 197, 253))
    elif name == "cough":
        face(200, 170, 110, SK, "open")
        oval(280, 200, 340, 260, (250, 213, 172), 3)
        for k, (dx, dy) in enumerate(((60, -30), (90, 0), (60, 30))):
            line(350 + 0 * dx, 190 + dy * 1.2, 350 + dx, 190 + dy * 1.2, 5)
    elif name == "headache":
        face(240, 190, 110, SK, "pain")
        poly([(220, 30), (180, 90), (214, 84), (186, 140), (256, 70), (222, 76), (250, 30)], RED)
    elif name == "toothache":
        face(220, 180, 110, SK, "pain")
        oval(280, 190, 340, 250, (252, 165, 165), 3)
        poly([(380, 80), (400, 80), (404, 130), (394, 160), (386, 130), (376, 160), (366, 130), (370, 80)], (255, 255, 255))
        line(350, 60, 340, 40, 5, RED)
        line(410, 56, 420, 36, 5, RED)
    elif name == "cold":
        face(240, 180, 110, (186, 230, 253), "cold")
        oval(228, 190, 252, 214, (248, 113, 113), 3)
        box(130, 250, 350, 290, (220, 38, 38), 10)
        for k in range(5):
            line(150 + k * 44, 252, 150 + k * 44, 288, 4, (248, 113, 113))
        for sx, sy in ((80, 70), (400, 80)):
            for a in range(0, 180, 60):
                dx, dy = math.cos(math.radians(a)) * 24, math.sin(math.radians(a)) * 24
                line(sx - dx, sy - dy, sx + dx, sy + dy, 4, (59, 130, 246))
    elif name == "flu":
        face(220, 180, 110, (134, 239, 172), "ill")
        box(300, 236, 410, 250, (255, 255, 255), 6, 3)
        oval(390, 230, 418, 258, RED, 3)
        for (vx, vy) in ((80, 60), (390, 80), (70, 260)):
            oval(vx - 14, vy - 14, vx + 14, vy + 14, (74, 222, 128), 3)
            for a in range(0, 360, 45):
                line(vx + math.cos(math.radians(a)) * 14, vy + math.sin(math.radians(a)) * 14, vx + math.cos(math.radians(a)) * 22, vy + math.sin(math.radians(a)) * 22, 3)
    elif name == "stomachache":
        _person(d, 240, 30, 2.6, "m", (37, 99, 235))
        oval(214, 210, 266, 254, (253, 224, 71), 3)
        for a in range(0, 360, 45):
            line(240 + math.cos(math.radians(a)) * 34, 232 + math.sin(math.radians(a)) * 34, 240 + math.cos(math.radians(a)) * 52, 232 + math.sin(math.radians(a)) * 52, 5, RED)
        oval(222, 218, 258, 246, RED, 0)
    elif name == "sore throat":
        face(220, 150, 100, SK, "open")
        box(190, 250, 250, 316, (250, 213, 172), 12)
        oval(205, 262, 235, 296, RED, 3)
        for k in (-1, 1):
            line(260 + k * 0 + 10, 270 + k * 14, 300, 270 + k * 24, 5, RED)
    elif name == "sneeze":
        face(200, 170, 110, SK, "sneeze")
        box(300, 190, 400, 260, (255, 255, 255), 10)
        line(310, 215, 390, 215, 3)
        line(310, 235, 390, 235, 3)
        for k, (dx, dy) in enumerate(((50, -24), (62, 0), (50, 24))):
            oval(300 + dx * 0 - 6 + 10 * k, 120 + dy + 40, 300 + 6 + 10 * k, 132 + dy + 40, (147, 197, 253), 2)
    elif name == "allergy":
        face(220, 180, 110, SK, "sneeze")
        oval(210, 182, 236, 208, (248, 113, 113), 3)
        for (fx, fy) in ((360, 90), (410, 150), (350, 220), (90, 70)):
            for a in range(0, 360, 72):
                oval(fx + math.cos(math.radians(a)) * 16 - 9, fy + math.sin(math.radians(a)) * 16 - 9, fx + math.cos(math.radians(a)) * 16 + 9, fy + math.sin(math.radians(a)) * 16 + 9, (244, 114, 182), 2)
            oval(fx - 8, fy - 8, fx + 8, fy + 8, (250, 204, 21), 2)
    elif name == "rash":
        box(60, 120, 420, 240, (250, 213, 172), 50)
        for (px, py) in ((120, 150), (170, 200), (220, 150), (270, 205), (320, 150), (360, 200), (190, 175), (300, 175), (140, 215)):
            oval(px - 9, py - 9, px + 9, py + 9, (239, 68, 68), 2)
    elif name == "wound":
        box(60, 130, 420, 250, (250, 213, 172), 50)
        poly([(180, 170), (240, 156), (300, 176), (250, 194)], (239, 68, 68))
        for k in range(5):
            line(196 + k * 20, 160 + (k % 2) * 8, 204 + k * 20, 190 + (k % 2) * 4, 3)
    elif name == "bruise":
        box(60, 130, 420, 250, (250, 213, 172), 50)
        oval(170, 150, 310, 234, (126, 34, 206), 0)
        oval(200, 164, 290, 220, (91, 33, 182), 0)
        oval(150, 140, 330, 244, (167, 139, 250), 0)
        oval(190, 156, 300, 228, (109, 40, 217), 0)
    elif name == "bandage":
        box(60, 140, 420, 220, (250, 200, 150), 36)
        box(180, 140, 300, 220, (254, 226, 190), 0)
        for r_ in range(3):
            for c_ in range(5):
                oval(200 + c_ * 20 - 3, 156 + r_ * 20 - 3, 200 + c_ * 20 + 3, 156 + r_ * 20 + 3, (240, 180, 130), 0)
        line(60, 180, 70, 180, 1)
    elif name == "medicine":
        box(160, 90, 290, 290, (253, 186, 116), 16)
        box(190, 50, 260, 100, (255, 255, 255), 8)
        box(180, 150, 270, 240, (255, 255, 255), 6)
        line(200, 170, 250, 170, 4)
        line(200, 190, 250, 190, 4)
        line(200, 210, 240, 210, 4)
        oval(330, 220, 410, 280, (226, 232, 240), 4)
        line(370, 240, 420, 130, 10, (170, 178, 192))
    elif name == "patient":
        box(40, 200, 440, 280, (147, 197, 253), 12)
        box(40, 180, 120, 220, (255, 255, 255), 14)
        oval(60, 150, 130, 215, (250, 213, 172), 3)
        box(120, 200, 440, 260, (134, 239, 172), 22)
        line(40, 280, 40, 320, 8)
        line(440, 280, 440, 320, 8)
        box(330, 60, 370, 130, (255, 255, 255), 6)
        line(350, 130, 350, 190, 4)
    elif name == "first aid kit":
        box(110, 120, 370, 290, (255, 255, 255), 18, 5)
        box(190, 78, 290, 124, (255, 255, 255), 14, 5)
        box(222, 160, 258, 250, RED, 4, 1)
        box(195, 187, 285, 223, RED, 4, 1)
    elif name == "wheelchair":
        oval(130, 160, 310, 340, BG, 10)
        oval(210, 232, 230, 252, L, 0)
        oval(330, 280, 390, 340, BG, 6)
        line(220, 90, 220, 220, 10)
        line(220, 220, 340, 220, 10)
        line(340, 220, 360, 290, 8)
        box(190, 70, 232, 150, (59, 130, 246), 8)
        oval(204, 36, 240, 72, (250, 213, 172), 3)
    elif name == "sunburn":
        face(220, 190, 110, (248, 113, 113), "hot")
        for k in range(12):
            a = math.radians(k * 30)
            line(390 + math.cos(a) * 38, 90 + math.sin(a) * 38, 390 + math.cos(a) * 62, 90 + math.sin(a) * 62, 6, (245, 158, 11))
        oval(352, 52, 428, 128, (250, 204, 21), 3)
    elif name == "health":
        pts = []
        for i in range(120):
            t = i / 119
            ang = math.pi * 2 * t
            x = 16 * math.sin(ang) ** 3
            y = -(13 * math.cos(ang) - 5 * math.cos(2 * ang) - 2 * math.cos(3 * ang) - math.cos(4 * ang))
            pts.append((240 + x * 9, 170 + y * 9))
        poly(pts, (244, 63, 94))
        d.line([(120 * s, 180 * s), (190 * s, 180 * s), (210 * s, 130 * s), (240 * s, 230 * s), (265 * s, 160 * s), (285 * s, 180 * s), (360 * s, 180 * s)], fill=(255, 255, 255), width=8 * s, joint="curve")
    elif name == "vitamin":
        box(170, 110, 300, 300, (251, 146, 60), 18)
        box(190, 74, 280, 116, (255, 255, 255), 8)
        box(186, 170, 284, 250, (255, 255, 255), 6)
        d.text((209 * s, 188 * s), "VIT", fill=L, font=_big_font(34 * s))
        oval(330, 220, 380, 270, (250, 204, 21), 3)
        oval(100, 230, 150, 280, (251, 146, 60), 3)
        oval(330, 250, 350, 280, (74, 222, 128), 2)
    else:
        raise SystemExit(f"unknown health icon: {name}")
    return finish(img)


def tech_icon(name: str) -> bytes:
    """Flat original drawing of a technology item (kind "tech")."""
    img, d = canvas()
    s = S
    L = OUTLINE

    def oval(x0, y0, x1, y1, fill, w=3):
        d.ellipse((x0 * s, y0 * s, x1 * s, y1 * s), fill=fill, outline=L if w else None, width=w * s)

    def box(x0, y0, x1, y1, fill, rr=0, w=3):
        d.rounded_rectangle((x0 * s, y0 * s, x1 * s, y1 * s), rr * s, fill=fill, outline=L if w else None, width=w * s)

    def poly(pts, fill):
        pp = [(x * s, y * s) for x, y in pts]
        d.polygon(pp, fill=fill)
        d.line(pp + [pp[0]], fill=L, width=3 * s, joint="curve")

    def line(x0, y0, x1, y1, w=6, col=L):
        d.line((x0 * s, y0 * s, x1 * s, y1 * s), fill=col, width=w * s)

    def phone(x0, y0, x1, y1):
        box(x0, y0, x1, y1, (30, 41, 59), 22, 4)
        box(x0 + 10, y0 + 22, x1 - 10, y1 - 22, (241, 245, 249), 6, 0)

    if name == "drone":
        for sx in (-1, 1):
            for sy in (-1, 1):
                line(240, 180, 240 + sx * 130, 180 + sy * 70, 10)
                oval(240 + sx * 130 - 26, 180 + sy * 70 - 8, 240 + sx * 130 + 26, 180 + sy * 70 + 8, (148, 163, 184), 3)
                line(240 + sx * 130, 180 + sy * 70, 240 + sx * 130, 180 + sy * 70 - 14, 4)
        box(200, 150, 280, 210, (51, 65, 85), 18)
        oval(222, 206, 258, 242, (30, 41, 59), 3)
        oval(232, 216, 248, 232, (96, 165, 250), 0)
    elif name == "website":
        box(50, 50, 430, 300, (255, 255, 255), 12, 4)
        box(50, 50, 430, 88, (203, 213, 225), 12, 4)
        for k, c in enumerate(((239, 68, 68), (250, 204, 21), (74, 222, 128))):
            oval(66 + k * 24, 62, 84 + k * 24, 78, c, 2)
        box(150, 58, 410, 80, (255, 255, 255), 10, 2)
        box(70, 108, 410, 170, (147, 197, 253), 6, 2)
        for k in range(3):
            box(70 + k * 116, 186, 170 + k * 116, 280, (226, 232, 240), 6, 2)
            line(80 + k * 116, 204, 160 + k * 116, 204, 4, (148, 163, 184))
            line(80 + k * 116, 222, 150 + k * 116, 222, 4, (148, 163, 184))
    elif name == "app":
        phone(170, 30, 310, 330)
        cols = [(239, 68, 68), (250, 204, 21), (74, 222, 128), (59, 130, 246), (168, 85, 247), (249, 115, 22), (20, 184, 166), (236, 72, 153), (100, 116, 139)]
        for k, c in enumerate(cols):
            box(188 + (k % 3) * 36, 78 + (k // 3) * 56, 212 + (k % 3) * 36, 102 + (k // 3) * 56, c, 7, 2)
    elif name == "social media":
        phone(170, 30, 310, 330)
        oval(190, 70, 222, 102, (250, 204, 21), 2)
        line(232, 80, 292, 80, 5, (148, 163, 184))
        line(232, 94, 272, 94, 4, (203, 213, 225))
        box(190, 118, 292, 190, (147, 197, 253), 6, 2)
        for hx, hy, hr, col in ((120, 90, 22, (244, 63, 94)), (360, 140, 18, (244, 63, 94))):
            oval(hx - hr, hy - hr, hx, hy, col, 0)
            oval(hx, hy - hr, hx + hr, hy, col, 0)
            poly([(hx - hr, hy - hr * 0.35), (hx + hr, hy - hr * 0.35), (hx, hy + hr * 1.1)], col)
        box(330, 220, 420, 270, (255, 255, 255), 12, 3)
        poly([(350, 270), (344, 292), (372, 270)], (255, 255, 255))
        for k in range(3):
            oval(350 + k * 22, 240, 362 + k * 22, 252, (148, 163, 184), 0)
        box(50, 200, 140, 250, (59, 130, 246), 12, 3)
        poly([(120, 250), (130, 272), (100, 250)], (59, 130, 246))
    elif name == "satellite":
        for sx in (-1, 1):
            for k in range(3):
                box(240 + sx * (62 + k * 36) - 17, 150, 240 + sx * (62 + k * 36) + 17, 220, (37, 99, 235), 2, 3)
        line(240, 185, 240 + 62, 185, 6)
        line(240, 185, 240 - 62, 185, 6)
        box(200, 140, 280, 230, (203, 213, 225), 12)
        box(208, 150, 272, 176, (250, 204, 21), 4, 2)
        line(240, 140, 240, 108, 5)
        oval(228, 92, 252, 116, (226, 232, 240), 3)
        for r_ in (30, 50):
            d.arc(((240 - r_) * s, (92 - r_) * s, (240 + r_) * s, (92 + r_) * s), 220, 320, fill=(59, 130, 246), width=4 * s)
    elif name == "QR code":
        box(120, 20, 360, 340, (255, 255, 255), 10, 4)
        rnd = [(0, 0)]
        import random as _r
        rng = _r.Random(7)
        n, cell, x0, y0 = 17, 12, 138, 38
        for i in range(n):
            for j in range(n):
                finder = (i < 7 and j < 7) or (i < 7 and j >= n - 7) or (i >= n - 7 and j < 7)
                if finder:
                    continue
                if rng.random() < 0.5:
                    d.rectangle(((x0 + j * cell) * s, (y0 + i * cell) * s, (x0 + j * cell + cell) * s, (y0 + i * cell + cell) * s), fill=L)
        for fi, fj in ((0, 0), (0, n - 7), (n - 7, 0)):
            fx, fy = x0 + fj * cell, y0 + fi * cell
            d.rectangle((fx * s, fy * s, (fx + 7 * cell) * s, (fy + 7 * cell) * s), fill=L)
            d.rectangle(((fx + cell) * s, (fy + cell) * s, (fx + 6 * cell) * s, (fy + 6 * cell) * s), fill=(255, 255, 255))
            d.rectangle(((fx + 2 * cell) * s, (fy + 2 * cell) * s, (fx + 5 * cell) * s, (fy + 5 * cell) * s), fill=L)
    elif name == "earbuds":
        for sx in (-1, 1):
            cx = 240 + sx * 80
            oval(cx - 34, 80, cx + 34, 150, (248, 250, 252), 4)
            box(cx - 10 + sx * 8, 140, cx + 10 + sx * 8, 250, (248, 250, 252), 9, 4)
            oval(cx - 14, 100, cx + 14, 128, (203, 213, 225), 2)
        box(150, 266, 330, 330, (226, 232, 240), 26, 4)
        line(150, 298, 330, 298, 2)
    elif name == "charger":
        box(160, 70, 320, 190, (248, 250, 252), 14, 4)
        box(188, 40, 206, 72, (170, 178, 192), 3, 3)
        box(274, 40, 292, 72, (170, 178, 192), 3, 3)
        d.line([(240 * s, 190 * s), (240 * s, 250 * s), (300 * s, 290 * s), (360 * s, 290 * s)], fill=L, width=7 * s, joint="curve")
        box(350, 274, 400, 306, (51, 65, 85), 6, 3)
    else:
        raise SystemExit(f"unknown tech icon: {name}")
    return finish(img)


def eco_icon(name: str) -> bytes:
    """Flat original drawing for an environment word (kind "eco")."""
    img, d = canvas()
    s = S
    L = OUTLINE

    def oval(x0, y0, x1, y1, fill, w=3):
        d.ellipse((x0 * s, y0 * s, x1 * s, y1 * s), fill=fill, outline=L if w else None, width=w * s)

    def box(x0, y0, x1, y1, fill, rr=0, w=3):
        d.rounded_rectangle((x0 * s, y0 * s, x1 * s, y1 * s), rr * s, fill=fill, outline=L if w else None, width=w * s)

    def poly(pts, fill):
        pp = [(x * s, y * s) for x, y in pts]
        d.polygon(pp, fill=fill)
        d.line(pp + [pp[0]], fill=L, width=3 * s, joint="curve")

    def line(x0, y0, x1, y1, w=6, col=L):
        d.line((x0 * s, y0 * s, x1 * s, y1 * s), fill=col, width=w * s)

    if name == "reusable bag":
        d.arc((170 * s, 20 * s, 310 * s, 190 * s), 180, 360, fill=L, width=9 * s)
        poly([(110, 120), (370, 120), (394, 330), (86, 330)], (134, 239, 172))
        poly([(240, 190), (290, 220), (270, 276), (240, 290), (210, 276), (190, 220)], (22, 163, 74))
        line(240, 200, 240, 292, 4, (22, 101, 52))
        for k in (-1, 1):
            line(240, 238, 240 + k * 22, 220, 3, (22, 101, 52))
            line(240, 262, 240 + k * 24, 244, 3, (22, 101, 52))
    elif name == "oil spill":
        d.rectangle((0, 150 * s, W * s, H * s), fill=(96, 165, 250))
        for k in range(5):
            line(20 + k * 100, 190, 70 + k * 100, 190, 4, (191, 219, 254))
        oval(80, 220, 400, 310, (30, 30, 36), 0)
        oval(140, 200, 330, 270, (45, 45, 55), 0)
        oval(260, 250, 420, 320, (24, 24, 30), 0)
        for sx, sy in ((150, 240), (300, 268)):
            oval(sx - 16, sy - 6, sx + 16, sy + 6, (126, 34, 206), 0)
        # tanker
        poly([(60, 150), (360, 150), (330, 190), (96, 190)], (71, 85, 105))
        box(120, 112, 180, 150, (226, 232, 240), 3)
        for k in range(4):
            line(200 + k * 36, 150, 200 + k * 36, 130, 4)
        line(250, 190, 250, 240, 3, (30, 30, 36))
    elif name == "litter":
        d.rectangle((0, 220 * s, W * s, H * s), fill=(176, 214, 160))
        box(60, 130, 120, 230, (59, 130, 246), 14)  # bottle body
        box(80, 96, 100, 134, (59, 130, 246), 6)
        box(76, 84, 104, 98, (226, 232, 240), 3)
        poly([(180, 230), (186, 270), (240, 270), (246, 230)], (203, 213, 225))  # can
        oval(180, 220, 246, 240, (226, 232, 240), 3)
        poly([(290, 250), (340, 236), (370, 270), (330, 292), (296, 280)], (254, 243, 199))  # paper
        line(305, 258, 346, 250, 3)
        line(310, 270, 352, 262, 3)
        poly([(390, 220), (420, 250), (404, 290), (372, 270)], (252, 165, 165))
        d.line([(215 * s, 190 * s), (215 * s, 180 * s)], fill=L, width=1 * s)
    else:
        raise SystemExit(f"unknown eco icon: {name}")
    return finish(img)


def travel_icon(name: str) -> bytes:
    """Flat original drawing for a travel word (kind "travel")."""
    img, d = canvas()
    s = S
    L = OUTLINE

    def oval(x0, y0, x1, y1, fill, w=3):
        d.ellipse((x0 * s, y0 * s, x1 * s, y1 * s), fill=fill, outline=L if w else None, width=w * s)

    def box(x0, y0, x1, y1, fill, rr=0, w=3):
        d.rounded_rectangle((x0 * s, y0 * s, x1 * s, y1 * s), rr * s, fill=fill, outline=L if w else None, width=w * s)

    def poly(pts, fill):
        pp = [(x * s, y * s) for x, y in pts]
        d.polygon(pp, fill=fill)
        d.line(pp + [pp[0]], fill=L, width=3 * s, joint="curve")

    def line(x0, y0, x1, y1, w=6, col=L):
        d.line((x0 * s, y0 * s, x1 * s, y1 * s), fill=col, width=w * s)

    def plane(cx, cy, k=1.0, col=(226, 232, 240)):
        poly([(cx - 70 * k, cy), (cx + 60 * k, cy - 10 * k), (cx + 80 * k, cy), (cx + 60 * k, cy + 10 * k)], col)
        poly([(cx - 8 * k, cy - 3 * k), (cx + 24 * k, cy - 54 * k), (cx + 40 * k, cy - 54 * k), (cx + 24 * k, cy - 3 * k)], (148, 163, 184))
        poly([(cx - 8 * k, cy + 3 * k), (cx + 24 * k, cy + 54 * k), (cx + 40 * k, cy + 54 * k), (cx + 24 * k, cy + 3 * k)], (148, 163, 184))

    if name == "boarding pass":
        box(50, 90, 430, 270, (255, 255, 255), 14, 4)
        line(330, 90, 330, 270, 3)
        d.line([(330 * s, 96 * s), (330 * s, 264 * s)], fill=BG, width=1)
        plane(130, 140, 0.7)
        for k in range(4):
            line(80, 190 + k * 20, 230 - (k % 2) * 40, 190 + k * 20, 5, (148, 163, 184))
        for k in range(10):
            line(350 + k * 7, 180, 350 + k * 7, 250, 3 + (k % 3))
        box(250, 110, 316, 140, (96, 165, 250), 6, 2)
    elif name == "ticket":
        poly([(50, 110), (430, 110), (430, 160), (410, 180), (430, 200), (430, 250), (50, 250), (50, 200), (70, 180), (50, 160)], (250, 204, 21))
        line(330, 116, 330, 244, 3)
        for k in range(3):
            line(80, 150 + k * 28, 280 - (k % 2) * 50, 150 + k * 28, 6, (146, 64, 14))
        oval(350, 150, 410, 210, (254, 243, 199), 3)
        poly([(380, 160), (386, 176), (404, 176), (390, 186), (396, 202), (380, 192), (364, 202), (370, 186), (356, 176), (374, 176)], (250, 204, 21))
    elif name == "tour guide":
        _person(d, 160, 40, 2.6, "f", (37, 99, 235))
        line(238, 190, 290, 120, 6, (120, 72, 36))
        poly([(290, 120), (370, 100), (370, 150), (290, 160)], (239, 68, 68))
        for k in range(3):
            _person(d, 330 + k * 46, 190, 0.9, "m" if k % 2 else "f", [(22, 163, 74), (234, 88, 12), (124, 58, 237)][k])
    elif name == "postcard":
        box(60, 70, 420, 290, (255, 255, 255), 8, 4)
        line(250, 90, 250, 270, 3)
        for k in range(4):
            line(270, 190 + k * 18, 400, 190 + k * 18, 3, (148, 163, 184))
        box(360, 84, 408, 130, (252, 165, 165), 3, 3)
        d.rectangle((76 * s, 90 * s, 236 * s, 270 * s), fill=(147, 197, 253))
        oval(170, 110, 220, 160, (250, 204, 21), 2)
        poly([(76, 270), (130, 180), (190, 270)], (74, 222, 128))
        poly([(130, 270), (180, 210), (236, 270)], (22, 163, 74))
    elif name == "travel agency":
        d.rectangle((0, 280 * s, W * s, H * s), fill=(176, 214, 160))
        box(70, 90, 410, 280, (254, 243, 199), 6, 4)
        for k in range(8):
            poly([(60 + k * 45, 90), (105 + k * 45, 90), (105 + k * 45, 130), (60 + k * 45, 130)], (59, 130, 246) if k % 2 == 0 else (255, 255, 255))
        box(100, 150, 260, 250, (147, 197, 253), 4, 3)
        plane(180, 200, 0.8, (255, 255, 255))
        box(310, 180, 376, 280, (120, 72, 36), 4, 3)
        oval(150, 40, 230, 80, (255, 255, 255), 3)
    elif name == "security check":
        box(40, 200, 440, 270, (148, 163, 184), 10, 4)
        box(110, 130, 370, 210, (51, 65, 85), 14, 4)
        box(130, 146, 350, 200, (15, 23, 42), 6, 0)
        poly([(190, 160), (250, 150), (270, 190), (200, 196)], (96, 165, 250))
        box(300, 160, 330, 190, (250, 204, 21), 4, 2)
        for k in range(2):
            box(70 + k * 20, 276, 126 + k * 20, 306, (226, 232, 240), 6, 3)
        oval(60, 80, 100, 120, (74, 222, 128), 3)
        line(80, 120, 80, 190, 6)
    elif name == "seat belt":
        box(120, 40, 360, 330, (96, 165, 250), 40, 4)
        poly([(180, 40), (250, 40), (350, 330), (270, 330)], (30, 41, 59))
        box(270, 260, 346, 300, (203, 213, 225), 6, 3)
        box(190, 270, 262, 300, (250, 204, 21), 6, 3)
        line(226, 276, 226, 294, 3)
    elif name == "hostel":
        d.rectangle((0, 290 * s, W * s, H * s), fill=(214, 196, 160))
        for k in range(2):
            y = 100 + k * 100
            box(60, y, 270, y + 14, (120, 72, 36), 3, 3)
            line(60, y, 60, y + 80, 8, (120, 72, 36))
            line(270, y, 270, y + 80, 8, (120, 72, 36))
            box(70, y - 26, 260, y, (147, 197, 253), 14, 3)
            box(70, y - 36, 130, y - 10, (255, 255, 255), 10, 3)
        line(60, 100, 60, 300, 8, (120, 72, 36))
        line(270, 100, 270, 300, 8, (120, 72, 36))
        box(310, 120, 420, 290, (203, 213, 225), 6, 4)
        for k in range(3):
            box(322, 134 + k * 52, 408, 168 + k * 52, (226, 232, 240), 4, 2)
    elif name == "sunscreen":
        box(170, 90, 310, 320, (251, 146, 60), 22)
        box(200, 56, 280, 96, (255, 255, 255), 8)
        box(190, 160, 290, 250, (255, 255, 255), 8, 3)
        for k in range(12):
            a = math.radians(k * 30)
            line(240 + math.cos(a) * 24, 205 + math.sin(a) * 24, 240 + math.cos(a) * 34, 205 + math.sin(a) * 34, 3, (245, 158, 11))
        oval(222, 187, 258, 223, (250, 204, 21), 2)
        for k in range(3):
            line(70, 140 + k * 36, 120, 140 + k * 36, 5, (245, 158, 11))
            line(360, 140 + k * 36, 410, 140 + k * 36, 5, (245, 158, 11))
    elif name == "tourist":
        _person(d, 150, 50, 2.5, "m", (234, 88, 12))
        box(120, 150, 180, 230, (22, 163, 74), 8, 3)  # backpack behind
        box(215, 160, 285, 215, (30, 41, 59), 8, 3)  # camera
        oval(235, 170, 265, 200, (96, 165, 250), 3)
        poly([(330, 280), (350, 100), (370, 280)], (203, 213, 225))  # tower
        line(340, 190, 360, 190, 4)
        box(320, 280, 380, 292, (148, 163, 184), 2, 2)
    elif name == "guidebook":
        box(130, 50, 350, 320, (22, 163, 74), 12, 4)
        box(130, 50, 160, 320, (21, 128, 61), 6, 3)
        oval(190, 110, 310, 230, (255, 255, 255), 3)
        poly([(250, 120), (268, 170), (250, 220), (232, 170)], (239, 68, 68))
        poly([(250, 220), (268, 170), (250, 120), (250, 170)], (255, 255, 255))
        oval(242, 162, 258, 178, L, 0)
        line(200, 262, 320, 262, 5, (255, 255, 255))
        line(200, 284, 290, 284, 4, (187, 247, 208))
    elif name == "passport":
        box(130, 30, 350, 330, (30, 58, 138), 14, 4)
        oval(190, 90, 290, 190, (250, 204, 21), 3)
        line(240, 90, 240, 190, 3, (30, 58, 138))
        line(190, 140, 290, 140, 3, (30, 58, 138))
        text_ = None
        d.text((168 * s, 230 * s), "PASSPORT", fill=(250, 204, 21), font=_big_font(32 * s))
        box(200, 270, 280, 290, (250, 204, 21), 4, 0)
    elif name == "luggage":
        box(120, 120, 360, 320, (180, 83, 9), 20, 4)
        box(200, 80, 280, 124, (180, 83, 9), 10, 4)
        line(150, 150, 150, 290, 6, (120, 53, 15))
        line(330, 150, 330, 290, 6, (120, 53, 15))
        box(222, 190, 258, 226, (250, 204, 21), 4, 3)
        oval(150, 318, 180, 346, L, 0)
        oval(300, 318, 330, 346, L, 0)
    elif name == "visa":
        box(60, 70, 420, 290, (255, 255, 255), 10, 4)
        box(80, 90, 400, 130, (30, 58, 138), 4, 0)
        d.text((96 * s, 94 * s), "VISA", fill=(255, 255, 255), font=_big_font(28 * s))
        for k in range(5):
            line(80, 156 + k * 22, 250 - (k % 2) * 40, 156 + k * 22, 4, (148, 163, 184))
        oval(280, 150, 392, 262, (254, 226, 226), 0)
        d.ellipse((282 * s, 152 * s, 390 * s, 260 * s), outline=(220, 38, 38), width=6 * s)
        d.ellipse((296 * s, 166 * s, 376 * s, 246 * s), outline=(220, 38, 38), width=3 * s)
        d.text((304 * s, 186 * s), "OK", fill=(220, 38, 38), font=_big_font(40 * s))
    else:
        raise SystemExit(f"unknown travel icon: {name}")
    return finish(img)


def shop_icon(name: str) -> bytes:
    """Flat original drawing for a shopping or money word (kind "shop")."""
    img, d = canvas()
    s = S
    L = OUTLINE

    def oval(x0, y0, x1, y1, fill, w=3):
        d.ellipse((x0 * s, y0 * s, x1 * s, y1 * s), fill=fill, outline=L if w else None, width=w * s)

    def box(x0, y0, x1, y1, fill, rr=0, w=3):
        d.rounded_rectangle((x0 * s, y0 * s, x1 * s, y1 * s), rr * s, fill=fill, outline=L if w else None, width=w * s)

    def poly(pts, fill):
        pp = [(x * s, y * s) for x, y in pts]
        d.polygon(pp, fill=fill)
        d.line(pp + [pp[0]], fill=L, width=3 * s, joint="curve")

    def line(x0, y0, x1, y1, w=6, col=L):
        d.line((x0 * s, y0 * s, x1 * s, y1 * s), fill=col, width=w * s)

    def text(x, y, t, size, col=L):
        d.text((x * s, y * s), t, fill=col, font=_big_font(size * s))

    GREEN, GOLD = (22, 163, 74), (250, 204, 21)
    if name == "coupon":
        poly([(50, 110), (430, 110), (430, 150), (412, 170), (430, 190), (430, 250), (50, 250), (50, 190), (68, 170), (50, 150)], (254, 226, 226))
        d.line([(300 * s, 116 * s), (300 * s, 244 * s)], fill=(220, 38, 38), width=3 * s)
        text(80, 128, "20%", 74, (220, 38, 38))
        text(88, 210, "OFF", 30, (220, 38, 38))
        for k in range(5):
            line(320, 140 + k * 22, 410, 140 + k * 22, 4, (220, 38, 38))
    elif name == "bargain":
        box(70, 130, 330, 290, (254, 243, 199), 8)
        for k in range(4):
            poly([(60 + k * 66, 90), (126 + k * 66, 90), (126 + k * 66, 130), (60 + k * 66, 130)], (220, 38, 38) if k % 2 == 0 else (255, 255, 255))
        box(110, 190, 190, 270, (251, 146, 60), 8)
        oval(220, 200, 290, 270, (74, 222, 128), 3)
        poly([(350, 120), (420, 120), (440, 160), (420, 200), (350, 200), (370, 160)], (250, 204, 21))
        text(368, 138, "$", 46)
    elif name == "credit card":
        box(60, 80, 420, 280, (59, 130, 246), 20, 4)
        box(60, 120, 420, 160, (30, 41, 59), 0, 0)
        box(90, 190, 160, 230, GOLD, 8, 3)
        for k in range(4):
            line(190 + k * 54, 250, 220 + k * 54, 250, 6, (219, 234, 254))
        oval(340, 200, 380, 240, (239, 68, 68), 0)
        oval(360, 200, 400, 240, (251, 146, 60), 0)
    elif name == "wallet":
        box(70, 100, 410, 290, (146, 83, 36), 22, 4)
        box(70, 100, 410, 160, (180, 105, 50), 22, 4)
        box(320, 180, 410, 240, (120, 70, 30), 14, 4)
        oval(350, 200, 376, 226, GOLD, 3)
        d.line([(90 * s, 252 * s), (300 * s, 252 * s)], fill=(250, 213, 172), width=3 * s)
        box(160, 70, 300, 110, (74, 222, 128), 4, 3)
        text(204, 74, "$", 34)
    elif name == "bank account":
        poly([(240, 40), (60, 120), (420, 120)], (226, 232, 240))
        for k in range(5):
            box(84 + k * 66, 130, 104 + k * 66, 250, (248, 250, 252), 2, 3)
        box(60, 250, 420, 280, (203, 213, 225), 2, 3)
        box(40, 280, 440, 304, (148, 163, 184), 2, 3)
        oval(220, 70, 260, 110, GOLD, 3)
        text(232, 70, "$", 32)
    elif name == "ATM":
        box(130, 40, 350, 320, (71, 85, 105), 16, 4)
        box(160, 70, 320, 150, (96, 165, 250), 6, 3)
        text(204, 84, "ATM", 40, (255, 255, 255))
        for r_ in range(3):
            for c_ in range(3):
                box(170 + c_ * 40, 170 + r_ * 26, 198 + c_ * 40, 190 + r_ * 26, (226, 232, 240), 3, 2)
        box(290, 200, 320, 212, (15, 23, 42), 2, 0)
        box(170, 262, 310, 290, (15, 23, 42), 4, 0)
        box(186, 268, 294, 284, (74, 222, 128), 2, 0)
    elif name == "price tag":
        poly([(110, 80), (330, 80), (420, 180), (330, 280), (110, 280)], (250, 204, 21))
        oval(130, 160, 170, 200, BG, 3)
        text(190, 124, "$9", 96)
        d.line([(150 * s, 180 * s), (60 * s, 100 * s)], fill=L, width=3 * s)
    elif name == "fitting room":
        box(90, 50, 390, 320, (186, 230, 253), 8, 4)
        box(110, 70, 370, 150, (226, 232, 240), 4, 0)
        poly([(160, 140), (320, 140), (330, 320), (150, 320)], (252, 165, 165))
        d.line([(160 * s, 140 * s), (160 * s, 320 * s)], fill=L, width=3 * s)
        d.line([(320 * s, 140 * s), (320 * s, 320 * s)], fill=L, width=3 * s)
        for k in range(4):
            line(190 + k * 40, 140, 190 + k * 40, 316, 3, (239, 68, 68))
        box(200, 40, 280, 70, GOLD, 6, 3)
        text(214, 42, "FIT", 24)
    elif name == "sale":
        d.polygon(poly_star(240 * s, 180 * s, 150 * s, 118 * s, 14), fill=(220, 38, 38), outline=L)
        text(160, 118, "SALE", 72, (255, 255, 255))
        text(176, 218, "-50%", 50, GOLD)
    elif name == "customer":
        _person(d, 140, 50, 2.6, "f", (219, 39, 119))
        box(250, 140, 400, 240, (30, 41, 59), 8, 3)
        box(264, 154, 386, 200, (147, 197, 253), 4, 2)
        d.rectangle((230 * s, 240 * s, 420 * s, 300 * s), fill=(148, 163, 184), outline=L, width=3 * s)
        box(250, 250, 300, 290, (74, 222, 128), 4, 2)
        oval(190, 220, 230, 260, GOLD, 3)
    elif name == "gift card":
        box(60, 90, 420, 270, (244, 114, 182), 18, 4)
        box(60, 160, 420, 190, (255, 255, 255), 0, 0)
        box(210, 90, 270, 270, (253, 224, 71), 0, 0)
        box(60, 160, 420, 190, (253, 224, 71), 0, 0)
        poly([(240, 90), (200, 50), (170, 80), (240, 90)], (253, 224, 71))
        poly([(240, 90), (280, 50), (310, 80), (240, 90)], (253, 224, 71))
        d.rectangle((60 * s, 90 * s, 420 * s, 270 * s), outline=L, width=4 * s)
    elif name == "shopping list":
        box(130, 40, 350, 320, (254, 249, 195), 10, 4)
        for k in range(5):
            box(158, 84 + k * 46, 180, 106 + k * 46, (255, 255, 255), 3, 3)
            line(200, 96 + k * 46, 320 - (k % 3) * 30, 96 + k * 46, 5, (148, 163, 184))
        line(160, 96, 174, 108, 4, GREEN)
        line(174, 108, 184, 82, 4, GREEN)
        line(160, 142, 174, 154, 4, GREEN)
        line(174, 154, 184, 128, 4, GREEN)
    elif name == "shopping bag":
        d.arc((180 * s, 30 * s, 300 * s, 160 * s), 180, 360, fill=L, width=8 * s)
        poly([(120, 100), (360, 100), (384, 320), (96, 320)], (251, 146, 60))
        oval(160, 80, 184, 104, (255, 255, 255), 3)
        oval(296, 80, 320, 104, (255, 255, 255), 3)
        text(200, 190, "$", 80, (255, 255, 255))
    elif name == "tip":
        oval(150, 130, 330, 300, (203, 213, 225), 4)
        d.rectangle((152 * s, 130 * s, 328 * s, 160 * s), fill=BG)
        box(130, 112, 350, 144, (148, 163, 184), 8, 4)
        for (cx, cy) in ((190, 270), (250, 276), (300, 262), (220, 236)):
            oval(cx - 20, cy - 20, cx + 20, cy + 20, GOLD, 3)
        text(222, 74, "TIP", 34)
        oval(300, 40, 340, 80, GOLD, 3)
        line(298, 64, 340, 40, 1)
    elif name == "bill":
        poly([(150, 40), (330, 40), (330, 320), (310, 304), (290, 320), (270, 304), (250, 320), (230, 304), (210, 320), (190, 304), (170, 320), (150, 304)], (255, 255, 255))
        text(184, 56, "BILL", 40)
        for k in range(5):
            line(176, 128 + k * 30, 250, 128 + k * 30, 4, (148, 163, 184))
            line(270, 128 + k * 30, 304, 128 + k * 30, 4, (148, 163, 184))
        line(176, 278, 304, 278, 3)
    else:
        raise SystemExit(f"unknown shop icon: {name}")
    return finish(img)


def cook_icon(name: str) -> bytes:
    """Flat original drawing of a cooking action (kind "cook")."""
    img, d = canvas()
    s = S
    L = OUTLINE

    def oval(x0, y0, x1, y1, fill, w=3):
        d.ellipse((x0 * s, y0 * s, x1 * s, y1 * s), fill=fill, outline=L if w else None, width=w * s)

    def box(x0, y0, x1, y1, fill, rr=0, w=3):
        d.rounded_rectangle((x0 * s, y0 * s, x1 * s, y1 * s), rr * s, fill=fill, outline=L if w else None, width=w * s)

    def poly(pts, fill):
        pp = [(x * s, y * s) for x, y in pts]
        d.polygon(pp, fill=fill)
        d.line(pp + [pp[0]], fill=L, width=3 * s, joint="curve")

    def line(x0, y0, x1, y1, w=6, col=L):
        d.line((x0 * s, y0 * s, x1 * s, y1 * s), fill=col, width=w * s)

    d.rectangle((0, 280 * s, W * s, H * s), fill=(214, 196, 160))
    if name == "peel":
        oval(150, 130, 290, 270, (180, 120, 70))
        for k in range(4):
            line(180 + k * 20, 150, 170 + k * 22, 250, 3, (150, 96, 54))
        poly([(250, 120), (330, 80), (350, 100), (270, 150)], (170, 178, 192))
        box(320, 70, 400, 96, (234, 88, 12), 10)
        d.arc((250 * s, 200 * s, 380 * s, 300 * s), 200, 350, fill=(250, 213, 172), width=10 * s)
        d.arc((250 * s, 200 * s, 380 * s, 300 * s), 200, 350, fill=L, width=2 * s)
    elif name == "grate":
        poly([(150, 80), (290, 80), (330, 300), (110, 300)], (170, 178, 192))
        for r_ in range(5):
            for c_ in range(4):
                oval(158 + c_ * 36 + r_ * 4, 110 + r_ * 38, 174 + c_ * 36 + r_ * 4, 124 + r_ * 38, (96, 104, 120), 2)
        poly([(360, 90), (420, 70), (430, 130), (370, 150)], (250, 204, 21))
        for k in range(5):
            oval(310 + (k % 3) * 22 + k * 3, 290 + (k % 2) * 6, 320 + (k % 3) * 22 + k * 3, 298 + (k % 2) * 6, (253, 224, 71), 1)
    elif name == "stir":
        box(100, 150, 380, 290, (71, 85, 105), 24)
        oval(100, 130, 380, 190, (180, 70, 50))
        d.arc((160 * s, 140 * s, 320 * s, 180 * s), 200, 20, fill=(254, 215, 170), width=5 * s)
        box(236, 20, 252, 190, (160, 100, 50), 6)
        poly([(404, 150), (430, 180), (404, 210)], (59, 130, 246))
        d.arc((120 * s, 110 * s, 360 * s, 200 * s), 20, 120, fill=(59, 130, 246), width=5 * s)
        box(60, 175, 100, 190, (71, 85, 105), 6)
    elif name == "bake":
        box(90, 60, 390, 300, (203, 213, 225), 18)
        box(110, 100, 370, 270, (30, 41, 59), 12)
        box(130, 120, 350, 250, (255, 190, 90), 8)
        poly([(200, 200), (280, 200), (270, 240), (210, 240)], (180, 100, 50))
        oval(210, 170, 270, 215, (250, 213, 172))
        oval(230, 156, 250, 176, (220, 38, 38), 2)
        for k in range(3):
            oval(120 + k * 70, 70, 140 + k * 70, 90, (96, 104, 120), 2)
    elif name == "marinate":
        oval(90, 120, 390, 300, (226, 232, 240))
        oval(110, 140, 370, 280, (120, 60, 30), 0)
        for (cx, cy) in ((180, 200), (240, 220), (290, 190), (230, 170)):
            poly([(cx - 36, cy), (cx - 10, cy - 24), (cx + 30, cy - 14), (cx + 40, cy + 14), (cx, cy + 26)], (250, 150, 130))
        for (gx, gy) in ((150, 240), (320, 230), (260, 150)):
            oval(gx - 8, gy - 8, gx + 8, gy + 8, (250, 250, 240), 2)
        oval(400, 70, 450, 120, (22, 163, 74), 3)
        box(380, 160, 440, 280, (120, 60, 30), 10)
    else:
        raise SystemExit(f"unknown cook icon: {name}")
    return finish(img)


def med_icon(name: str) -> bytes:
    """Flat original drawing for a body or medical word (kind "med")."""
    img, d = canvas()
    s = S
    L = OUTLINE

    def oval(x0, y0, x1, y1, fill, w=3):
        d.ellipse((x0 * s, y0 * s, x1 * s, y1 * s), fill=fill, outline=L if w else None, width=w * s)

    def box(x0, y0, x1, y1, fill, rr=0, w=3):
        d.rounded_rectangle((x0 * s, y0 * s, x1 * s, y1 * s), rr * s, fill=fill, outline=L if w else None, width=w * s)

    def poly(pts, fill):
        pp = [(x * s, y * s) for x, y in pts]
        d.polygon(pp, fill=fill)
        d.line(pp + [pp[0]], fill=L, width=3 * s, joint="curve")

    def line(x0, y0, x1, y1, w=6, col=L):
        d.line((x0 * s, y0 * s, x1 * s, y1 * s), fill=col, width=w * s)

    def text(x, y, t, size, col=L):
        d.text((x * s, y * s), t, fill=col, font=_big_font(size * s))

    BONE = (250, 245, 232)
    if name == "skeleton":
        oval(205, 30, 275, 100, BONE)
        box(222, 92, 258, 110, BONE, 4)
        oval(225, 58, 239, 72, L, 0)
        oval(243, 58, 257, 72, L, 0)
        line(232, 90, 248, 90, 3)
        line(240, 110, 240, 230, 8, (214, 205, 184))
        for k in range(5):
            d.arc(((190 + 0) * s, (116 + k * 20) * s, 290 * s, (150 + k * 20) * s), 0, 180, fill=L, width=4 * s)
            d.arc((190 * s, (116 + k * 20) * s, 290 * s, (150 + k * 20) * s), 0, 180, fill=BONE, width=2 * s)
        poly([(200, 230), (280, 230), (292, 262), (240, 276), (188, 262)], BONE)
        for sx in (-1, 1):
            line(240 + sx * 50, 120, 240 + sx * 110, 210, 8, BONE)
            line(240 + sx * 110, 210, 240 + sx * 120, 290, 8, BONE)
            line(240 + sx * 22, 270, 240 + sx * 36, 340, 9, BONE)
    elif name == "heartbeat":
        d.rectangle((0, 0, W * s, H * s), fill=(15, 23, 42))
        pts = [(20, 190), (110, 190), (140, 190), (160, 110), (190, 280), (215, 150), (235, 190), (330, 190), (360, 190), (380, 150), (400, 230), (420, 190), (460, 190)]
        d.line([(x * s, y * s) for x, y in pts], fill=(74, 222, 128), width=7 * s, joint="curve")
        for gx in range(0, 480, 40):
            d.line((gx * s, 0, gx * s, H * s), fill=(30, 41, 59), width=1 * s)
        oval(205, 40, 275, 100, (244, 63, 94))
    elif name == "calorie":
        box(130, 40, 350, 320, (255, 255, 255), 8, 4)
        text(150, 54, "Nutrition", 34)
        text(150, 100, "Facts", 34)
        line(145, 150, 335, 150, 8)
        text(150, 162, "Calories", 34)
        text(150, 204, "210", 56)
        for k in range(3):
            line(150, 276 + k * 14, 330 - (k % 2) * 40, 276 + k * 14, 3, (148, 163, 184))
        d.polygon(poly_star(400 * s, 80 * s, 36 * s, 16 * s, 8), fill=(250, 204, 21), outline=L)
    elif name == "scale":
        box(110, 130, 370, 300, (226, 232, 240), 26, 4)
        box(170, 150, 310, 210, (255, 255, 255), 10, 3)
        d.arc((190 * s, 165 * s, 290 * s, 250 * s), 180, 360, fill=L, width=4 * s)
        line(240, 205, 262, 170, 4, (220, 38, 38))
        oval(230, 195, 250, 215, L, 0)
        box(150, 240, 330, 268, (203, 213, 225), 10, 2)
        text(196, 130, "kg", 22)
    elif name == "prescription":
        box(110, 30, 370, 330, (255, 255, 255), 10, 4)
        text(134, 40, "Rx", 100, (37, 99, 235))
        for k in range(5):
            line(140, 190 + k * 26, 340 - (k % 3) * 30, 190 + k * 26, 4, (148, 163, 184))
        oval(300, 56, 350, 106, (254, 226, 226), 3)
        line(318, 70, 332, 92, 4, (220, 38, 38))
    elif name == "contact lens":
        oval(150, 90, 330, 220, (219, 234, 254), 4)
        oval(180, 112, 300, 198, (255, 255, 255), 2)
        d.arc((150 * s, 90 * s, 330 * s, 220 * s), 200, 300, fill=(255, 255, 255), width=5 * s)
        box(150, 250, 330, 320, (203, 213, 225), 18, 4)
        box(150, 250, 240, 320, (147, 197, 253), 18, 4)
        text(171, 262, "L", 40)
        text(268, 262, "R", 40)
    elif name == "hearing aid":
        d.arc((150 * s, 40 * s, 330 * s, 260 * s), 200, 20, fill=(250, 213, 172), width=26 * s)
        d.arc((150 * s, 40 * s, 330 * s, 260 * s), 200, 20, fill=L, width=3 * s)
        d.arc((190 * s, 70 * s, 310 * s, 200 * s), 220, 40, fill=(180, 140, 110), width=14 * s)
        box(270, 130, 310, 230, (96, 165, 250), 18, 3)
        oval(284, 232, 300, 248, (96, 165, 250), 3)
        for k in range(3):
            line(350 + k * 14, 150 + k * 10, 350 + k * 14, 170 + k * 10, 4)
    else:
        raise SystemExit(f"unknown med icon: {name}")
    return finish(img)


def edu_icon(name: str) -> bytes:
    """Flat original drawing for a school or study word (kind "edu")."""
    img, d = canvas()
    s = S
    L = OUTLINE

    def oval(x0, y0, x1, y1, fill, w=3):
        d.ellipse((x0 * s, y0 * s, x1 * s, y1 * s), fill=fill, outline=L if w else None, width=w * s)

    def box(x0, y0, x1, y1, fill, rr=0, w=3):
        d.rounded_rectangle((x0 * s, y0 * s, x1 * s, y1 * s), rr * s, fill=fill, outline=L if w else None, width=w * s)

    def poly(pts, fill):
        pp = [(x * s, y * s) for x, y in pts]
        d.polygon(pp, fill=fill)
        d.line(pp + [pp[0]], fill=L, width=3 * s, joint="curve")

    def line(x0, y0, x1, y1, w=6, col=L):
        d.line((x0 * s, y0 * s, x1 * s, y1 * s), fill=col, width=w * s)

    def text(x, y, t, size, col=L):
        d.text((x * s, y * s), t, fill=col, font=_big_font(size * s))

    def cap(cx, cy, k=1.0):
        poly([(cx - 90 * k, cy), (cx, cy - 40 * k), (cx + 90 * k, cy), (cx, cy + 40 * k)], (30, 41, 59))
        poly([(cx - 50 * k, cy + 22 * k), (cx - 50 * k, cy + 62 * k), (cx, cy + 80 * k), (cx + 50 * k, cy + 62 * k), (cx + 50 * k, cy + 22 * k), (cx, cy + 40 * k)], (51, 65, 85))
        line(cx + 80 * k, cy, cx + 80 * k, cy + 60 * k, 4, (250, 204, 21))
        oval(cx + 72 * k, cy + 56 * k, cx + 88 * k, cy + 72 * k, (250, 204, 21), 2)

    d.rectangle((0, 290 * s, W * s, H * s), fill=(214, 196, 160))
    if name == "graduation":
        cap(240, 120, 1.5)
        box(150, 230, 330, 300, (255, 255, 255), 6, 3)
        line(170, 252, 310, 252, 4, (148, 163, 184))
        line(170, 272, 270, 272, 4, (148, 163, 184))
        for k in range(4):
            d.polygon(poly_star((90 + k * 100) * s, 60 * s, 14 * s, 6 * s, 5), fill=(250, 204, 21), outline=L)
    elif name == "graduation gown":
        poly([(170, 120), (310, 120), (350, 320), (130, 320)], (30, 41, 59))
        poly([(200, 120), (240, 180), (280, 120)], (255, 255, 255))
        poly([(240, 130), (225, 170), (240, 320), (255, 170)], (250, 204, 21))
        oval(205, 20, 275, 90, (250, 213, 172))
        cap(240, 22, 0.8)
    elif name == "dormitory":
        d.rectangle((0, 0, W * s, 290 * s), fill=(254, 243, 199))
        for k, x in enumerate((40, 270)):
            box(x, 190, x + 170, 210, (120, 72, 36), 4, 3)
            box(x, 150, x + 170, 190, (147, 197, 253), 10, 3)
            box(x, 140, x + 56, 170, (255, 255, 255), 8, 3)
            line(x, 210, x, 300, 7, (120, 72, 36))
            line(x + 170, 210, x + 170, 300, 7, (120, 72, 36))
        box(185, 60, 295, 190, (226, 232, 240), 6, 3)
        box(195, 70, 285, 130, (147, 197, 253), 4, 2)
        line(240, 70, 240, 130, 3)
    elif name == "laboratory":
        d.rectangle((0, 0, W * s, 290 * s), fill=(241, 245, 249))
        box(40, 230, 440, 300, (148, 163, 184), 6, 3)
        poly([(90, 120), (130, 120), (130, 170), (160, 230), (60, 230), (90, 170)], (200, 230, 250))
        poly([(75, 190), (145, 190), (160, 230), (60, 230)], (74, 222, 128))
        box(200, 140, 250, 230, (255, 255, 255), 8, 3)
        box(200, 140, 250, 170, (239, 68, 68), 4, 2)
        poly([(300, 230), (360, 230), (350, 190), (310, 190)], (96, 165, 250))
        oval(310, 150, 350, 190, (96, 165, 250))
        box(380, 90, 410, 230, (255, 255, 255), 6, 3)
        line(380, 130, 410, 130, 2)
        line(380, 170, 410, 170, 2)
    elif name == "tutor":
        d.rectangle((0, 0, W * s, 290 * s), fill=(241, 245, 249))
        _person(d, 140, 50, 2.4, "f", (124, 58, 237))
        _person(d, 330, 100, 1.9, "m", (234, 88, 12))
        box(200, 230, 420, 260, (203, 213, 225), 4, 3)
        box(240, 200, 300, 232, (255, 255, 255), 3, 3)
        line(250, 212, 290, 212, 3, (148, 163, 184))
        box(60, 40, 130, 100, (22, 101, 52), 4, 3)
        line(72, 62, 118, 62, 3, (240, 244, 240))
    elif name == "essay":
        box(120, 30, 360, 330, (255, 255, 255), 8, 4)
        text(150, 44, "My Town", 40)
        for k in range(9):
            line(145, 120 + k * 22, 335 - (k % 4) * 24, 120 + k * 22, 3, (148, 163, 184))
        line(350, 120, 420, 60, 8, (250, 204, 21))
        poly([(414, 54), (430, 40), (440, 54), (426, 70)], (234, 88, 12))
    elif name == "flashcard":
        for k, (x, y, col) in enumerate(((70, 90, (254, 240, 138)), (140, 70, (187, 247, 208)), (210, 56, (186, 230, 253)))):
            box(x, y, x + 200, y + 150, col, 12, 4)
        text(240, 104, "apple", 38)
        line(236, 160, 370, 160, 4)
        text(240, 172, "quả táo", 28, (71, 85, 105))
    elif name == "highlighter":
        box(100, 200, 380, 320, (255, 255, 255), 6, 3)
        for k in range(3):
            line(120, 230 + k * 28, 360 - k * 30, 230 + k * 28, 4, (148, 163, 184))
        d.rectangle((120 * s, 222 * s, 280 * s, 238 * s), fill=(253, 224, 71))
        line(120, 230, 280, 230, 4, (148, 163, 184))
        poly([(240, 50), (290, 30), (330, 120), (280, 140)], (250, 204, 21))
        poly([(290, 30), (310, 24), (352, 114), (330, 120)], (253, 224, 71))
        poly([(280, 140), (330, 120), (316, 168), (274, 186)], (74, 222, 128))
    elif name == "worksheet":
        box(110, 30, 370, 330, (255, 255, 255), 8, 4)
        text(134, 40, "Worksheet", 30)
        for k in range(5):
            text(134, 96 + k * 44, f"{k + 1}.", 26)
            line(170, 118 + k * 44, 340, 118 + k * 44, 3, (148, 163, 184))
        line(330, 70, 410, 40, 9, (250, 204, 21))
        poly([(404, 34), (420, 24), (428, 36), (412, 48)], (234, 88, 12))
    elif name == "projector":
        d.rectangle((0, 0, W * s, 290 * s), fill=(241, 245, 249))
        box(240, 30, 450, 190, (255, 255, 255), 4, 4)
        poly([(270, 160), (310, 100), (350, 130), (390, 70), (430, 160)], (74, 222, 128))
        oval(380, 50, 420, 90, (250, 204, 21), 2)
        box(50, 200, 190, 270, (51, 65, 85), 12, 3)
        oval(150, 214, 184, 248, (96, 165, 250), 3)
        poly([(184, 220), (240, 70), (240, 190), (184, 244)], (254, 249, 195))
    elif name == "science fair":
        box(60, 40, 420, 230, (59, 130, 246), 6, 4)
        box(80, 60, 220, 130, (255, 255, 255), 3, 2)
        box(240, 60, 400, 130, (255, 255, 255), 3, 2)
        line(80, 160, 400, 160, 4, (255, 255, 255))
        line(80, 190, 360, 190, 4, (255, 255, 255))
        poly([(130, 130), (170, 130), (170, 100), (190, 200), (110, 200), (130, 100)], (200, 230, 250))
        oval(300, 70, 360, 120, (239, 68, 68), 2)
        poly([(280, 230), (320, 230), (320, 300), (280, 300)], (96, 165, 250))
        d.polygon(poly_star(240 * s, 270 * s, 26 * s, 11 * s, 5), fill=(250, 204, 21), outline=L)
    elif name == "quiz":
        text(190, 10, "?", 200, (37, 99, 235))
        for k, lab in enumerate("ABCD"):
            oval(80 + k * 86, 250, 130 + k * 86, 300, (255, 255, 255), 4)
            text(96 + k * 86, 256, lab, 34)
        oval(80 + 2 * 86, 250, 130 + 2 * 86, 300, (134, 239, 172), 4)
        line(266, 276, 280, 290, 5, (22, 101, 52))
        line(280, 290, 300, 258, 5, (22, 101, 52))
    elif name == "group project":
        box(60, 40, 260, 180, (254, 249, 195), 6, 4)
        text(84, 52, "Project", 32)
        for k in range(3):
            line(84, 108 + k * 18, 230 - k * 20, 108 + k * 18, 3, (148, 163, 184))
        for k in range(3):
            _person(d, 280 + k * 62, 160, 1.0, "m" if k % 2 == 0 else "f", [(22, 163, 74), (234, 88, 12), (37, 99, 235)][k])
        box(60, 260, 420, 285, (203, 213, 225), 4, 3)
    elif name == "assignment":
        box(110, 30, 370, 330, (255, 255, 255), 8, 4)
        text(140, 44, "Homework", 32)
        for k in range(4):
            box(140, 110 + k * 50, 160, 130 + k * 50, (255, 255, 255), 3, 3)
            line(180, 120 + k * 50, 340 - (k % 3) * 40, 120 + k * 50, 3, (148, 163, 184))
        line(142, 120, 150, 130, 4, (22, 163, 74))
        line(150, 130, 162, 108, 4, (22, 163, 74))
        text(260, 292, "Due Mon", 24, (220, 38, 38))
    else:
        raise SystemExit(f"unknown edu icon: {name}")
    return finish(img)


SETTING_WORDS = [
    ("restaurant", ("waiter", "menu", "restaurant", "dish", "meal", "table", "bill", "order", "drink", "chair", "takeaway", "fast food", "burger", "soup", "water", "allergy", "chef", "tip")),
    ("airport", ("airport", "passport", "boarding", "gate", "luggage", "baggage", "flight", "customs", "check-in", "seat", "departure", "plane")),
    ("hotel", ("hotel", "reception", "room", "wake-up", "breakfast", "check out", "check in", "wifi", "air conditioner", "taxi")),
    ("shop", ("shop", "price", "market", "bargain", "customer", "fitting", "cheaper", "gift", "sale", "receipt", "card", "clothes", "sizes", "cashier", "checkout", "wrapping", "return", "tag")),
    ("phone", ("phone", "call", "voicemail", "answering", "signal", "battery", "message", "switchboard", "headset", "telephone", "number")),
    ("street", ("street", "map", "directions", "bridge", "bus", "corner", "turn", "road", "station", "lost", "walking", "junction", "building", "ticket office")),
    ("classroom", ("classroom", "student", "teacher", "pen", "classmates", "english", "notebook", "learning")),
    ("office", ("office", "colleagues", "meeting", "business", "formal", "manager", "desk", "reception")),
    ("park", ("park", "weather", "sunny", "picnic", "outdoor", "walk", "weekend", "friends", "waving", "goodbye", "cold", "scarf")),
    ("home", ("home", "window", "neighbours", "family", "door", "favour", "hug", "helping", "box", "gift")),
]
PROP_WORDS = [
    ("menu", ("menu", "order", "ordering")), ("bill", ("bill", "receipt", "card", "paying", "pay", "tip", "price", "change")),
    ("map", ("map",)), ("phone", ("phone", "call", "voicemail", "battery", "signal", "smartphone", "message", "answering")),
    ("passport", ("passport",)), ("suitcase", ("luggage", "baggage", "suitcase", "bags", "bag", "wrapping")),
    ("key", ("key", "reception", "check in", "check out", "reservation")), ("ticket", ("ticket", "boarding")),
    ("coffee", ("coffee", "cafe", "tea", "drink", "water")), ("gift", ("gift", "present")),
    ("clock", ("clock", "time", "watch", "minutes", "waiting")), ("umbrella", ("weather", "rain", "cold")),
    ("tray", ("tray", "meal", "dish", "food", "soup", "takeaway", "burger")),
]


def infer_scene(term: str, query: str) -> tuple[str, str]:
    text = (term + " " + query).lower()
    best, best_n = "street", 0
    for name, words in SETTING_WORDS:
        n = sum(1 for w in words if w in text)
        if n > best_n:
            best, best_n = name, n
    prop = "none"
    for name, words in PROP_WORDS:
        if any(w in text for w in words):
            prop = name
            break
    return best, prop


def _wrap(text: str, width: int) -> list[str]:
    lines, cur = [], ""
    for w in text.split():
        trial = (cur + " " + w).strip()
        if len(trial) > width and cur:
            lines.append(cur)
            cur = w
        else:
            cur = trial
    if cur:
        lines.append(cur)
    return lines


FORCED_SETTING = {"C03": "restaurant", "C04": "street", "C05": "shop", "C06": "phone"}


def scene_image(term: str, query: str, seed: int, code: str = "") -> bytes:
    """Original illustration of a conversational situation with the phrase in a speech bubble (kind "scene")."""
    from PIL import ImageFont

    setting, prop = infer_scene(term, query)
    setting = FORCED_SETTING.get(code.upper(), setting)
    if setting in ("airport", "hotel", "restaurant", "phone") and code.upper() in ("C01", "C02", "C08"):
        setting = "park" if code.upper() == "C08" else "office"
    img, d = canvas()
    s = S
    L = OUTLINE
    PAL = [(37, 99, 235), (234, 88, 12), (22, 163, 74), (219, 39, 119), (124, 58, 237), (14, 165, 233)]

    def box(x0, y0, x1, y1, fill, rr=0, w=3):
        d.rounded_rectangle((x0 * s, y0 * s, x1 * s, y1 * s), rr * s, fill=fill, outline=L if w else None, width=w * s)

    def oval(x0, y0, x1, y1, fill, w=3):
        d.ellipse((x0 * s, y0 * s, x1 * s, y1 * s), fill=fill, outline=L if w else None, width=w * s)

    def poly(pts, fill):
        pp = [(x * s, y * s) for x, y in pts]
        d.polygon(pp, fill=fill)
        d.line(pp + [pp[0]], fill=L, width=3 * s, joint="curve")

    def line(x0, y0, x1, y1, w=4, col=L):
        d.line((x0 * s, y0 * s, x1 * s, y1 * s), fill=col, width=w * s)

    # background
    floor_y = 300
    wall = {"street": (186, 230, 253), "park": (186, 230, 253), "office": (226, 232, 240), "classroom": (254, 249, 195), "restaurant": (254, 226, 202),
            "shop": (237, 233, 254), "airport": (219, 234, 254), "hotel": (254, 243, 199), "home": (220, 252, 231), "phone": (241, 245, 249)}[setting]
    d.rectangle((0, 0, W * s, H * s), fill=wall)
    d.rectangle((0, floor_y * s, W * s, H * s), fill={"street": (156, 163, 175), "park": (134, 239, 172)}.get(setting, (214, 196, 160)))
    if setting in ("street", "park"):
        if setting == "street":
            for k, (x, h, col) in enumerate(((10, 150, (203, 213, 225)), (130, 190, (254, 215, 170)), (290, 130, (187, 247, 208)), (390, 170, (221, 214, 254)))):
                box(x, floor_y - h, x + 110, floor_y, col, 0, 2)
                for r_ in range(h // 44):
                    for c_ in range(2):
                        box(x + 16 + c_ * 46, floor_y - h + 14 + r_ * 44, x + 40 + c_ * 46, floor_y - h + 40 + r_ * 44, (147, 197, 253), 2, 2)
        else:
            for x in (30, 150, 400):
                line(x, floor_y, x, floor_y - 120, 12, (120, 72, 36))
                oval(x - 56, floor_y - 210, x + 56, floor_y - 100, (74, 222, 128), 3)
            oval(400, 20, 450, 70, (250, 204, 21), 3)
    elif setting == "restaurant":
        box(30, 40, 150, 140, (147, 197, 253), 4, 3)
        box(330, 50, 450, 130, (254, 243, 199), 4, 3)
        late_table = True
    elif setting == "shop":
        for r_ in range(3):
            line(20, 70 + r_ * 56, 460, 70 + r_ * 56, 6, (180, 120, 70))
            for c_ in range(9):
                box(30 + c_ * 49, 40 + r_ * 56, 62 + c_ * 49, 70 + r_ * 56, PAL[(c_ + r_) % 6], 3, 2)
    elif setting == "airport":
        box(20, 30, 460, 190, (186, 230, 253), 4, 3)
        poly([(180, 130), (300, 124), (330, 134), (300, 144)], (255, 255, 255))
        poly([(230, 130), (260, 90), (275, 90), (262, 130)], (203, 213, 225))
        box(300, 214, 470, 262, (148, 163, 184), 6, 3)
    elif setting == "hotel":
        box(20, 40, 150, 140, (254, 215, 170), 4, 3)
        for k in range(6):
            oval(36 + (k % 3) * 36, 56 + (k // 3) * 40, 52 + (k % 3) * 36, 72 + (k // 3) * 40, (250, 204, 21), 2)
        box(290, 230, 470, 262, (146, 83, 36), 6, 3)
    elif setting == "classroom":
        box(40, 30, 300, 150, (22, 101, 52), 4, 4)
        line(60, 70, 240, 70, 3, (240, 244, 240))
        line(60, 100, 200, 100, 3, (240, 244, 240))
        box(60, 236, 200, 252, (180, 120, 70), 6, 3)
        box(300, 236, 440, 252, (180, 120, 70), 6, 3)
    elif setting == "office":
        box(300, 30, 460, 150, (147, 197, 253), 4, 3)
        box(30, 232, 200, 252, (180, 120, 70), 6, 3)
        box(60, 190, 140, 232, (51, 65, 85), 6, 3)
    elif setting == "home":
        box(30, 40, 170, 140, (147, 197, 253), 4, 3)
        box(300, 200, 470, 300, (251, 146, 60), 14, 3)
        oval(400, 150, 440, 200, (74, 222, 128), 3)
    elif setting == "phone":
        for k in range(6):
            oval(20 + k * 80, 150 + (k % 2) * 40, 40 + k * 80, 170 + (k % 2) * 40, (219, 234, 254), 0)

    # people
    late_table = locals().get('late_table', False)
    two = setting != "phone" or "call" not in term.lower()
    g1, g2 = ("m", "f") if seed % 2 == 0 else ("f", "m")
    c1, c2 = PAL[seed % 6], PAL[(seed + 2) % 6]
    if setting == "phone":
        xa, xb = 150, 330
    else:
        xa, xb = 150, 330
    _person(d, xa, 128, 2.2, g1, c1)
    if two:
        _person(d, xb, 128, 2.2, g2, c2)
    if late_table:
        box(40, 268, 440, 286, (180, 120, 70), 6, 3)
        line(110, 286, 110, 340, 9, (120, 72, 36))
        line(370, 286, 370, 340, 9, (120, 72, 36))
    # prop between the people
    px, py = 240, 240
    if prop == "menu":
        box(px - 34, py - 40, px + 34, py + 30, (255, 255, 255), 5, 3)
        for k in range(3):
            line(px - 22, py - 24 + k * 16, px + 22, py - 24 + k * 16, 3, (148, 163, 184))
    elif prop == "bill":
        poly([(px - 28, py - 44), (px + 28, py - 44), (px + 28, py + 30), (px + 14, py + 22), (px, py + 30), (px - 14, py + 22), (px - 28, py + 30)], (255, 255, 255))
        for k in range(3):
            line(px - 18, py - 28 + k * 16, px + 18, py - 28 + k * 16, 3, (148, 163, 184))
    elif prop == "map":
        poly([(px - 40, py - 36), (px - 14, py - 28), (px + 14, py - 36), (px + 40, py - 28), (px + 40, py + 30), (px + 14, py + 38), (px - 14, py + 30), (px - 40, py + 38)], (254, 240, 138))
        line(px - 30, py + 10, px + 30, py - 10, 3, (59, 130, 246))
        oval(px + 14, py - 24, px + 26, py - 12, (239, 68, 68), 2)
    elif prop == "phone":
        box(px - 22, py - 44, px + 22, py + 30, (30, 41, 59), 8, 3)
        box(px - 16, py - 32, px + 16, py + 14, (147, 197, 253), 3, 0)
    elif prop == "passport":
        box(px - 26, py - 36, px + 26, py + 30, (30, 58, 138), 4, 3)
        oval(px - 12, py - 20, px + 12, py + 4, (250, 204, 21), 2)
    elif prop == "suitcase":
        box(px - 38, py - 20, px + 38, py + 40, (180, 83, 9), 8, 3)
        box(px - 14, py - 36, px + 14, py - 20, (180, 83, 9), 4, 3)
    elif prop == "key":
        oval(px - 24, py - 30, px, py - 6, (250, 204, 21), 3)
        line(px, py - 18, px + 34, py - 18, 6, (250, 204, 21))
        line(px + 24, py - 18, px + 24, py - 6, 6, (250, 204, 21))
    elif prop == "ticket":
        poly([(px - 40, py - 26), (px + 40, py - 26), (px + 40, py + 20), (px - 40, py + 20)], (250, 204, 21))
        line(px + 14, py - 26, px + 14, py + 20, 2)
    elif prop == "coffee":
        box(px - 22, py - 26, px + 18, py + 24, (255, 255, 255), 5, 3)
        oval(px + 12, py - 12, px + 34, py + 10, (255, 255, 255), 3)
        line(px - 10, py - 36, px - 6, py - 48, 3, (148, 163, 184))
    elif prop == "gift":
        box(px - 30, py - 20, px + 30, py + 34, (244, 114, 182), 4, 3)
        box(px - 6, py - 20, px + 6, py + 34, (253, 224, 71), 0, 0)
        box(px - 30, py + 2, px + 30, py + 14, (253, 224, 71), 0, 0)
    elif prop == "clock":
        oval(px - 28, py - 40, px + 28, py + 16, (255, 255, 255), 3)
        line(px, py - 12, px, py - 30, 3)
        line(px, py - 12, px + 14, py - 6, 3)
    elif prop == "umbrella":
        d.pieslice(((px - 44) * s, (py - 60) * s, (px + 44) * s, (py + 28) * s), 180, 360, fill=(59, 130, 246), outline=L, width=3 * s)
        line(px, py - 16, px, py + 36, 4)
    elif prop == "tray":
        poly([(px - 44, py), (px + 44, py), (px + 34, py + 20), (px - 34, py + 20)], (170, 178, 192))
        oval(px - 26, py - 18, px + 8, py + 2, (255, 255, 255), 2)
        box(px + 14, py - 26, px + 38, py, (200, 230, 250), 4, 2)

    # speech bubble with the phrase
    def font(px_):
        for name in ("arialbd.ttf", "arial.ttf", "DejaVuSans-Bold.ttf"):
            try:
                return ImageFont.truetype(name, int(px_))
            except OSError:
                continue
        return ImageFont.load_default()

    size = 28
    while True:
        f = font(size * s)
        lines = _wrap(term, max(10, int(26 * 28 / size)))
        widths = [d.textlength(ln, font=f) for ln in lines]
        total_h = len(lines) * size * 1.25
        if (max(widths) / s <= 410 and total_h <= 84) or size <= 15:
            break
        size -= 1
    bw, bh = max(widths) / s + 34, total_h + 34
    bx0 = max(20, min(W - 20 - bw, xa - 40))
    by0 = 16
    box(bx0, by0, bx0 + bw, by0 + bh, (255, 255, 255), 16, 3)
    tx = xa + 10
    poly([(tx - 14, by0 + bh - 2), (tx + 14, by0 + bh - 2), (tx, by0 + bh + 24)], (255, 255, 255))
    d.line([((tx - 12) * s, (by0 + bh - 1) * s), ((tx + 12) * s, (by0 + bh - 1) * s)], fill=(255, 255, 255), width=5 * s)
    for i, ln in enumerate(lines):
        d.text(((bx0 + 15) * s, (by0 + 17 + i * size * 1.25) * s), ln, fill=L, font=f)
    return finish(img)


def media_icon(name: str) -> bytes:
    """Flat original drawing for a media or news word (kind "media")."""
    img, d = canvas()
    s = S
    L = OUTLINE

    def oval(x0, y0, x1, y1, fill, w=3):
        d.ellipse((x0 * s, y0 * s, x1 * s, y1 * s), fill=fill, outline=L if w else None, width=w * s)

    def box(x0, y0, x1, y1, fill, rr=0, w=3):
        d.rounded_rectangle((x0 * s, y0 * s, x1 * s, y1 * s), rr * s, fill=fill, outline=L if w else None, width=w * s)

    def poly(pts, fill):
        pp = [(x * s, y * s) for x, y in pts]
        d.polygon(pp, fill=fill)
        d.line(pp + [pp[0]], fill=L, width=3 * s, joint="curve")

    def line(x0, y0, x1, y1, w=5, col=L):
        d.line((x0 * s, y0 * s, x1 * s, y1 * s), fill=col, width=w * s)

    def text(x, y, t, size, col=L):
        d.text((x * s, y * s), t, fill=col, font=_big_font(size * s))

    def mic(x, y):
        box(x - 12, y - 34, x + 12, y + 4, (51, 65, 85), 12, 3)
        line(x, y + 4, x, y + 40, 6)

    def tv(x0, y0, x1, y1, screen=(147, 197, 253)):
        box(x0, y0, x1, y1, (51, 65, 85), 14, 4)
        box(x0 + 12, y0 + 12, x1 - 12, y1 - 12, screen, 6, 0)

    d.rectangle((0, 300 * s, W * s, H * s), fill=(214, 196, 160))
    if name == "journalist":
        _person(d, 140, 50, 2.6, "f", (37, 99, 235))
        box(230, 160, 300, 240, (255, 255, 255), 6, 3)
        for k in range(4):
            line(240, 178 + k * 16, 290, 178 + k * 16, 3, (148, 163, 184))
        line(300, 190, 360, 160, 6, (250, 204, 21))
        mic(400, 190)
    elif name == "news anchor":
        d.rectangle((0, 0, W * s, 300 * s), fill=(30, 58, 138))
        _person(d, 240, 70, 2.3, "m", (51, 65, 85))
        box(110, 230, 370, 300, (226, 232, 240), 10, 4)
        box(130, 40, 230, 100, (59, 130, 246), 6, 3)
        text(146, 56, "NEWS", 34, (255, 255, 255))
    elif name == "channel":
        tv(110, 60, 370, 250)
        text(160, 100, "CH 5", 80, (255, 255, 255))
        box(180, 260, 300, 290, (148, 163, 184), 6, 3)
    elif name == "studio":
        d.rectangle((0, 0, W * s, 300 * s), fill=(30, 41, 59))
        for k, x in enumerate((90, 240, 390)):
            poly([(x - 30, 20), (x + 30, 20), (x + 60, 160), (x - 60, 160)], (254, 249, 195))
            oval(x - 24, 8, x + 24, 36, (250, 204, 21), 3)
        _person(d, 240, 150, 1.5, "f", (219, 39, 119))
        box(160, 250, 320, 300, (226, 232, 240), 8, 3)
    elif name == "subtitles":
        tv(60, 40, 420, 270, (30, 41, 59))
        box(120, 190, 360, 240, (0, 0, 0), 6, 0)
        text(138, 198, "Hello, how are you?", 28, (255, 255, 255))
        poly([(180, 90), (230, 70), (270, 110), (220, 150)], (74, 222, 128))
    elif name == "broadcast":
        tv(60, 60, 280, 230)
        for r_ in (30, 54, 78):
            d.arc(((330 - r_) * s, (110 - r_) * s, (330 + r_) * s, (110 + r_) * s), 300, 60, fill=(59, 130, 246), width=5 * s)
        oval(318, 98, 342, 122, (59, 130, 246), 3)
        line(330, 122, 330, 250, 8)
    elif name == "presenter":
        d.rectangle((0, 0, W * s, 300 * s), fill=(124, 58, 237))
        _person(d, 160, 60, 2.4, "f", (255, 255, 255))
        box(280, 60, 440, 170, (255, 255, 255), 8, 4)
        poly([(300, 150), (340, 100), (380, 130), (420, 80), (420, 150)], (74, 222, 128))
        mic(250, 200)
    elif name == "advertisement":
        box(90, 40, 390, 230, (254, 240, 138), 10, 4)
        text(120, 60, "NEW!", 70, (220, 38, 38))
        text(120, 150, "Buy now", 40)
        line(150, 230, 150, 320, 10)
        line(330, 230, 330, 320, 10)
    elif name == "billboard":
        d.rectangle((0, 0, W * s, 300 * s), fill=(186, 230, 253))
        box(60, 40, 420, 210, (255, 255, 255), 6, 5)
        box(76, 56, 404, 194, (250, 204, 21), 4, 0)
        text(110, 80, "TRY IT!", 66)
        line(160, 210, 160, 320, 12)
        line(320, 210, 320, 320, 12)
    elif name == "leaflet":
        for k, (x, y, col) in enumerate(((60, 90, (254, 240, 138)), (180, 60, (187, 247, 208)), (300, 100, (186, 230, 253)))):
            box(x, y, x + 120, y + 180, col, 6, 3)
            line(x + 14, y + 30, x + 100, y + 30, 6)
            for j in range(4):
                line(x + 14, y + 60 + j * 20, x + 100 - (j % 2) * 20, y + 60 + j * 20, 3, (100, 116, 139))
    elif name == "article":
        box(90, 30, 390, 330, (255, 255, 255), 8, 4)
        text(110, 40, "Local News", 36)
        box(110, 90, 230, 190, (147, 197, 253), 4, 2)
        for k in range(5):
            line(246, 98 + k * 18, 372, 98 + k * 18, 3, (148, 163, 184))
        for k in range(5):
            line(110, 210 + k * 18, 372, 210 + k * 18, 3, (148, 163, 184))
    elif name == "photograph":
        box(90, 50, 390, 290, (255, 255, 255), 6, 4)
        box(110, 70, 370, 250, (147, 197, 253), 2, 0)
        oval(280, 90, 340, 150, (250, 204, 21), 0)
        poly([(110, 250), (200, 150), (290, 250)], (74, 222, 128))
        poly([(220, 250), (300, 170), (370, 250)], (22, 163, 74))
    elif name == "documentary":
        box(60, 50, 420, 250, (30, 41, 59), 10, 4)
        for k in range(6):
            box(70, 66 + k * 30, 86, 84 + k * 30, (255, 255, 255), 3, 0)
            box(394, 66 + k * 30, 410, 84 + k * 30, (255, 255, 255), 3, 0)
        box(110, 70, 370, 230, (147, 197, 253), 4, 0)
        poly([(160, 210), (220, 120), (270, 180), (310, 140), (350, 210)], (74, 222, 128))
        oval(300, 84, 340, 124, (250, 204, 21), 0)
        text(150, 262, "DOCUMENTARY", 28)
    else:
        raise SystemExit(f"unknown media icon: {name}")
    return finish(img)


def sport_icon(name: str) -> bytes:
    """Flat original drawing for a sports or event word (kind "sport")."""
    img, d = canvas()
    s = S
    L = OUTLINE
    GOLD = (250, 204, 21)

    def oval(x0, y0, x1, y1, fill, w=3):
        d.ellipse((x0 * s, y0 * s, x1 * s, y1 * s), fill=fill, outline=L if w else None, width=w * s)

    def box(x0, y0, x1, y1, fill, rr=0, w=3):
        d.rounded_rectangle((x0 * s, y0 * s, x1 * s, y1 * s), rr * s, fill=fill, outline=L if w else None, width=w * s)

    def poly(pts, fill):
        pp = [(x * s, y * s) for x, y in pts]
        d.polygon(pp, fill=fill)
        d.line(pp + [pp[0]], fill=L, width=3 * s, joint="curve")

    def line(x0, y0, x1, y1, w=5, col=L):
        d.line((x0 * s, y0 * s, x1 * s, y1 * s), fill=col, width=w * s)

    def text(x, y, t, size, col=L):
        d.text((x * s, y * s), t, fill=col, font=_big_font(size * s))

    def ball(cx, cy, r):
        oval(cx - r, cy - r, cx + r, cy + r, (255, 255, 255), 3)
        poly([(cx, cy - r * 0.4), (cx + r * 0.38, cy - r * 0.12), (cx + r * 0.24, cy + r * 0.32), (cx - r * 0.24, cy + r * 0.32), (cx - r * 0.38, cy - r * 0.12)], L)

    def trophy(cx, cy, k=1.0):
        poly([(cx - 40 * k, cy - 60 * k), (cx + 40 * k, cy - 60 * k), (cx + 30 * k, cy + 10 * k), (cx + 10 * k, cy + 30 * k), (cx - 10 * k, cy + 30 * k), (cx - 30 * k, cy + 10 * k)], GOLD)
        d.arc(((cx - 64 * k) * s, (cy - 56 * k) * s, (cx - 24 * k) * s, (cy - 6 * k) * s), 90, 270, fill=L, width=int(5 * s * k))
        d.arc(((cx + 24 * k) * s, (cy - 56 * k) * s, (cx + 64 * k) * s, (cy - 6 * k) * s), 270, 90, fill=L, width=int(5 * s * k))
        box(cx - 8 * k, cy + 30 * k, cx + 8 * k, cy + 56 * k, GOLD, 2, 3)
        box(cx - 36 * k, cy + 56 * k, cx + 36 * k, cy + 78 * k, (120, 72, 36), 4, 3)

    grass = (134, 239, 172)
    if name in ("trophy", "champion"):
        d.rectangle((0, 0, W * s, 300 * s), fill=(254, 249, 195))
        trophy(240 if name == "trophy" else 330, 170, 1.7 if name == "trophy" else 1.3)
        if name == "champion":
            _person(d, 150, 60, 2.4, "m", (37, 99, 235))
            line(200, 190, 270, 150, 7, (250, 213, 172))
            for k in range(6):
                d.polygon(poly_star((40 + k * 80) * s, 30 * s, 12 * s, 5 * s, 5), fill=GOLD, outline=L)
    elif name == "medal":
        line(170, 20, 230, 150, 20, (220, 38, 38))
        line(310, 20, 250, 150, 20, (37, 99, 235))
        oval(180, 140, 300, 260, GOLD, 5)
        oval(198, 158, 282, 242, (253, 224, 71), 3)
        text(222, 172, "1", 60)
    elif name == "podium":
        box(170, 120, 310, 320, (59, 130, 246), 4, 4)
        box(40, 190, 170, 320, (148, 163, 184), 4, 4)
        box(310, 220, 440, 320, (180, 83, 9), 4, 4)
        text(226, 150, "1", 80, (255, 255, 255))
        text(90, 210, "2", 60, (255, 255, 255))
        text(360, 236, "3", 50, (255, 255, 255))
        oval(205, 70, 275, 120, GOLD, 3)
        d.polygon(poly_star(240 * s, 50 * s, 22 * s, 9 * s, 5), fill=GOLD, outline=L)
    elif name == "scoreboard":
        box(60, 50, 420, 250, (15, 23, 42), 12, 5)
        text(100, 80, "HOME", 30, (255, 255, 255))
        text(290, 80, "AWAY", 30, (255, 255, 255))
        text(112, 130, "2", 100, (250, 204, 21))
        text(312, 130, "1", 100, (250, 204, 21))
        line(120, 250, 120, 320, 12)
        line(360, 250, 360, 320, 12)
    elif name == "goalkeeper":
        d.rectangle((0, 0, W * s, 300 * s), fill=grass)
        for x in range(40, 450, 24):
            line(x, 40, x, 260, 2, (255, 255, 255))
        for y in range(40, 270, 24):
            line(40, y, 440, y, 2, (255, 255, 255))
        box(30, 30, 450, 270, None, 0, 6)
        _person(d, 210, 90, 2.1, "m", (234, 88, 12))
        ball(340, 150, 26)
        line(260, 190, 310, 160, 9, (234, 88, 12))
    elif name == "penalty":
        d.rectangle((0, 0, W * s, 300 * s), fill=grass)
        box(100, 40, 380, 160, None, 0, 7)
        for x in range(112, 380, 22):
            line(x, 48, x, 156, 1, (255, 255, 255))
        oval(224, 268, 256, 300, (255, 255, 255), 3)
        ball(240, 270, 18)
        oval(232, 200, 248, 216, L, 0)
        line(240, 216, 240, 250, 3)
        text(180, 304, "PENALTY", 26)
    elif name == "coach":
        d.rectangle((0, 0, W * s, 300 * s), fill=grass)
        _person(d, 150, 50, 2.6, "m", (30, 58, 138))
        line(220, 180, 300, 150, 7, (250, 213, 172))
        box(300, 100, 440, 220, (255, 255, 255), 6, 4)
        line(320, 130, 340, 160, 3)
        line(340, 130, 320, 160, 3)
        oval(380, 130, 402, 152, L, 3)
        line(350, 190, 420, 130, 3, (220, 38, 38))
    elif name == "tournament":
        for k in range(4):
            box(40, 50 + k * 70, 120, 90 + k * 70, (226, 232, 240), 6, 3)
        for k in range(2):
            box(190, 85 + k * 140, 270, 125 + k * 140, (226, 232, 240), 6, 3)
            line(120, 70 + k * 140, 150, 70 + k * 140, 4)
            line(120, 140 + k * 140, 150, 140 + k * 140, 4)
            line(150, 70 + k * 140, 150, 140 + k * 140, 4)
            line(150, 105 + k * 140, 190, 105 + k * 140, 4)
        line(270, 105, 300, 105, 4)
        line(270, 245, 300, 245, 4)
        line(300, 105, 300, 245, 4)
        line(300, 175, 330, 175, 4)
        trophy(390, 160, 0.9)
    elif name == "jersey":
        poly([(150, 60), (200, 40), (240, 70), (280, 40), (330, 60), (420, 140), (380, 180), (340, 130), (340, 310), (140, 310), (140, 130), (100, 180), (60, 140)], (220, 38, 38))
        poly([(200, 40), (240, 70), (280, 40), (270, 30), (240, 56), (210, 30)], (255, 255, 255))
        text(196, 120, "10", 110, (255, 255, 255))
        line(140, 290, 340, 290, 5, (255, 255, 255))
    elif name == "finish line":
        d.rectangle((0, 0, W * s, 300 * s), fill=(214, 236, 252))
        d.rectangle((0, 220 * s, W * s, H * s), fill=(190, 80, 60))
        for r_ in range(2):
            for c_ in range(16):
                if (r_ + c_) % 2 == 0:
                    d.rectangle(((30 + c_ * 28) * s, (60 + r_ * 28) * s, (58 + c_ * 28) * s, (88 + r_ * 28) * s), fill=L)
        box(30, 60, 478, 116, None, 0, 3)
        line(30, 60, 30, 260, 8)
        line(450, 60, 450, 260, 8)
        for k in range(2):
            _person(d, 140 + k * 120, 150, 1.0, "m", [(37, 99, 235), (234, 88, 12)][k])
    elif name == "athlete":
        d.rectangle((0, 0, W * s, 300 * s), fill=(190, 80, 60))
        for y in (100, 200, 300):
            line(0, y, 480, y, 4, (255, 255, 255))
        _person(d, 240, 30, 2.8, "m", (37, 99, 235))
        text(214, 160, "7", 50, (255, 255, 255))
    elif name == "relay race":
        d.rectangle((0, 0, W * s, 300 * s), fill=(190, 80, 60))
        line(0, 120, 480, 120, 4, (255, 255, 255))
        _person(d, 130, 60, 2.2, "m", (37, 99, 235))
        _person(d, 330, 60, 2.2, "f", (219, 39, 119))
        line(200, 190, 290, 170, 8, (250, 213, 172))
        box(230, 156, 262, 168, GOLD, 3, 3)
    elif name == "birthday party":
        d.rectangle((0, 0, W * s, 300 * s), fill=(254, 226, 226))
        for k, col in enumerate(((220, 38, 38), (250, 204, 21), (37, 99, 235), (22, 163, 74), (219, 39, 119))):
            x = 60 + k * 90
            line(x, 120 + (k % 2) * 20, 240, 250, 2)
            oval(x - 28, 40 + (k % 2) * 20, x + 28, 120 + (k % 2) * 20, col, 3)
        box(160, 220, 320, 300, (254, 215, 170), 6, 4)
        box(160, 220, 320, 244, (244, 114, 182), 6, 3)
        for k in range(5):
            line(180 + k * 30, 190, 180 + k * 30, 220, 5, (59, 130, 246))
            oval(174 + k * 30, 170, 186 + k * 30, 190, (250, 204, 21), 2)
    elif name == "parade":
        d.rectangle((0, 0, W * s, 300 * s), fill=(186, 230, 253))
        for k, col in enumerate(((220, 38, 38), (250, 204, 21), (37, 99, 235), (22, 163, 74))):
            line(40 + k * 130, 110, 40 + k * 130, 50, 5)
            poly([(40 + k * 130, 50), (100 + k * 130, 66), (40 + k * 130, 84)], col)
        for k in range(4):
            _person(d, 70 + k * 100, 120, 1.2, "m" if k % 2 == 0 else "f", [(220, 38, 38), (250, 204, 21), (37, 99, 235), (22, 163, 74)][k])
        box(30, 300, 450, 316, (148, 163, 184), 4, 0)
    else:
        raise SystemExit(f"unknown sport icon: {name}")
    return finish(img)


def art_icon(name: str) -> bytes:
    """Flat original drawing for an art or culture word (kind "art")."""
    img, d = canvas()
    s = S
    L = OUTLINE
    GOLD = (250, 204, 21)

    def oval(x0, y0, x1, y1, fill, w=3):
        d.ellipse((x0 * s, y0 * s, x1 * s, y1 * s), fill=fill, outline=L if w else None, width=w * s)

    def box(x0, y0, x1, y1, fill, rr=0, w=3):
        d.rounded_rectangle((x0 * s, y0 * s, x1 * s, y1 * s), rr * s, fill=fill, outline=L if w else None, width=w * s)

    def poly(pts, fill):
        pp = [(x * s, y * s) for x, y in pts]
        d.polygon(pp, fill=fill)
        d.line(pp + [pp[0]], fill=L, width=3 * s, joint="curve")

    def line(x0, y0, x1, y1, w=5, col=L):
        d.line((x0 * s, y0 * s, x1 * s, y1 * s), fill=col, width=w * s)

    d.rectangle((0, 300 * s, W * s, H * s), fill=(214, 196, 160))
    if name == "art studio":
        d.rectangle((0, 0, W * s, 300 * s), fill=(254, 249, 195))
        box(300, 40, 440, 160, (186, 230, 253), 4, 4)
        line(150, 100, 90, 310, 8, (120, 72, 36))
        line(190, 100, 250, 310, 8, (120, 72, 36))
        box(100, 40, 240, 170, (255, 255, 255), 4, 4)
        poly([(110, 160), (160, 90), (200, 130), (230, 100), (230, 160)], (74, 222, 128))
        _person(d, 330, 150, 1.5, "f", (124, 58, 237))
        for k, c in enumerate(((220, 38, 38), (250, 204, 21), (37, 99, 235))):
            oval(40 + k * 36, 262, 66 + k * 36, 288, c, 3)
    elif name == "carving":
        poly([(150, 100), (330, 100), (350, 280), (130, 280)], (180, 120, 70))
        for k in range(5):
            line(150 + k * 10, 120 + k * 36, 330 - k * 6, 130 + k * 36, 3, (120, 72, 36))
        poly([(200, 140), (240, 110), (280, 140), (270, 220), (210, 220)], (214, 160, 100))
        oval(218, 150, 232, 164, L, 0)
        oval(248, 150, 262, 164, L, 0)
        poly([(380, 80), (440, 40), (450, 60), (396, 110)], (170, 178, 192))
        box(370, 100, 410, 140, (120, 72, 36), 6, 3)
    elif name == "palette":
        poly([(70, 160), (110, 70), (230, 40), (370, 70), (430, 160), (390, 260), (290, 300), (220, 250), (150, 270), (80, 230)], (254, 215, 170))
        oval(210, 190, 270, 250, (214, 196, 160), 3)
        for (x, y, c) in ((130, 110, (220, 38, 38)), (200, 80, (250, 204, 21)), (280, 76, (37, 99, 235)), (350, 110, (22, 163, 74)), (390, 170, (219, 39, 119))):
            oval(x - 22, y - 22, x + 22, y + 22, c, 3)
        line(380, 250, 440, 190, 10, (120, 72, 36))
    elif name == "audience":
        d.rectangle((0, 0, W * s, 300 * s), fill=(76, 29, 149))
        box(60, 20, 420, 110, (30, 41, 59), 4, 3)
        for row in range(2):
            for k in range(6):
                _person(d, 50 + k * 76 + row * 38, 130 + row * 66, 1.0, "m" if (k + row) % 2 else "f", [(220, 38, 38), (250, 204, 21), (37, 99, 235), (22, 163, 74), (219, 39, 119), (234, 88, 12)][k % 6])
    elif name == "backstage":
        d.rectangle((0, 0, W * s, 300 * s), fill=(51, 65, 85))
        box(60, 40, 220, 200, (255, 255, 255), 6, 4)
        for k in range(6):
            oval(66 + (k % 3) * 52 + 6, 46 + (k // 3) * 150, 90 + (k % 3) * 52, 66 + (k // 3) * 150, (254, 240, 138), 2)
        box(250, 80, 440, 130, (148, 163, 184), 4, 3)
        for k in range(4):
            poly([(270 + k * 44, 130), (300 + k * 44, 130), (310 + k * 44, 270), (260 + k * 44, 270)], [(219, 39, 119), (37, 99, 235), (250, 204, 21), (22, 163, 74)][k])
        _person(d, 130, 140, 1.4, "f", (124, 58, 237))
    elif name == "costume":
        poly([(180, 90), (300, 90), (390, 300), (90, 300)], (124, 58, 237))
        poly([(180, 90), (240, 130), (300, 90), (270, 60), (210, 60)], (250, 204, 21))
        oval(190, 10, 290, 70, (250, 213, 172))
        for k in range(5):
            d.polygon(poly_star((140 + k * 50) * s, (200 + (k % 2) * 40) * s, 14 * s, 6 * s, 5), fill=GOLD, outline=L)
    elif name == "face paint":
        oval(130, 40, 350, 300, (250, 213, 172), 5)
        for sx in (-1, 1):
            oval(240 + sx * 56 - 14, 130, 240 + sx * 56 + 14, 160, L, 0)
            poly([(240 + sx * 90, 190), (240 + sx * 60, 210), (240 + sx * 100, 230)], (220, 38, 38))
        d.arc((190 * s, 190 * s, 290 * s, 250 * s), 20, 160, fill=L, width=6 * s)
        for k, c in enumerate(((37, 99, 235), (250, 204, 21), (22, 163, 74))):
            line(190 + k * 50, 90, 210 + k * 50, 70, 8, c)
        d.polygon(poly_star(240 * s, 190 * s, 16 * s, 7 * s, 5), fill=(219, 39, 119), outline=L)
    elif name == "marionette":
        box(160, 20, 320, 40, (120, 72, 36), 4, 3)
        for x in (190, 240, 290):
            line(x, 40, x, 120 if x == 240 else 150, 2)
        oval(214, 110, 266, 160, (250, 213, 172))
        poly([(210, 165), (270, 165), (284, 250), (196, 250)], (220, 38, 38))
        for sx in (-1, 1):
            line(240 + sx * 36, 175, 240 + sx * 70, 220, 7, (250, 213, 172))
            line(240 + sx * 14, 250, 240 + sx * 24, 320, 8, (37, 99, 235))
        line(190, 40, 170, 190, 2)
        line(290, 40, 310, 190, 2)
    elif name == "traditional dress":
        poly([(210, 40), (270, 40), (290, 120), (310, 320), (170, 320), (190, 120)], (219, 39, 119))
        poly([(210, 40), (240, 80), (270, 40)], (255, 255, 255))
        poly([(190, 130), (290, 130), (300, 150), (180, 150)], (250, 204, 21))
        poly([(110, 320), (170, 140), (190, 140), (200, 320)], (255, 255, 255))
        poly([(280, 320), (290, 140), (310, 140), (370, 320)], (255, 255, 255))
        oval(215, 0, 265, 40, (250, 213, 172))
    else:
        raise SystemExit(f"unknown art icon: {name}")
    return finish(img)


def city_icon(name: str) -> bytes:
    """Flat original drawing for a city or housing word (kind "city")."""
    img, d = canvas()
    s = S
    L = OUTLINE

    def oval(x0, y0, x1, y1, fill, w=3):
        d.ellipse((x0 * s, y0 * s, x1 * s, y1 * s), fill=fill, outline=L if w else None, width=w * s)

    def box(x0, y0, x1, y1, fill, rr=0, w=3):
        d.rounded_rectangle((x0 * s, y0 * s, x1 * s, y1 * s), rr * s, fill=fill, outline=L if w else None, width=w * s)

    def poly(pts, fill):
        pp = [(x * s, y * s) for x, y in pts]
        d.polygon(pp, fill=fill)
        d.line(pp + [pp[0]], fill=L, width=3 * s, joint="curve")

    def line(x0, y0, x1, y1, w=5, col=L):
        d.line((x0 * s, y0 * s, x1 * s, y1 * s), fill=col, width=w * s)

    def text(x, y, t, size, col=L):
        d.text((x * s, y * s), t, fill=col, font=_big_font(size * s))

    def house(x, y, w, h, col):
        box(x, y, x + w, y + h, col, 2, 3)
        poly([(x - 8, y), (x + w / 2, y - h * 0.55), (x + w + 8, y)], (220, 38, 38))
        box(x + w / 2 - 12, y + h - 40, x + w / 2 + 12, y + h, (120, 72, 36), 2, 2)
        box(x + 10, y + 16, x + 34, y + 40, (147, 197, 253), 2, 2)

    d.rectangle((0, 0, W * s, 280 * s), fill=(186, 230, 253))
    d.rectangle((0, 280 * s, W * s, H * s), fill=(134, 239, 172))
    if name == "fountain":
        box(110, 250, 370, 290, (203, 213, 225), 14, 4)
        box(210, 180, 270, 252, (203, 213, 225), 6, 4)
        box(150, 170, 330, 190, (203, 213, 225), 10, 4)
        for sx in (-1, 1):
            d.arc(((240 - 90 + sx * 0) * s, 40 * s, 330 * s, 200 * s), 200 if sx < 0 else 270, 270 if sx < 0 else 340, fill=(59, 130, 246), width=6 * s)
        line(240, 70, 240, 180, 7, (59, 130, 246))
        for k in range(5):
            oval(130 + k * 50, 262, 150 + k * 50, 278, (147, 197, 253), 0)
    elif name == "suburb":
        for k, c in enumerate(((254, 215, 170), (187, 247, 208), (221, 214, 254), (254, 240, 138))):
            house(30 + k * 110, 170 + (k % 2) * 20, 90, 80, c)
        line(0, 290, 480, 290, 4, (148, 163, 184))
        oval(400, 20, 450, 70, (250, 204, 21), 3)
    elif name == "rent":
        house(60, 130, 170, 130, (254, 215, 170))
        box(290, 90, 440, 240, (255, 255, 255), 8, 4)
        text(312, 104, "FOR RENT", 28, (220, 38, 38))
        text(312, 150, "$500", 60)
        line(365, 240, 365, 300, 8)
    elif name == "landlord":
        house(50, 130, 170, 130, (254, 215, 170))
        _person(d, 330, 110, 1.6, "m", (30, 58, 138))
        for k in range(3):
            oval(400 + k * 0, 150 + k * 16, 424, 170 + k * 16, (250, 204, 21), 3)
        line(350, 215, 420, 205, 7, (250, 213, 172))
        oval(404, 190, 440, 224, (250, 204, 21), 3)
        line(414, 204, 430, 204, 3)
    elif name == "neighbourhood":
        line(0, 262, 480, 262, 10, (148, 163, 184))
        for k, c in enumerate(((254, 215, 170), (187, 247, 208), (221, 214, 254), (254, 240, 138))):
            house(24 + k * 116, 150, 96, 90, c)
        for x in (60, 300):
            line(x, 260, x, 230, 8, (120, 72, 36))
            oval(x - 26, 180, x + 26, 240, (74, 222, 128), 3)
        _person(d, 250, 200, 0.9, "f", (219, 39, 119))
    else:
        raise SystemExit(f"unknown city icon: {name}")
    return finish(img)


def sci_icon(name: str) -> bytes:
    """Flat original drawing for a science word (kind "sci")."""
    img, d = canvas()
    s = S
    L = OUTLINE

    def oval(x0, y0, x1, y1, fill, w=3):
        d.ellipse((x0 * s, y0 * s, x1 * s, y1 * s), fill=fill, outline=L if w else None, width=w * s)

    def box(x0, y0, x1, y1, fill, rr=0, w=3):
        d.rounded_rectangle((x0 * s, y0 * s, x1 * s, y1 * s), rr * s, fill=fill, outline=L if w else None, width=w * s)

    def poly(pts, fill):
        pp = [(x * s, y * s) for x, y in pts]
        d.polygon(pp, fill=fill)
        d.line(pp + [pp[0]], fill=L, width=3 * s, joint="curve")

    def line(x0, y0, x1, y1, w=5, col=L):
        d.line((x0 * s, y0 * s, x1 * s, y1 * s), fill=col, width=w * s)

    if name in ("orbit", "meteor", "telescope"):
        d.rectangle((0, 0, W * s, H * s), fill=(15, 23, 42))
        for k in range(40):
            x, y = (k * 97) % 480, (k * 53) % 360
            oval(x, y, x + 3, y + 3, (226, 232, 240), 0)
    else:
        d.rectangle((0, 300 * s, W * s, H * s), fill=(214, 196, 160))
    if name == "orbit":
        oval(210, 140, 270, 200, (250, 204, 21), 3)
        d.ellipse((60 * s, 90 * s, 420 * s, 250 * s), outline=(148, 163, 184), width=4 * s)
        oval(380, 150, 420, 190, (59, 130, 246), 3)
        oval(392, 160, 402, 170, (74, 222, 128), 0)
    elif name == "meteor":
        poly([(110, 40), (300, 190), (280, 200)], (253, 186, 116))
        line(120, 50, 300, 192, 10, (253, 224, 71))
        oval(280, 170, 330, 220, (180, 83, 9), 3)
        d.rectangle((0, 290 * s, W * s, H * s), fill=(30, 41, 59))
        poly([(0, 290), (80, 250), (160, 290)], (30, 41, 59))
        poly([(280, 290), (360, 240), (470, 290)], (30, 41, 59))
    elif name == "telescope":
        line(240, 220, 170, 330, 9, (148, 163, 184))
        line(240, 220, 310, 330, 9, (148, 163, 184))
        line(240, 220, 240, 330, 9, (148, 163, 184))
        poly([(130, 90), (180, 70), (330, 160), (310, 200), (260, 190)], (226, 232, 240))
        box(320, 150, 360, 210, (59, 130, 246), 6, 3)
        oval(110, 70, 160, 120, (147, 197, 253), 3)
        for (x, y) in ((60, 60), (400, 60), (420, 200)):
            d.polygon(poly_star(x * s, y * s, 10 * s, 4 * s, 5), fill=(250, 204, 21))
    elif name == "magnet":
        d.arc((120 * s, 70 * s, 360 * s, 310 * s), 180, 360, fill=(220, 38, 38), width=60 * s)
        box(120, 190, 180, 290, (220, 38, 38), 0, 4)
        box(300, 190, 360, 290, (37, 99, 235), 0, 4)
        box(120, 270, 180, 300, (226, 232, 240), 0, 3)
        box(300, 270, 360, 300, (226, 232, 240), 0, 3)
        for k in range(6):
            line(130 + k * 22, 330, 136 + k * 22, 318, 3)
    elif name == "test tube":
        box(60, 200, 420, 240, (180, 120, 70), 6, 4)
        for k, c in enumerate(((220, 38, 38), (37, 99, 235), (74, 222, 128), (250, 204, 21))):
            x = 100 + k * 80
            box(x, 80, x + 44, 240, (226, 240, 255), 18, 4)
            box(x + 4, 150 - k * 8, x + 40, 236, c, 14, 0)
        line(60, 240, 60, 320, 8, (120, 72, 36))
        line(420, 240, 420, 320, 8, (120, 72, 36))
    elif name == "experiment":
        poly([(200, 70), (260, 70), (260, 150), (320, 270), (140, 270), (200, 150)], (200, 230, 250))
        poly([(170, 220), (290, 220), (320, 270), (140, 270)], (134, 239, 172))
        for (x, y, r) in ((230, 190, 10), (250, 170, 7), (215, 160, 6), (240, 120, 8)):
            oval(x - r, y - r, x + r, y + r, (255, 255, 255), 2)
        box(60, 270, 420, 300, (180, 120, 70), 6, 4)
        box(340, 180, 390, 270, (255, 255, 255), 6, 3)
        oval(350, 150, 380, 180, (250, 204, 21), 3)
    elif name == "lab coat":
        poly([(160, 50), (230, 30), (250, 60), (270, 30), (340, 50), (420, 130), (380, 170), (340, 120), (340, 320), (140, 320), (140, 120), (100, 170), (60, 130)], (255, 255, 255))
        line(250, 60, 250, 320, 4)
        box(160, 210, 210, 260, (226, 232, 240), 4, 3)
        box(290, 210, 340, 260, (226, 232, 240), 4, 3)
        oval(176, 150, 192, 166, (59, 130, 246), 2)
        line(184, 160, 184, 200, 4, (59, 130, 246))
    else:
        raise SystemExit(f"unknown sci icon: {name}")
    return finish(img)


def main() -> None:
    path = Path(sys.argv[1])
    manifest = read_json(path)
    code = manifest["set"]["code"].lower()
    final = BUILD / "vocab-images" / "final" / code
    final.mkdir(parents=True, exist_ok=True)
    done = 0
    for i, card in enumerate(manifest["cards"]):
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
        elif kind == "emotion":
            data = emotion_face(val)
        elif kind == "health":
            data = health_icon(val)
        elif kind == "tech":
            data = tech_icon(val)
        elif kind == "eco":
            data = eco_icon(val)
        elif kind == "travel":
            data = travel_icon(val)
        elif kind == "shop":
            data = shop_icon(val)
        elif kind == "cook":
            data = cook_icon(val)
        elif kind == "med":
            data = med_icon(val)
        elif kind == "edu":
            data = edu_icon(val)
        elif kind == "media":
            data = media_icon(val)
        elif kind == "sport":
            data = sport_icon(val)
        elif kind == "art":
            data = art_icon(val)
        elif kind == "city":
            data = city_icon(val)
        elif kind == "sci":
            data = sci_icon(val)
        elif kind == "scene":
            data = scene_image(card["term"], card.get("imageQuery", ""), i, manifest["set"]["code"])
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
