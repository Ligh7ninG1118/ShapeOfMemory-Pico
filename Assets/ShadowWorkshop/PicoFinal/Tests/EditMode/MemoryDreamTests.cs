using System.Linq;
using CloudGarden.ShadowPuzzle;
using NUnit.Framework;
using UnityEngine;

namespace ShapeOfMemory.Tests
{
    public class MemoryDreamTests
    {
        // The kit's longest sides (metres; build_dream_kit.py prints them).
        static readonly float[] Sizes={2.93f,3.90f,4.36f,2.75f,3.50f,1.80f,1.80f,2.28f,4.74f,2.10f,2.60f,2.75f,4.60f,2.82f};

        [Test] public void LayoutIsTheSameEveryTime()
        {
            var a=MemoryDream.Layout(Sizes,MemoryDream.Eye,MemoryDream.Count);var b=MemoryDream.Layout(Sizes,MemoryDream.Eye,MemoryDream.Count);
            for(int i=0;i<a.Length;i++){Assert.AreEqual(a[i].Position,b[i].Position);Assert.AreEqual(a[i].Piece,b[i].Piece);Assert.AreEqual(a[i].Rank,b[i].Rank);}
        }

        // Every copy placed, in the band from the eyes, upright and turned off the line to the eyes, at its own scale, and
        // the right size to the eye: none as big as a wall (about 45 degrees).
        [Test] public void EveryCopySitsInTheBand([Values(1,7,42,1234)] int seed)
        {
            var eye=MemoryDream.Eye;var spots=MemoryDream.Layout(Sizes,eye,MemoryDream.Count,seed);
            for(int i=0;i<spots.Length;i++)
            {
                var s=spots[i];Assert.Greater(s.Distance,0,$"copy {i} placed (seed {seed})");
                var toward=s.Position-eye;Assert.AreEqual(s.Distance,toward.magnitude,1e-3f);
                Assert.That(s.Distance,Is.InRange(MemoryDream.Near-1e-3f,MemoryDream.Far+1e-3f),$"copy {i} distance");
                float half=s.Across*.5f,elevation=Mathf.Asin(toward.normalized.y)*Mathf.Rad2Deg,azimuth=Mathf.Atan2(toward.x,toward.z)*Mathf.Rad2Deg;
                Assert.That(elevation-half,Is.GreaterThanOrEqualTo(MemoryDream.Low-1e-2f),$"copy {i} low edge");
                Assert.That(elevation+half,Is.LessThanOrEqualTo(MemoryDream.High+1e-2f),$"copy {i} high edge");
                Assert.That(Mathf.Abs(azimuth)+half,Is.LessThanOrEqualTo(MemoryDream.Azimuth+1e-2f),$"copy {i} azimuth");
                Assert.That(s.Scale,Is.InRange(MemoryDream.ScaleMin,MemoryDream.ScaleMax),$"copy {i} scale");
                Assert.AreEqual(Sizes[s.Piece]*s.Scale,s.Size,1e-4f,$"copy {i} size");
                Assert.AreEqual(2*Mathf.Atan(s.Size*.5f/s.Distance)*Mathf.Rad2Deg,s.Across,1e-2f,$"copy {i} across");
                Assert.That(s.Across,Is.InRange(2f,25f),$"copy {i} across");
                Assert.That(Vector3.Angle(s.Facing*Vector3.up,Vector3.up),Is.LessThanOrEqualTo(MemoryDream.Tilt*1.5f+1e-2f),$"copy {i} upright");
                var flatToEyes=Vector3.ProjectOnPlane(-toward,Vector3.up);var flatFront=Vector3.ProjectOnPlane(s.Facing*Vector3.forward,Vector3.up);
                Assert.That(Vector3.Angle(flatToEyes,flatFront),Is.InRange(MemoryDream.TurnMin-MemoryDream.Tilt,MemoryDream.TurnMax+MemoryDream.Tilt),$"copy {i} turned off the eyes");
            }
            Assert.AreEqual(Enumerable.Range(0,spots.Length),spots.Select(s=>s.Rank).OrderBy(r=>r),"each surfaces in its own turn");
            Assert.AreEqual(Enumerable.Range(0,Sizes.Length),spots.Select(s=>s.Piece).Distinct().OrderBy(p=>p),"every piece shows");
        }

        // Seen from the eyes, no two copies overlap (Gap between their outlines).
        [Test] public void CopiesStandApart([Values(1,7,42,1234)] int seed)
        {
            var eye=MemoryDream.Eye;var spots=MemoryDream.Layout(Sizes,eye,MemoryDream.Count,seed);
            for(int i=0;i<spots.Length;i++)for(int j=i+1;j<spots.Length;j++)
            {
                float apart=Vector3.Angle(spots[i].Position-eye,spots[j].Position-eye)-(spots[i].Across+spots[j].Across)*.5f;
                Assert.GreaterOrEqual(apart,MemoryDream.Gap-1e-2f,$"copies {i} and {j} (seed {seed})");
            }
        }

        // The book grows into the walls well in front of everything: the nearest is at least four times as far as the
        // walls' corner, and even bobbing and at full size none reaches back into the book's room.
        [Test] public void CopiesStayClearOfTheBook()
        {
            var eye=MemoryDream.Eye;float corner=Vector3.Distance(eye,Torch2Projection.Corner);
            Assert.GreaterOrEqual(MemoryDream.Near,4*corner,"Near against the walls' corner");
            foreach(var s in MemoryDream.Layout(Sizes,eye,MemoryDream.Count))
                Assert.Greater(s.Distance-s.Size*.5f,2*corner,$"copy of piece {s.Piece} reaches toward the book");
        }

        // Too many to fit: the ones without room are left out, not stacked.
        [Test] public void LeftOutRatherThanStacked()
        {
            var spots=MemoryDream.Layout(Sizes,MemoryDream.Eye,400);
            Assert.Greater(spots.Count(s=>s.Distance>0),10);Assert.Greater(spots.Count(s=>s.Distance<=0),0);
        }
    }
}
