"""Shape of Memory's font files (Assets/ShadowWorkshop/PicoFinal/Fonts), made from Google Fonts' originals (SIL OFL 1.1).

The game prints with static TMP atlases baked from these (MetaHackthone > Pico Final > Fonts > Bake atlases); the TTFs
stay in the editor and never ship. Roles (MemoryText.Role):
  Text  - Merriweather Regular / Italic / Bold (text optical size 18, normal width), renamed "Memory Text".
  Caps  - Cinzel SemiBold, renamed "Memory Caps".
  Title - Cinzel Decorative Bold, unmodified.
  Chinese - Noto Serif SC Regular / Bold, cut down to GB2312 plus Latin.
Cinzel and Merriweather reserve their names under the OFL, so the instances made here (Modified Versions) carry our own.

Sources, from https://github.com/google/fonts (ofl/<family>/), in one folder:
  CinzelDecorative-Bold.ttf, Cinzel[wght].ttf, Merriweather[opsz,wdth,wght].ttf,
  Merriweather-Italic[opsz,wdth,wght].ttf, NotoSerifSC[wght].ttf, and each family's OFL.txt saved as OFL-<Family>.txt.
Run: python Tools/ShadowArt/build_fonts.py <sources folder>   (needs fontTools: pip install fonttools)
"""
from pathlib import Path
import shutil
import sys

from fontTools.ttLib import TTFont
from fontTools.varLib import instancer
from fontTools import subset

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/ShadowWorkshop/PicoFinal/Fonts'


def rename(font, family, style, weight, italic=False):
    """Our own family name in every name record a font menu or TMP reads (the reserved name must not survive)."""
    name = font['name']
    full, ps = f'{family} {style}', f'{family.replace(" ", "")}-{style.replace(" ", "")}'
    for nid in (1, 2, 3, 4, 6, 16, 17, 21, 22, 25):
        name.removeNames(nameID=nid)
    for nid, value in ((1, family), (2, style), (3, f'{ps};ShapeOfMemory'), (4, full), (6, ps)):
        name.setName(value, nid, 3, 1, 0x409)
    os2 = font['OS/2']
    os2.usWeightClass = weight
    bold = weight >= 700
    os2.fsSelection = (os2.fsSelection & ~0b1100001) | (1 if italic else 0) | (0b100000 if bold else 0) | (0b1000000 if not (bold or italic) else 0)
    font['head'].macStyle = (1 if bold else 0) | (2 if italic else 0)


def instance(source, axes, family, style, weight, out, italic=False, keep=None):
    font = TTFont(source)
    if keep is not None:
        options = subset.Options()
        options.layout_features = ['*']
        options.name_IDs = ['*']
        options.notdef_outline = True
        options.glyph_names = False
        sub = subset.Subsetter(options)
        sub.populate(unicodes=keep)
        sub.subset(font)
    font = instancer.instantiateVariableFont(font, axes, updateFontNames=False)
    rename(font, family, style, weight, italic)
    font.save(out)
    print(f'{out.name:34s} {out.stat().st_size / 1024:8.0f} KB')


def chinese_set():
    """Every character GB2312 has (6,763 hanzi and its punctuation), plus ASCII, Latin-1 and typographic punctuation."""
    keep = set(range(0x20, 0x7F)) | set(range(0xA0, 0x100)) | set(range(0x2010, 0x2028)) | {0x2032, 0x2033, 0x20AC}
    for cp in range(0x2000, 0x10000):
        if 0xD800 <= cp <= 0xDFFF:
            continue
        try:
            chr(cp).encode('gb2312')
            keep.add(cp)
        except UnicodeEncodeError:
            pass
    return keep


def main(src):
    src = Path(src)
    OUT.mkdir(parents=True, exist_ok=True)
    text = src / 'Merriweather[opsz,wdth,wght].ttf'
    italic = src / 'Merriweather-Italic[opsz,wdth,wght].ttf'
    instance(text, {'wght': 400, 'wdth': 100, 'opsz': 18}, 'Memory Text', 'Regular', 400, OUT / 'MemoryText-Regular.ttf')
    instance(text, {'wght': 700, 'wdth': 100, 'opsz': 18}, 'Memory Text', 'Bold', 700, OUT / 'MemoryText-Bold.ttf')
    instance(italic, {'wght': 400, 'wdth': 100, 'opsz': 18}, 'Memory Text', 'Italic', 400, OUT / 'MemoryText-Italic.ttf', italic=True)
    instance(src / 'Cinzel[wght].ttf', {'wght': 600}, 'Memory Caps', 'SemiBold', 600, OUT / 'MemoryCaps-SemiBold.ttf')
    shutil.copyfile(src / 'CinzelDecorative-Bold.ttf', OUT / 'CinzelDecorative-Bold.ttf')
    print('CinzelDecorative-Bold.ttf          (copied unmodified)')
    keep = chinese_set()
    noto = src / 'NotoSerifSC[wght].ttf'
    instance(noto, {'wght': 400}, 'Noto Serif SC', 'Regular', 400, OUT / 'NotoSerifSC-Regular.ttf', keep=keep)
    instance(noto, {'wght': 700}, 'Noto Serif SC', 'Bold', 700, OUT / 'NotoSerifSC-Bold.ttf', keep=keep)
    for family in ('Merriweather', 'Cinzel', 'CinzelDecorative', 'NotoSerifSC'):
        shutil.copyfile(src / f'OFL-{family}.txt', OUT / f'OFL-{family}.txt')


if __name__ == '__main__':
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    main(sys.argv[1])
