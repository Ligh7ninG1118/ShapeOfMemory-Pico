using System.Linq;
using CloudGarden.ShadowPuzzle;
using NUnit.Framework;
using UnityEngine;

namespace ShapeOfMemory.Tests
{
    public class MemorySolveCueTests
    {
        // The end snap's glide (2026-11-15: "slow down the end snap"): from 0 to 1, never back, setting off at its average
        // speed (the old ease-out set off at three times it) and coming to rest.
        [Test] public void TheGlideSetsOffAtItsAverageSpeedAndComesToRest()
        {
            Assert.AreEqual(0,Torch2Workshop.Glide(0),1e-6f);Assert.AreEqual(1,Torch2Workshop.Glide(1),1e-6f);
            float last=0;for(int i=1;i<=100;i++){float g=Torch2Workshop.Glide(i/100f);Assert.GreaterOrEqual(g,last,"never turns back");Assert.LessOrEqual(g,1+1e-6f,"never overshoots");last=g;}
            const float h=1e-3f;
            Assert.AreEqual(1,Torch2Workshop.Glide(h)/h,.01f,"sets off at the average speed");
            Assert.AreEqual(0,(1-Torch2Workshop.Glide(1-h))/h,.01f,"comes to rest");
        }
        // Every chapter of the shipping book has its own found moment, and each one's cut-outs can be drawn.
        [Test] public void EveryChapterHasItsOwnFoundMoment()
        {
            MemorySolveCue.Clear();var book=MemoryChapters.Load();Assert.IsNotNull(book,"the chapters asset");
            foreach(var level in book.chapters.Where(c=>c))
            {
                var cue=MemorySolveCue.For(level);Assert.IsNotNull(cue,level.name+" has a cue in Resources/"+MemorySolveCue.ResourceFolder);
                Assert.Greater(cue.layers.Length,0,cue.name+" has cut-outs");Assert.GreaterOrEqual(cue.seconds,1);
                foreach(var layer in cue.layers)
                {
                    string at=cue.name+": "+layer.note;
                    Assert.Greater(layer.count,0,at);Assert.Less(layer.wall,level.lamps.Length,at+" names a lit page the level has");
                    if(layer.spawn==MemorySolveCue.Spawn.Points)Assert.GreaterOrEqual(layer.points.Length,1,at+" has points");
                    Assert.Greater(layer.size.x,0,at);Assert.GreaterOrEqual(layer.size.y,layer.size.x,at);Assert.GreaterOrEqual(layer.life.y,layer.life.x,at);
                    Assert.Greater(layer.tints.Length,0,at+" has a colour");
                }
                // No clip check here: this public repository leaves out the Soundsnap clips (their licence forbids sharing
                // the raw files, see README), so a listed sound may have none. The moment plays without it.
            }
        }
        // The atlas's cells tile it: each inside, none overlapping.
        [Test] public void TheAtlasCellsTileTheTexture()
        {
            var arts=(MemorySolveCue.Art[])System.Enum.GetValues(typeof(MemorySolveCue.Art));
            Assert.LessOrEqual(arts.Length,MemorySolveCue.Columns*MemorySolveCue.Columns);
            var cells=arts.Select(MemorySolveCue.Cell).ToArray();
            foreach(var c in cells){Assert.GreaterOrEqual(c.xMin,0);Assert.GreaterOrEqual(c.yMin,0);Assert.LessOrEqual(c.xMax,1+1e-6f);Assert.LessOrEqual(c.yMax,1+1e-6f);}
            for(int i=0;i<cells.Length;i++)for(int j=i+1;j<cells.Length;j++){var a=cells[i];var b=cells[j];a.xMin+=1e-4f;a.yMin+=1e-4f;a.xMax-=1e-4f;a.yMax-=1e-4f;Assert.IsFalse(a.Overlaps(b),arts[i]+" and "+arts[j]);}
            Assert.AreEqual(new Rect(0,.75f,.25f,.25f),MemorySolveCue.Cell(MemorySolveCue.Art.CloudA),"the first cell is the top left");
        }
    }
}
