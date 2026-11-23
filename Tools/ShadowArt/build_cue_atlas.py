"""The found moment's paper cut-outs (MemorySolveCue.Art), one 5x5 atlas for Assets/ShadowWorkshop/PicoFinal/Art/Cues.

Cut-outs are white paper with an ink outline and a soft shadow on the page below them, so a layer's tint colours the
paper and leaves the ink dark. Light things (flame, sparkle, steam, puff, dash) are soft white glows with no outline.
Cells run row by row from the top left in the order of MemorySolveCue.Art. Drawn at 4x and reduced, for clean edges.

    python -I Tools/ShadowArt/build_cue_atlas.py [out.png] [--sheet preview.png]
"""
import math, sys
from PIL import Image, ImageDraw, ImageFilter, ImageChops

CELL, SS, COLS = 256, 4, 5
C = CELL * SS                      # a cell at drawing size
INK = (43, 30, 34)                 # MemoryPalette.Sepia
OUTLINE = 7 * SS                   # ink width
SHADOW = (5 * SS, -6 * SS)         # drop shadow offset (right, down in image space is +y; see shadow())
ART = ["CloudA", "CloudB", "CloudSmall", "Bird", "Confetti", "Streamer", "Balloon", "Flame",
       "Puff", "Sparkle", "Heart", "Steam", "Sun", "Dash", "Shard", "Bang", "Star"]


def mask():
    return Image.new("L", (C, C), 0)


def circles(d, spec):
    for x, y, r in spec:
        d.ellipse([(x - r) * C, (y - r) * C, (x + r) * C, (y + r) * C], fill=255)


def dilate(m, px):
    # Round dilation by repeated 3x3 max filters on a reduced copy is slow; a blur + threshold gives a round ring.
    b = m.filter(ImageFilter.GaussianBlur(px * .5))
    return b.point(lambda v: 255 if v > 18 else int(v * 255 / 18))


def cutout(shape, shade=True, extra_ink=None):
    """White paper (shape) inside an ink ring, over a soft shadow. extra_ink: an L mask drawn in ink on top."""
    outer = dilate(shape, OUTLINE)
    paper = Image.new("RGBA", (C, C), (0, 0, 0, 0))
    # Shadow: the outer shape moved down-right, blurred, ink at 30%.
    sh = ImageChops.offset(outer, SHADOW[0], -SHADOW[1]).filter(ImageFilter.GaussianBlur(6 * SS))
    shadow = Image.new("RGBA", (C, C), INK + (0,))
    shadow.putalpha(sh.point(lambda v: int(v * .30)))
    paper = Image.alpha_composite(paper, shadow)
    ink = Image.new("RGBA", (C, C), INK + (0,)); ink.putalpha(outer)
    paper = Image.alpha_composite(paper, ink)
    # Paper: white, a touch darker toward the bottom (light from above).
    grad = Image.linear_gradient("L").resize((C, C)).point(lambda v: 255 - int(v * .10)) if shade else Image.new("L", (C, C), 255)
    fill = Image.merge("RGBA", (grad, grad, grad, shape))
    paper = Image.alpha_composite(paper, fill)
    if extra_ink is not None:
        e = Image.new("RGBA", (C, C), INK + (0,)); e.putalpha(extra_ink)
        paper = Image.alpha_composite(paper, e)
    return paper


def glow(shape, core_blur, halo_blur, halo=.45):
    """A soft white light: a sharp-ish core and a wide halo."""
    core = shape.filter(ImageFilter.GaussianBlur(core_blur))
    wide = shape.filter(ImageFilter.GaussianBlur(halo_blur)).point(lambda v: int(v * halo))
    a = ImageChops.lighter(core, wide)
    return Image.merge("RGBA", (Image.new("L", (C, C), 255),) * 3 + (a,))


def ink_only(shape):
    return Image.merge("RGBA", tuple(Image.new("L", (C, C), v) for v in INK) + (shape,))


def cloud_a():
    m = mask(); d = ImageDraw.Draw(m)
    circles(d, [(.30, .58, .17), (.47, .45, .22), (.66, .52, .17), (.78, .62, .11), (.20, .66, .10)])
    d.rounded_rectangle([.12 * C, .58 * C, .88 * C, .74 * C], radius=.08 * C, fill=255)
    return cutout(m)


def cloud_b():
    m = mask(); d = ImageDraw.Draw(m)
    circles(d, [(.22, .58, .11), (.36, .50, .15), (.54, .46, .17), (.72, .53, .13), (.84, .60, .08)])
    d.rounded_rectangle([.12 * C, .56 * C, .90 * C, .66 * C], radius=.05 * C, fill=255)
    return cutout(m)


