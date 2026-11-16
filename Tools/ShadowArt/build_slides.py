"""The pictures the lamps project in Shape of Memory (Torch2Level.leftSlide / rightSlide), one scene per chapter.

A slide tints the lamp's light (ShadowWorkshop/Projected Slide): blurred and grey while the shadow is far off, sharp and
in colour once the memory is found, and squeezed into 55-100% of the light so the shadow stays the darkest thing on the
wall. Only the lamp's spot shows: centre UV (0.56, 0.41) on the left wall and (0.44, 0.41) on the right, radius about
0.37 across and 0.39 up. Each scene is laid out round its chapter's shadow and its found-moment cut-outs
(MemorySolveCue): Good Morning's window is where the cut-out sun rises, its kettle where the cut-out steam starts.

Flat storybook shapes, drawn at 2x and reduced. 768 x 720 (the wall's 128:120).

    python -I Tools/ShadowArt/build_slides.py [out_dir] [--preview outlines.json preview_dir]
"""
import json, math, os, sys
from PIL import Image, ImageDraw, ImageFilter

W, H, SS = 768, 720, 2
CW, CH = W * SS, H * SS
SPOT = {0: (.561, .411), 1: (.4395, .411)}   # spot centre (UV, y up) per wall
RADIUS = (.368, .393)                         # spot radius in UV (x, y)


def hexc(v, a=255):
    return ((v >> 16) & 255, (v >> 8) & 255, v & 255, a)


class Slide:
    def __init__(self):
        self.img = Image.new("RGBA", (CW, CH), (255, 255, 255, 255))

    # UV (y up) to canvas pixels.
    def P(self, u, v):
        return (u * CW, (1 - v) * CH)

    def layer(self):
        return Image.new("RGBA", (CW, CH), (0, 0, 0, 0))

    def put(self, layer, blur=0):
        if blur:
            layer = layer.filter(ImageFilter.GaussianBlur(blur * SS))
        self.img = Image.alpha_composite(self.img, layer)

    def vgrad(self, stops, v0=0, v1=1, u0=0, u1=1):
        """Vertical gradient between v0 and v1 through (v, colour) stops."""
        lay = self.layer(); d = ImageDraw.Draw(lay)
        y_top, y_bot = int((1 - v1) * CH), int((1 - v0) * CH)
        for y in range(y_top, y_bot):
            v = 1 - (y + .5) / CH
            for i in range(len(stops) - 1):
                (va, ca), (vb, cb) = stops[i], stops[i + 1]
                if va <= v <= vb or (i == 0 and v < va) or (i == len(stops) - 2 and v > vb):
                    t = 0 if vb == va else min(1, max(0, (v - va) / (vb - va)))
                    c = tuple(int(ca[k] + (cb[k] - ca[k]) * t) for k in range(4))
                    break
            d.line([(u0 * CW, y), (u1 * CW, y)], fill=c)
        self.put(lay)

    def rect(self, u0, v0, u1, v1, c, d=None):
        dd = d or ImageDraw.Draw(self.img)
        a, b = self.P(u0, v1), self.P(u1, v0)
        dd.rectangle([a, b], fill=c)

    def poly(self, pts, c, d=None):
        (d or ImageDraw.Draw(self.img)).polygon([self.P(u, v) for u, v in pts], fill=c)

    def ellipse(self, u, v, ru, rv, c, d=None):
        a, b = self.P(u - ru, v + rv), self.P(u + ru, v - rv)
        (d or ImageDraw.Draw(self.img)).ellipse([a, b], fill=c)

    def circle(self, u, v, r, c, d=None):
        self.ellipse(u, v, r * H / W, r, c, d)

    def line(self, pts, c, w, d=None):
        (d or ImageDraw.Draw(self.img)).line([self.P(u, v) for u, v in pts], fill=c, width=int(w * CH), joint="curve")

    def glow(self, u, v, r, c, blur):
        lay = self.layer(); self.circle(u, v, r, c, ImageDraw.Draw(lay)); self.put(lay, blur)

    def cloud(self, u, v, s, c, shade):
        lay = self.layer(); d = ImageDraw.Draw(lay)
        for du, dv, r in [(-.9, 0, .55), (-.35, .35, .75), (.35, .28, .65), (.9, .02, .5), (0, -.1, .7)]:
            self.circle(u + du * s * .9, v + dv * s, r * s, shade, d)
        self.put(lay, 2)
        lay = self.layer(); d = ImageDraw.Draw(lay)
        for du, dv, r in [(-.9, .06, .5), (-.35, .42, .7), (.35, .34, .6), (.9, .08, .45), (0, 0, .62)]:
            self.circle(u + du * s * .9, v + dv * s, r * s, c, d)
        self.put(lay, 1.5)

    def done(self):
        return self.img.resize((W, H), Image.LANCZOS).convert("RGB")


