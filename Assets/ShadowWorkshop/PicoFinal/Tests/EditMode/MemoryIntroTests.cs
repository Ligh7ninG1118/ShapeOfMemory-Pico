using CloudGarden.ShadowPuzzle;
using NUnit.Framework;
using UnityEngine;

namespace ShapeOfMemory.Tests
{
    public class MemoryIntroTests
    {
        // Every level's things lie flat on the table, inside it, clear of the riddle card and the console, and apart.
        [Test] public void ScatterLiesOnTheTableAndApart([ValueSource(nameof(Levels))] string id)
        {
            var level=Torch2Level.Load(id);Assert.NotNull(level,id);
            var poses=Torch2Transit.Scatter(level);var boxes=new Rect[poses.Length];
            Rect card=Rect.MinMaxRect(-.45f,-.40f,-.13f,-.22f),console=Rect.MinMaxRect(.05f,-.40f,.45f,-.26f);
            for(int i=0;i<poses.Length;i++)
            {
                var piece=level.pieces[i];var bounds=piece.mesh.bounds;var min=Vector3.positiveInfinity;var max=Vector3.negativeInfinity;
                for(int c=0;c<8;c++)
                {
                    var corner=bounds.center+Vector3.Scale(bounds.extents,new Vector3((c&1)==0?-1:1,(c&2)==0?-1:1,(c&4)==0?-1:1));
                    var p=poses[i].position+poses[i].rotation*Vector3.Scale(corner,piece.Scale);min=Vector3.Min(min,p);max=Vector3.Max(max,p);
                }
                Assert.AreEqual(-.005f,min.y,.002f,$"{id}/{piece.label} rests on the table top");
                boxes[i]=Rect.MinMaxRect(min.x,min.z,max.x,max.z);
                Assert.IsTrue(min.x>-.7f&&max.x<.7f&&min.z>-.46f&&max.z<.54f,$"{id}/{piece.label} on the table: {boxes[i]}");
                Assert.IsFalse(boxes[i].Overlaps(card),$"{id}/{piece.label} clear of the riddle card");
                Assert.IsFalse(boxes[i].Overlaps(console),$"{id}/{piece.label} clear of the console");
            }
            for(int i=0;i<boxes.Length;i++)for(int j=i+1;j<boxes.Length;j++)
            {
                var a=boxes[i];var b=boxes[j];float dx=Mathf.Min(a.xMax,b.xMax)-Mathf.Max(a.xMin,b.xMin),dz=Mathf.Min(a.yMax,b.yMax)-Mathf.Max(a.yMin,b.yMin);
                Assert.IsFalse(dx>.01f&&dz>.01f,$"{id}: {level.pieces[i].label} and {level.pieces[j].label} overlap by {dx:0.000} x {dz:0.000}");
            }
        }
        static string[] Levels=>MemoryProgress.Levels;
    }
}