def cloud_small():
    m = mask(); d = ImageDraw.Draw(m)
    circles(d, [(.35, .56, .13), (.52, .47, .17), (.68, .57, .12)])
    d.rounded_rectangle([.24 * C, .56 * C, .78 * C, .68 * C], radius=.06 * C, fill=255)
    return cutout(m)


def bird():
    # A gull in flight: two arcs meeting at the body, drawn as a thick ink stroke.
    m = mask(); d = ImageDraw.Draw(m)
    w = int(.045 * C)
    for sign in (-1, 1):
        pts = []
        for i in range(25):
            t = i / 24
            x = .5 + sign * t * .38
            y = .52 - math.sin(t * math.pi) * .14 + t * .06
            pts.append((x * C, y * C))
        d.line(pts, fill=255, width=w, joint="curve")
    d.ellipse([.47 * C, .49 * C, .53 * C, .56 * C], fill=255)
    return ink_only(m.filter(ImageFilter.GaussianBlur(SS * .6)))


def confetti():
    m = mask(); d = ImageDraw.Draw(m)
    d.rectangle([.32 * C, .40 * C, .68 * C, .60 * C], fill=255)
    return cutout(m, shade=False)


def streamer():
    m = mask(); d = ImageDraw.Draw(m)
    pts = []
    for i in range(120):
        t = i / 119
        a = t * math.pi * 3.2
        x = .5 + .16 * math.sin(a) + (t - .5) * .10
        y = .18 + t * .64
        pts.append((x * C, y * C))
    d.line(pts, fill=255, width=int(.06 * C), joint="curve")
    return cutout(m, shade=False)


def balloon():
    m = mask(); d = ImageDraw.Draw(m)
    d.ellipse([.30 * C, .08 * C, .70 * C, .58 * C], fill=255)
    d.polygon([(.46 * C, .60 * C), (.54 * C, .60 * C), (.50 * C, .55 * C)], fill=255)
    s = mask(); ds = ImageDraw.Draw(s)
    pts = [((.50 + .04 * math.sin(t * 9)) * C, (.61 + t * .36) * C) for t in [i / 40 for i in range(41)]]
    ds.line(pts, fill=255, width=int(.012 * C))
    # A highlight: a small paper-white crescent stays white; drawn as ink-free (part of the paper).
    out = cutout(m, extra_ink=s)
    hl = mask(); dh = ImageDraw.Draw(hl); dh.ellipse([.38 * C, .16 * C, .47 * C, .30 * C], fill=255)
    hl = hl.filter(ImageFilter.GaussianBlur(3 * SS)).point(lambda v: int(v * .7))
    white = Image.new("RGBA", (C, C), (255, 255, 255, 0)); white.putalpha(hl)
    # The highlight is pure white over the tinted paper only where the tint multiplies: it is part of the white fill,
    # so instead lighten the shading: nothing to do in RGB (the fill is already white). Keep it as a brighter rim.
    return out


def flame():
    m = mask(); d = ImageDraw.Draw(m)
    pts = []
    for i in range(80):
        t = i / 79 * math.pi * 2
        r = .5 + .5 * math.cos(t)              # 1 at the bottom, 0 at the tip
        x = .5 + math.sin(t) * .13 * (r ** .7)
        y = .68 - (1 - r) * .52 + (r ** 3) * .06
        pts.append((x * C, y * C))
    d.polygon(pts, fill=255)
    return glow(m, 2 * SS, 18 * SS, .55)


def puff():
    m = mask(); d = ImageDraw.Draw(m)
    circles(d, [(.40, .55, .15), (.56, .48, .17), (.62, .60, .13), (.46, .64, .12)])
    return glow(m, 10 * SS, 22 * SS, .4)


def sparkle():
    m = mask(); d = ImageDraw.Draw(m)
    pts = []
    for i in range(8):
        a = i * math.pi / 4 - math.pi / 2
        r = .36 if i % 2 == 0 else .07
        pts.append(((.5 + math.cos(a) * r) * C, (.5 + math.sin(a) * r) * C))
    d.polygon(pts, fill=255)
    return glow(m, 1.5 * SS, 14 * SS, .5)


def heart():
    m = mask(); d = ImageDraw.Draw(m)
    pts = []
    for i in range(200):
        t = i / 199 * math.pi * 2
        x = 16 * math.sin(t) ** 3
        y = 13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)
        pts.append(((.5 + x * .021) * C, (.47 - y * .021) * C))
    d.polygon(pts, fill=255)
    return cutout(m)


def steam():
    m = mask(); d = ImageDraw.Draw(m)
    pts = []
    for i in range(100):
        t = i / 99
        x = .5 + .12 * math.sin(t * math.pi * 2.2)
        y = .85 - t * .70
        pts.append((x * C, y * C))
    d.line(pts, fill=255, width=int(.07 * C), joint="curve")
    a = m.filter(ImageFilter.GaussianBlur(7 * SS))
    # Thinner and fainter toward the top.
    fade = Image.linear_gradient("L").resize((C, C)).point(lambda v: int(80 + v * .69))
    a = ImageChops.multiply(a, fade)
    return Image.merge("RGBA", (Image.new("L", (C, C), 255),) * 3 + (a,))