def catenary(u0, v0, u1, v1, sag, n=60):
    pts = []
    for i in range(n + 1):
        t = i / n
        pts.append((u0 + (u1 - u0) * t, v0 + (v1 - v0) * t - sag * 4 * t * (1 - t)))
    return pts


# ---- I. Birthday (left wall): an evening party. Striped wallpaper, bunting over the cake, a window on the dusk, the
# cake's table under it (its foot is at v .156).
def birthday():
    s = Slide()
    s.rect(0, 0, 1, 1, hexc(0xF8DCE2))
    for i in range(0, 30):
        u = i * .05
        if i % 2 == 0:
            s.rect(u, 0, u + .025, 1, hexc(0xFBE9E3))
    # A dusk window left of the cake (the balloons rise past it).
    s.rect(.17, .30, .37, .64, hexc(0xE9CFA9))
    s.vgrad([(.33, hexc(0xF2A47E)), (.45, hexc(0xB989B6)), (.61, hexc(0x4E5A9E))], .33, .61, .19, .35)
    for (u, v) in [(.22, .57), (.31, .54), (.26, .50), (.33, .59)]:
        s.circle(u, v, .006, hexc(0xFFF4D8))
    s.rect(.268, .33, .272, .61, hexc(0xE9CFA9)); s.rect(.19, .465, .35, .475, hexc(0xE9CFA9))
    # Bunting: two garlands of flags in the chapter colours.
    flags = [0xE36D8A, 0x6C9BE8, 0xF0A45E, 0xA88BE0, 0x8FC27A, 0xF2C46D]
    for g, (v0, v1, sag) in enumerate([(.86, .83, .09), (.76, .79, .07)]):
        pts = catenary(.12, v0, .98, v1, sag)
        s.line(pts, hexc(0x8C3553), .004)
        for k in range(1, 14):
            t = k / 14; i = int(t * (len(pts) - 1)); (u, v) = pts[i]
            s.poly([(u - .022, v), (u + .022, v), (u, v - .065)], hexc(flags[(k + g * 3) % len(flags)]))
    # The table: a cloth with a scalloped hem and a rose band.
    s.rect(0, 0, 1, .158, hexc(0xFFF6EA))
    for k in range(40):
        s.ellipse(k * .03 + .015, .158, .016, .012, hexc(0xFFF6EA))
    s.rect(0, .06, 1, .085, hexc(0xF2A0B4))
    return s.done()


# ---- II. Bon Voyage (right wall): a high sky with clouds, a low dawn sun on the left, an airfield on the horizon below the
# plane (which flies through v .27-.55).
def bon_voyage():
    s = Slide()
    s.vgrad([(0, hexc(0xF9DDB8)), (.2, hexc(0xFCE6C6)), (.42, hexc(0xBFD9F2)), (1, hexc(0x5E8FD8))])
    s.glow(.21, .27, .12, hexc(0xFFE7A8, 200), 18)
    s.circle(.21, .27, .055, hexc(0xFFD37E))
    for (u, v, sc) in [(.16, .72, .07), (.58, .82, .06), (.80, .62, .05), (.30, .60, .045), (.12, .46, .05), (.66, .46, .04)]:
        s.cloud(u, v, sc, hexc(0xFFFFFF), hexc(0xC9D6EE))
    # Far hills, the field, the runway in perspective with its centre line, a tower and a terminal.
    s.poly([(0, .17)] + [(u / 40, .17 + .025 * math.sin(u / 40 * 9) + .015) for u in range(41)] + [(1, .17)], hexc(0xA9B7D2))
    s.rect(0, 0, 1, .17, hexc(0x9DB58A))
    s.rect(0, .15, 1, .17, hexc(0xB5C79C))
    s.poly([(.30, .17), (.44, .17), (.62, 0), (.06, 0)], hexc(0x7C7F8E))
    for k in range(6):
        t0, t1 = k / 6, k / 6 + .08
        def at(t):
            return (.37 + (.34 - .37) * t, .17 * (1 - t))
        a, b = at(t0), at(t1)
        s.poly([(a[0] - .004 - .01 * t0, a[1]), (a[0] + .004 + .01 * t0, a[1]), (b[0] + .004 + .01 * t1, b[1]), (b[0] - .004 - .01 * t1, b[1])], hexc(0xF5F0E6))
    s.rect(.62, .17, .80, .215, hexc(0xE6DCCB)); s.rect(.62, .205, .80, .215, hexc(0x8C9AB8))
    for k in range(6):
        s.rect(.635 + k * .027, .18, .648 + k * .027, .197, hexc(0x6C86B5))
    s.rect(.545, .17, .565, .30, hexc(0xE6DCCB))
    s.poly([(.53, .30), (.58, .30), (.572, .335), (.538, .335)], hexc(0x6C86B5))
    s.rect(.536, .335, .574, .345, hexc(0xE6DCCB))
    s.circle(.555, .352, .006, hexc(0xE36D8A))
    return s.done()


