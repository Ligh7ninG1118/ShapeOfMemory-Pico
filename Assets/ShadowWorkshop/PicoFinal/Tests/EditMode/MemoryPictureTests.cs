using CloudGarden.ShadowPuzzle;
using NUnit.Framework;
using UnityEngine;

namespace ShapeOfMemory.Tests
{
    // The found picture is drawn from the shadow's exact outline at PictureSize, not the score's 64-cell raster: ink and
    // paper both show, with soft (anti-aliased) edges; a two-wall level shows both walls' shadows side by side.
    public class MemoryPictureTests
    {
        static (float ink,int soft) Measure(Texture2D picture,int x0,int x1)
        {
            var pixels=picture.GetPixels32();int ink=0,soft=0,total=0;Color32 paper=MemoryPalette.Paper;
            for(int y=0;y<picture.height;y++)for(int x=x0;x<x1;x++)
            {
                var p=pixels[y*picture.width+x];total++;
                if(p.r==paper.r&&p.g==paper.g&&p.b==paper.b)continue;
                ink++;if(Mathf.Abs(p.r-paper.r)<Mathf.Abs(Color.Lerp(paper,Color.black,.5f).r-paper.r))soft++;
            }
            return (ink/(float)total,soft);
        }
        // Every chapter's: one wall sharp and framed; two walls side by side, and each alone.
        [Test] public void ChapterPicturesAreSharpAndFramed([ValueSource(nameof(Chapters))] string id)
        {
            var kit=new MemoryKit();var pages=new MemoryPages(kit){Readable=true};
            try
            {
                var level=Torch2Level.Load(id);Assert.NotNull(level,id);
                var picture=pages.Picture(level);
                Assert.AreEqual(MemoryPages.PictureSize,picture.width);Assert.AreEqual(MemoryPages.PictureSize,picture.height);
                Assert.AreSame(picture,pages.Picture(level),"drawn once a session");
                if(level.lamps.Length>1)
                {
                    int half=picture.width/2;
                    Assert.Greater(Measure(picture,0,half).ink,.02f,"the left wall's shadow");Assert.Greater(Measure(picture,half,picture.width).ink,.02f,"the right wall's");
                    for(int slot=0;slot<level.lamps.Length;slot++)Assert.Greater(Measure(pages.Picture(level,slot),0,MemoryPages.PictureSize).ink,.05f,"each wall alone, slot "+slot);
                    return;
                }
                var (ink,soft)=Measure(picture,0,picture.width);
                Assert.That(ink,Is.InRange(.08f,.75f),"the shadow fills the frame without flooding it");
                Assert.Greater(soft,0,"edges are anti-aliased");
            }
            finally{foreach(var o in kit.Owned)Object.DestroyImmediate(o);}
        }
        static string[] Chapters=>MemoryProgress.Levels;
    }
}
