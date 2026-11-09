using NUnit.Framework;
using UnityEngine;

namespace ShapeOfMemory.Tests
{
    // The palette's contrast pairs hold (WCAG 2: body text >= 7, large titles >= 4.5, strokes and icons >= 3, each on its
    // own background), and its tokens are the handoff's hex values.
    public class MemoryPaletteTests
    {
        static float Linear(float c)=>c<=.04045f?c/12.92f:Mathf.Pow((c+.055f)/1.055f,2.4f);
        static float Luminance(Color c)=>.2126f*Linear(c.r)+.7152f*Linear(c.g)+.0722f*Linear(c.b);
        public static float Contrast(Color a,Color b){float x=Luminance(a),y=Luminance(b);return (Mathf.Max(x,y)+.05f)/(Mathf.Min(x,y)+.05f);}
        const float Body=7,Large=4.5f,Stroke=3;

        static readonly object[] Pairs=
        {
            new object[]{"Moonlight on Ink700 (wall text)",MemoryPalette.Moonlight,MemoryPalette.Ink700,12.5f,Body},
            new object[]{"Gold on Ink700 (chapter numeral)",MemoryPalette.Gold,MemoryPalette.Ink700,9.2f,Body},
            new object[]{"AquaHover on Ink700 (action words)",MemoryPalette.AquaHover,MemoryPalette.Ink700,11.8f,Body},
            new object[]{"RoseLight on Ink700",MemoryPalette.Light(0),MemoryPalette.Ink700,9.1f,Large},
            new object[]{"CornflowerLight on Ink700",MemoryPalette.Light(1),MemoryPalette.Ink700,8.8f,Large},
            new object[]{"Haze on Ink800 (panel labels)",MemoryPalette.Haze,MemoryPalette.Ink800,8.4f,Body},
            new object[]{"Gold on Leather (chevrons, icons)",MemoryPalette.Gold,MemoryPalette.Leather,7.0f,Stroke},
            new object[]{"Sepia on Paper",MemoryPalette.Sepia,MemoryPalette.Paper,13.2f,Body},
            new object[]{"SepiaSoft on Paper",MemoryPalette.SepiaSoft,MemoryPalette.Paper,5.7f,Large},
            new object[]{"GoldDeep on Paper",MemoryPalette.GoldDeep,MemoryPalette.Paper,5.3f,Large},
            new object[]{"AquaDeep on Paper",MemoryPalette.AquaDeep,MemoryPalette.Paper,5.6f,Large},
            new object[]{"RoseDeep on Paper",MemoryPalette.Deep(0),MemoryPalette.Paper,6.3f,Large},
            new object[]{"CornflowerDeep on Paper",MemoryPalette.Deep(1),MemoryPalette.Paper,7.0f,Large},
            new object[]{"RoseLight on LeatherDeep (banner title)",MemoryPalette.Light(0),MemoryPalette.LeatherDeep,9.2f,Large},
            new object[]{"Ink800 rim against Paper (edge rim)",MemoryPalette.Ink800,MemoryPalette.Paper,14.0f,Stroke},
            new object[]{"Ember on Ink600 (reset fill)",MemoryPalette.Ember,MemoryPalette.Ink600,5.5f,Stroke},
        };
        [TestCaseSource(nameof(Pairs))]
        public void PairHolds(string pair,Color front,Color back,float expected,float floor)
        {
            float ratio=Contrast(front,back);
            Assert.That(ratio,Is.EqualTo(expected).Within(.06f),pair+": the handoff's ratio");
            Assert.That(ratio,Is.GreaterThanOrEqualTo(floor),pair+": its target");
        }
        // Every chapter's accent reads on the surfaces it is printed on: Light as a title on the walls and the banner, Deep
        // on paper and on the empty plate.
        [Test] public void EveryChapterAccentReads()
        {
            for(int chapter=0;chapter<5;chapter++)
            {
                Assert.GreaterOrEqual(Contrast(MemoryPalette.Light(chapter),MemoryPalette.Ink700),Large,"Light on the walls, chapter "+chapter);
                Assert.GreaterOrEqual(Contrast(MemoryPalette.Light(chapter),MemoryPalette.LeatherDeep),Large,"Light on the banner, chapter "+chapter);
                Assert.GreaterOrEqual(Contrast(MemoryPalette.Deep(chapter),MemoryPalette.Paper),Large,"Deep on paper, chapter "+chapter);
                Assert.GreaterOrEqual(Contrast(MemoryPalette.Deep(chapter),MemoryPalette.PaperShade),Large,"Deep on the empty plate, chapter "+chapter);
            }
            Assert.AreEqual(MemoryPalette.Light(0),MemoryPalette.Light(5),"the sixth chapter starts again");
        }
        [Test] public void TokensAreTheHandoffHex()
        {
            Assert.AreEqual("5FD8C6",ColorUtility.ToHtmlStringRGB(MemoryPalette.AquaIdle));
            Assert.AreEqual("F3EADB",ColorUtility.ToHtmlStringRGB(MemoryPalette.Moonlight));
            Assert.AreEqual("16131E",ColorUtility.ToHtmlStringRGB(MemoryPalette.Ink900));
            Assert.AreEqual("8A4528",ColorUtility.ToHtmlStringRGB(MemoryPalette.Deep(3)),"the fourth chapter takes Peach");
            Assert.AreEqual("<color=#A2F4E6>Turn them</color>",MemoryPalette.Colored("Turn them",MemoryPalette.AquaHover));
        }
    }
}