# ---- III. Good Morning (left wall): a tiled kitchen. She stands in the middle (u .41-.67); the window behind her right
# shoulder is where the cut-out sun rises (u .83, v .29 -> .45); the kettle on the counter is where the steam starts.
def good_morning():
    s = Slide()
    s.rect(0, 0, 1, 1, hexc(0xEAF1E4))
    for row in range(40):
        v = row * .032
        s.rect(0, v, 1, v + .003, hexc(0xD3DFCF))
        off = .03 if row % 2 else 0
        for k in range(20):
            u = off + k * .06
            s.rect(u, v, u + .003, v + .032, hexc(0xD3DFCF))
    # Window: a wooden frame, a sunrise sky, rooftops far off.
    s.rect(.66, .20, .94, .60, hexc(0xF1E3C8))
    s.vgrad([(.22, hexc(0xFFB873)), (.36, hexc(0xFFE2A2)), (.58, hexc(0xBFE0F2))], .22, .58, .675, .925)
    s.glow(.80, .25, .09, hexc(0xFFF0B8, 220), 16)
    for (u0, u1, top) in [(.675, .73, .27), (.73, .77, .30), (.77, .84, .26), (.84, .925, .29)]:
        s.rect(u0, .22, u1, top, hexc(0xC7A9C6))
    s.rect(.795, .22, .805, .58, hexc(0xF1E3C8)); s.rect(.675, .395, .925, .405, hexc(0xF1E3C8))
    s.rect(.65, .19, .95, .205, hexc(0xD9C3A0))
    # A shelf of jars on the left, and a plant.
    s.rect(.17, .60, .40, .615, hexc(0xB7875C))
    for (u, w, h, lid) in [(.20, .035, .07, 0xE36D8A), (.25, .03, .09, 0x6C9BE8), (.30, .04, .06, 0xF0A45E)]:
        s.rect(u, .615, u + w, .615 + h, hexc(0xFDF8EE)); s.rect(u - .003, .615 + h, u + w + .003, .615 + h + .012, hexc(lid))
    s.poly([(.355, .615), (.39, .615), (.385, .655), (.36, .655)], hexc(0xD2876A))
    for a in (-35, -10, 15, 40):
        r = math.radians(a)
        s.ellipse(.372 + .02 * math.sin(r), .675 + .02 * math.cos(r), .009, .022, hexc(0x7FB069))
    # The counter, and the kettle under the cut-out steam (u .27-.34).
    s.rect(0, 0, 1, .12, hexc(0xC99B6D)); s.rect(0, .105, 1, .125, hexc(0xE2BC8E))
    s.ellipse(.30, .155, .045, .042, hexc(0xE36D5A))
    s.rect(.26, .125, .34, .15, hexc(0xE36D5A))
    s.poly([(.335, .16), (.375, .20), (.385, .195), (.345, .145)], hexc(0xE36D5A))
    s.line([(.27, .185), (.30, .215), (.33, .185)], hexc(0x2B1E22), .008)
    s.circle(.30, .2, .008, hexc(0x2B1E22))
    return s.done()