def sun():
    m = mask(); d = ImageDraw.Draw(m)
    d.ellipse([.31 * C, .31 * C, .69 * C, .69 * C], fill=255)
    for i in range(12):
        a = i * math.pi / 6
        r0, r1, w = .24, .40 if i % 2 == 0 else .34, .045
        cx, cy = math.cos(a), math.sin(a); px, py = -cy, cx
        d.polygon([((.5 + cx * r0 + px * w) * C, (.5 + cy * r0 + py * w) * C), ((.5 + cx * r1) * C, (.5 + cy * r1) * C),
                   ((.5 + cx * r0 - px * w) * C, (.5 + cy * r0 - py * w) * C)], fill=255)
    return cutout(m, shade=False)


def star():
    # A five-pointed star (Good Morning's chef: COMPLIMENTS TO THE CHEF, five stars), its tips a little rounded. Cell 16,
    # after the original sixteen (2026-11-17): cue assets store the index, and Summer and Wild West use the sun at 12.
    m = mask(); d = ImageDraw.Draw(m)
    pts = []
    for i in range(10):
        a = -math.pi / 2 + i * math.pi / 5
        r = .37 if i % 2 == 0 else .16
        pts.append(((.5 + math.cos(a) * r) * C, (.535 + math.sin(a) * r) * C))
    d.polygon(pts, fill=255)
    m = m.filter(ImageFilter.GaussianBlur(3 * SS)).point(lambda v: 255 if v > 140 else 0)
    return cutout(m)


def dash():
    m = mask(); d = ImageDraw.Draw(m)
    d.rounded_rectangle([.18 * C, .44 * C, .82 * C, .56 * C], radius=.06 * C, fill=255)
    return glow(m, 3 * SS, 10 * SS, .35)


def shard():
    m = mask(); d = ImageDraw.Draw(m)
    d.polygon([(.30 * C, .30 * C), (.72 * C, .38 * C), (.62 * C, .70 * C), (.40 * C, .62 * C)], fill=255)
    return cutout(m)


def bang():
    m = mask(); d = ImageDraw.Draw(m)
    d.polygon([(.42 * C, .12 * C), (.60 * C, .12 * C), (.55 * C, .62 * C), (.47 * C, .62 * C)], fill=255)
    d.ellipse([.43 * C, .70 * C, .57 * C, .84 * C], fill=255)
    return cutout(m, shade=False)


DRAW = [cloud_a, cloud_b, cloud_small, bird, confetti, streamer, balloon, flame, puff, sparkle, heart, steam, sun, dash, shard, bang, star]


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    out = args[0] if args else "Assets/ShadowWorkshop/PicoFinal/Art/Cues/CueAtlas.png"
    atlas = Image.new("RGBA", (CELL * COLS, CELL * COLS), (255, 255, 255, 0))
    for i, draw in enumerate(DRAW):
        cell = draw().resize((CELL, CELL), Image.LANCZOS)
        atlas.paste(cell, ((i % COLS) * CELL, (i // COLS) * CELL))
        print("drew", ART[i])
    # Bleed colour into transparent texels (white paper, the ink stays where alpha is) so mips don't darken edges
    # beyond what the ink already does: transparent texels take white.
    px = atlas.load()
    for y in range(atlas.height):
        for x in range(atlas.width):
            r, g, b, a = px[x, y]
            if a == 0:
                px[x, y] = (255, 255, 255, 0)
    atlas.save(out)
    print("wrote", out)
    if "--sheet" in sys.argv:
        sheet_path = sys.argv[sys.argv.index("--sheet") + 1]
        bg = Image.new("RGBA", atlas.size, (238, 222, 190, 255))
        tints = [(255, 255, 255), (250, 250, 255), (235, 240, 255), (255, 255, 255), (247, 185, 202), (169, 201, 246),
                 (205, 185, 243), (255, 214, 140), (255, 255, 255), (255, 227, 166), (247, 150, 170), (255, 255, 255),
                 (242, 196, 109), (255, 255, 255), (214, 130, 90), (246, 195, 160), (242, 196, 109)]
        tinted = atlas.copy(); tp = tinted.load()
        for y in range(atlas.height):
            for x in range(atlas.width):
                r, g, b, a = tp[x, y]
                i = (y // CELL) * COLS + x // CELL
                t = tints[i] if i < len(tints) else (255, 255, 255)
                tp[x, y] = (r * t[0] // 255, g * t[1] // 255, b * t[2] // 255, a)
        Image.alpha_composite(bg, tinted).save(sheet_path)
        print("wrote", sheet_path)


if __name__ == "__main__":
    main()
