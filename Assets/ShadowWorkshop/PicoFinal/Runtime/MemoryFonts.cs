using TMPro;
using UnityEngine;

namespace ShapeOfMemory
{
    // The game's typefaces, by role (MemoryText.Role) and script. Static TMP atlases baked in the editor from the TTFs in
    // PicoFinal/Fonts (MetaHackthone > Pico Final > Fonts > Bake atlases, which also fills this asset); nothing is made at
    // run time, and the TTFs never ship. Theme: dreamy, warm, classic (2026-11-14).
    // - Text: Merriweather ("Memory Text"); its weight table holds the Italic and the Bold, so <i>, <b> and FontStyles pick
    //   real faces, not slanted or thickened ones.
    // - Caps: Cinzel SemiBold ("Memory Caps"): kickers, chapter numbers, banners, small headings.
    // - Title: Cinzel Decorative Bold: the cover's title, chapter names, THANK YOU. Large sizes only.
    // - Chinese (and the other upright scripts): Noto Serif SC; Text is Regular (Bold in its weight table), Caps and
    //   Title are Bold.
    public sealed class MemoryFonts : ScriptableObject
    {
        public const string ResourcePath="ShapeOfMemory/Fonts";
        [Header("Latin")]
        public TMP_FontAsset text;
        public TMP_FontAsset italic,bold,caps,title;
        [Header("Chinese")]
        public TMP_FontAsset cjkText;
        public TMP_FontAsset cjkBold;

        static MemoryFonts loaded;
        public static MemoryFonts Load(){if(!loaded)loaded=Resources.Load<MemoryFonts>(ResourcePath);return loaded;}

        // The face a role prints in, for a language that is upright (Chinese) or not.
        public TMP_FontAsset For(MemoryText.Role role,bool upright)
        {
            if(upright)return role==MemoryText.Role.Text?cjkText:cjkBold;
            return role switch{MemoryText.Role.Caps=>caps,MemoryText.Role.Title=>title,_=>text};
        }
    }
}