# ---- IV. Home (both walls): a warm living room in the evening. Sage wallpaper with small flowers above a panelled
# wainscot; a curtained night window and a picture on the dog's wall; a clock and a lamp's glow on the man's.
def home(wall):
    s = Slide()
    s.rect(0, 0, 1, 1, hexc(0xE4E7CF))
    for row in range(16):
        for k in range(14):
            u = k * .075 + (.0375 if row % 2 else 0); v = .3 + row * .055
            s.circle(u, v, .006, hexc(0xF2B6A6))
            s.circle(u + .008, v + .006, .004, hexc(0xB8C99A))
    s.rect(0, 0, 1, .28, hexc(0xD8B48C)); s.rect(0, .27, 1, .29, hexc(0xB98E64))
    for k in range(9):
        u = .03 + k * .12
        s.rect(u, .05, u + .09, .23, hexc(0xCFA77D)); s.rect(u + .006, .056, u + .084, .224, hexc(0xD8B48C))
    if wall == 0:
        # Night window with curtains, upper left; a heart picture upper right.
        s.rect(.22, .52, .42, .82, hexc(0xF1E3C8))
        s.vgrad([(.54, hexc(0x3E4C8E)), (.80, hexc(0x24305E))], .54, .80, .235, .405)
        s.circle(.36, .74, .02, hexc(0xFFF1C4))
        for (u, v) in [(.26, .70), (.30, .77), (.28, .60), (.39, .62)]:
            s.circle(u, v, .004, hexc(0xFFF4D8))
        s.rect(.318, .54, .322, .80, hexc(0xF1E3C8))
        s.poly([(.20, .85), (.26, .85), (.24, .50), (.19, .50)], hexc(0xC0606E))
        s.poly([(.38, .85), (.44, .85), (.45, .50), (.40, .50)], hexc(0xC0606E))
        s.rect(.18, .84, .46, .86, hexc(0x8C5A3C))
        s.rect(.70, .58, .84, .74, hexc(0xB98E64)); s.rect(.712, .595, .828, .725, hexc(0xFDF3E3))
        s.circle(.758, .67, .02, hexc(0xE36D8A)); s.circle(.782, .67, .02, hexc(0xE36D8A))
        s.poly([(.741, .662), (.799, .662), (.77, .62)], hexc(0xE36D8A))
    else:
        # A clock upper left; a lamp's warm glow on the right.
        s.circle(.25, .70, .06, hexc(0xB98E64)); s.circle(.25, .70, .05, hexc(0xFDF3E3))
        s.line([(.25, .70), (.25, .735)], hexc(0x2B1E22), .006); s.line([(.25, .70), (.275, .70)], hexc(0x2B1E22), .006)
        s.glow(.70, .62, .10, hexc(0xFFE2A2, 170), 20)
        s.poly([(.66, .66), (.74, .66), (.72, .60), (.68, .60)], hexc(0xF0A45E))
        s.rect(.697, .40, .703, .60, hexc(0x8C5A3C))
    return s.done()


SLIDES = [("Birthday", 0, birthday), ("BonVoyage", 1, bon_voyage), ("GoodMorning", 0, good_morning),
          ("Home_Left", 0, lambda: home(0)), ("Home_Right", 1, lambda: home(1))]


def preview(slides, outlines_path, out_dir):
    """The found wall as the shader draws it: the lamp's warm light turned toward the chapter's accent, times the slide
    (squeezed into .55-1), on the dark page, with the shadow at 10% light."""
    data = json.load(open(outlines_path))
    base = (0x2A, 0x24, 0x36); warm = (255, 230, 184)
    accents = {"Birthday": (250, 158, 179), "BonVoyage": (115, 179, 250), "GoodMorning": (250, 140, 77), "Home": (242, 115, 115)}
    PIX = .0088
    for lv in data["levels"]:
        key = lv["name"].replace("_NEW", "")
        acc = accents[key]; light = tuple(warm[i] * .55 + acc[i] * .45 for i in range(3))
        for j, wall in enumerate(lv["walls"]):
            name = key if key != "Home" else ("Home_Left" if wall == 0 else "Home_Right")
            slide = slides[name].resize((W, H)); sp = slide.load()
            bright = slide.copy(); bp = bright.load()
            b = lv["slots"][j]["bright"]
            mask = Image.new("L", (W, H), 0); md = ImageDraw.Draw(mask)
            tri = lv["slots"][j]["tri"]; pts = [(tri[i], tri[i + 1]) for i in range(0, len(tri), 2)]
            sx, sy = W / (128 * PIX), H / (120 * PIX)
            for i in range(0, len(pts), 3):
                md.polygon([(p[0] * sx, H - p[1] * sy) for p in pts[i:i + 3]], fill=255)
            mp = mask.load()
            for y in range(H):
                for x in range(W):
                    br = b[min(119, int((H - 1 - y) * 120 / H)) * 128 + min(127, int(x * 128 / W))]
                    if mp[x, y]:
                        br *= .1
                    s = sp[x, y]
                    bp[x, y] = tuple(int(min(255, (base[k] + (light[k] - base[k]) * br) * (.55 + .45 * s[k] / 255))) for k in range(3))
            bright.save(os.path.join(out_dir, f"preview_{name}.png"))
            print("preview", name)


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    out = args[0] if args and "--preview" not in sys.argv[1:2] else "Assets/ShadowWorkshop/PicoFinal/Art/Slides"
    os.makedirs(out, exist_ok=True)
    made = {}
    for name, wall, draw in SLIDES:
        img = draw(); made[name] = img
        path = os.path.join(out, f"Slide_{name}.png"); img.save(path); print("wrote", path)
    if "--preview" in sys.argv:
        i = sys.argv.index("--preview")
        preview(made, sys.argv[i + 1], sys.argv[i + 2])


if __name__ == "__main__":
    main()
