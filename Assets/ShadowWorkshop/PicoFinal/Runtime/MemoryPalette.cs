using System;
using CloudGarden.ShadowPuzzle;
using UnityEngine;

namespace ShapeOfMemory
{
    // The game's UI colours (UI palette pass, 2026-11-08), each with one job, all chosen against the dusk sky:
    // - Aqua: "you can touch this". Only things a hand pinches, presses or turns, and the words that name them. Brightness
    //   is the state: Idle at rest, Hover with a hand near, Grab while pinched or pressed. Deep for those words on paper.
    // - Gold: "you earned this" (found, chapter numbers, rules, hints). Deep for gold words on paper.
    // - A chapter's own accent: Light on dark surfaces, Deep on paper (Chapter, by the chapter's order).
    // - Moonlight, Haze, Faint: text on dark. Paper, Sepia, Leather: the book. Ink900..Ink500: dark surfaces.
    // - Ember: the hold-to-reset fill only. Coral, red and pink belong to the sky: nothing here uses them.
    // Every light stroke (an aqua edge, a ring, a fingertip) has an Ink800 rim, so it reads on paper and sky alike. The
    // rings and edges are see-through (StrokeRest).
    // Values are sRGB hex (Unity linearises them for the GPU; checked on screen: an unlit quad and TMP text come out exact).
    // No pure white or black anywhere in the UI: Moonlight and Ink900 stand in for them.
    public static class MemoryPalette
    {
        public static readonly Color
            Ink900=Hex(0x16131E),Ink800=Hex(0x1F1A29),Ink700=Hex(0x2A2436),Ink600=Hex(0x382F47),Ink500=Hex(0x4E445F),Table=Hex(0x232838),
            Moonlight=Hex(0xF3EADB),Haze=Hex(0xBDB2C6),Faint=Hex(0x978CA8),
            Paper=Hex(0xF4E8D0),PaperShade=Hex(0xE5D4B4),Sepia=Hex(0x2B1E22),SepiaSoft=Hex(0x6A5650),Leather=Hex(0x5C2A36),LeatherDeep=Hex(0x3F1A25),
            AquaIdle=Hex(0x5FD8C6),AquaHover=Hex(0xA2F4E6),AquaGrab=Hex(0xE2FFF9),AquaDeep=Hex(0x14665D),
            Gold=Hex(0xF2C46D),GoldBright=Hex(0xFFE3A6),GoldDeep=Hex(0x82560F),
            Ember=Hex(0xFF8C6E);

        // Chapter accents in the order chapters take them, starting again after five: Rose, Cornflower, Lilac, Peach,
        // Sage. The game has four chapters and the last takes Peach, so Sage is fifth. Each Light sits at about the same
        // lightness as the others, and so does each Deep.
        static readonly Color[] lights={Hex(0xF7B9CA),Hex(0xA9C9F6),Hex(0xCDB9F3),Hex(0xF6C3A0),Hex(0xC2DBA8)};
        static readonly Color[] deeps={Hex(0x8C3553),Hex(0x2F4C7E),Hex(0x57408E),Hex(0x8A4528),Hex(0x46652F)};
        public static Color Light(int chapter)=>lights[Wrap(chapter)];
        public static Color Deep(int chapter)=>deeps[Wrap(chapter)];
        // A level's chapter: its place among the game's four, or the first (a designer's level played as a book of one).
        public static int ChapterOf(Torch2Level level)=>level?Mathf.Max(0,Array.IndexOf(MemoryProgress.Levels,level.name)):0;
        static int Wrap(int chapter)=>(chapter%lights.Length+lights.Length)%lights.Length;

        // Rims: how far a dark rim stands out round a light stroke (metres in the world).
        public const float RimWidth=.002f;
        // The orb's rings and the pages' turning edges are see-through (2026-11-08: opaque, they were too loud): StrokeRest
        // at rest, up to StrokeLit with a hand near, StrokeHeld while pinched or pressed. Their rims RimAlpha.
        public const float StrokeRest=.5f,StrokeLit=.85f,StrokeHeld=.95f,RimAlpha=.4f;
        public static Color Alpha(Color color,float alpha){color.a=alpha;return color;}
        // A colour as a TMP rich-text tag ("<color=#A2F4E6>").
        public static string Tag(Color color)=>"<color=#"+ColorUtility.ToHtmlStringRGB(color)+">";
        public static string Colored(string words,Color color)=>Tag(color)+words+"</color>";
        public static Color Hex(uint rgb)=>new Color(((rgb>>16)&255)/255f,((rgb>>8)&255)/255f,(rgb&255)/255f,1);
    }
}
